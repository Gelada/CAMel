﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CAMel.Types.MaterialForm;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.Types.Machine
{
    // Some standards to help develop GCode based machines
    public interface IGCodeMachine : IMachine
    {
        [NotNull] string header { get; }
        [NotNull] string footer { get; }

        [NotNull, UsedImplicitly] string speedChangeCommand { get; }
        [NotNull] string toolChangeCommand { get; }

        [NotNull] string sectionBreak { get; }
        [NotNull] string fileStart { get; }
        [NotNull] string fileEnd { get; }
        [NotNull] string commentStart { get; }
        [NotNull] string commentEnd { get; }

        [NotNull] ToolPoint readTP([NotNull] Dictionary<char, double> values, [NotNull] MaterialTool mT);
    }

    public static class Kinematics
    {
        // Collection of Inverse Kinematics

        // 2-Axis and 3-Axis don't need any work, so they just need writing functions
        // in the GCode library, plus a general purpose linear interpolation.

        [NotNull]
        public static ToolPoint interpolateLinear([NotNull] ToolPoint fP, [NotNull] ToolPoint tP, double p)
        {
            ToolPoint tPt = fP.deepClone();
            tPt.pt = tP.pt * p + fP.pt * (1 - p);
            return tPt;
        }

        // 5-Axis...
        // 5-Axis machine have some non-local issues, especially on machines
        // that can rotate fully, so need non-trivial K and IK functions
        //
        // Should really output a machine state type, but not much use for that yet.

        public static Vector3d ikFiveAxisABTable([NotNull] ToolPoint tP, Vector3d pivot, double toolLength, out Point3d machPt)
        {
            // Always gives B from -pi to pi and A from -pi/2 to pi/2.
            double ao = Math.Asin(tP.dir.Y);
            double bo = Math.Atan2(-tP.dir.X, tP.dir.Z);

            if (ao > Math.PI / 2.0)
            {
                ao = Math.PI - ao;
                bo = bo - Math.PI;
                if (bo < 0) { bo = bo + 2.0 * Math.PI; }
            }

            if (ao < -Math.PI / 2.0)
            {
                ao = Math.PI - ao;
                bo = bo - Math.PI;
                if (bo < 0) { bo = bo + 2.0 * Math.PI; }
            }

            Point3d oPt = tP.pt;

            // rotate from material orientation to machine orientation
            oPt.Transform(Transform.Rotation(bo, Vector3d.YAxis, Point3d.Origin));
            oPt.Transform(Transform.Rotation(ao, Vector3d.XAxis, Point3d.Origin));

            // translate from origin at pivot to origin set so that the tooltip is at machine origin
            oPt = oPt - pivot + Vector3d.ZAxis * toolLength;

            machPt = oPt;
            return new Vector3d(ao, bo, 0);
        }

        [NotNull]
        public static ToolPoint kFiveAxisABTable([NotNull] ToolPoint tP, Vector3d pivot, double toolLength, Point3d machPt, Vector3d ab)
        {
            Point3d oPt = machPt;
            // translate from the tooltip at machine origin origin to pivot at origin
            oPt = oPt + pivot - Vector3d.ZAxis * toolLength;

            // rotate from machine orientation to material orientation
            oPt.Transform(Transform.Rotation(-ab.X, Vector3d.XAxis, Point3d.Origin));
            oPt.Transform(Transform.Rotation(-ab.Y, Vector3d.YAxis, Point3d.Origin));

            Vector3d dir = Vector3d.ZAxis;
            // rotate from machine orientation to material orientation
            dir.Transform(Transform.Rotation(-ab.X, Vector3d.XAxis, Point3d.Origin));
            dir.Transform(Transform.Rotation(-ab.Y, Vector3d.YAxis, Point3d.Origin));

            ToolPoint outTP = tP.deepClone();
            outTP.pt = oPt;
            outTP.dir = dir;

            return outTP;
        }

        // Interpolate the machine axes linearly between two positions.
        // If both axes have full rotation then there are four ways to do this.
        // If lng is true then reverse the direction on the B axis (for PocketNC)

        [NotNull]
        public static ToolPoint interpolateFiveAxisABTable(Vector3d pivot, double toolLength, [NotNull] ToolPoint from, [NotNull] ToolPoint to, double p, bool lng)
        {
            Vector3d fromAB = ikFiveAxisABTable(from, pivot, toolLength, out Point3d fromMachPt);
            Vector3d toAB = ikFiveAxisABTable(to, pivot, toolLength, out Point3d toMachPt);

            Point3d outPt = (1 - p) * fromMachPt + p * toMachPt;
            Vector3d outAB = (1 - p) * fromAB + p * toAB;
            // switch to long way round or short way round depending on gap between angles
            if ((!lng || !(Math.Abs(fromAB.Y - toAB.Y) <= Math.PI)) && (lng || !(Math.Abs(fromAB.Y - toAB.Y) > Math.PI))
            ) { return kFiveAxisABTable(from, pivot, toolLength, outPt, outAB); }

            Vector3d alt = fromAB.Y > toAB.Y ? new Vector3d(0, 2 * Math.PI, 0) : new Vector3d(0, -2 * Math.PI, 0);
            outAB = (1 - p) * fromAB + p * (toAB + alt);
            return kFiveAxisABTable(from, pivot, toolLength, outPt, outAB);
        }

        public static double angDiffFiveAxisABTable(Vector3d pivot, double toolLength, [NotNull] ToolPoint fP, [NotNull] ToolPoint tP, bool lng)
        {
            Vector3d ang1 = ikFiveAxisABTable(fP, pivot, toolLength, out Point3d _);
            Vector3d ang2 = ikFiveAxisABTable(tP, pivot, toolLength, out Point3d _);

            Vector2d diff = new Vector2d();
            if (lng)
            {
                diff.X = Math.Abs(ang1.X - ang2.X);
                diff.Y = 2 * Math.PI - Math.Abs(ang1.Y - ang2.Y);
            }
            else
            {
                diff.X = Math.Abs(ang1.X - ang2.X);
                diff.Y = Math.Min(Math.Min(Math.Abs(ang1.Y - ang2.Y), Math.Abs(2 * Math.PI + ang1.Y - ang2.Y)), Math.Abs(2 * Math.PI - ang1.Y + ang2.Y));
            }
            return Math.Max(diff.X, diff.Y);
        }

        public static ToolPath angleRefine(IMachine m, ToolPath tP, double maxAngle)
        {
            if (tP.Count < 2) { return tP.deepClone(); }

            ToolPath sRef = tP.deepCloneWithNewPoints(new List<ToolPoint>());
            sRef.Add(tP[0]);

            for(int i=1; i<tP.Count; i++)
            {
                double angCh = m.angDiff(tP[i - 1], tP[i], tP.matTool, false);
                if ( angCh > maxAngle)
                {
                    int newSt = (int)Math.Ceiling(angCh / maxAngle);
                    for (int j = 1; j < newSt; j++)
                    {
                        sRef.Add(m.interpolate(tP[i-1],tP[i], tP.matTool, (double)j/(double)newSt,false));
                    }
                }
                sRef.Add(tP[i]);
            }
            return sRef;
        }
    }

    public static class Utility
    {
        // planeOffset works with self-intersection of a closed curve
        // It looses possible toolpoint information and uses toolDir
        // for all points
        [NotNull]
        public static List<ToolPath> planeOffset([NotNull] ToolPath tP, Vector3d toolDir)
        {
            if (tP.additions.offset.SquareLength < CAMel_Goo.Tolerance) { return new List<ToolPath> {tP}; }
            // if the path is open localOffset will do well enough
            if (!tP.isClosed()) { return localOffset(tP); }
            // Shift curve to XY plane
            Vector3d d = tP.additions.offset;
            double uOS = d.Length;
            Plane p = new Plane(Point3d.Origin, d);

            PolylineCurve uC = tP.getLine();

            uC.Transform(Transform.PlaneToPlane(p, Plane.WorldXY));
            bool reversed = false;

            // ensure the curve is anticlockwise
            if (Math.Abs(uOS) > CAMel_Goo.Tolerance)
            {
                if (uC.ClosedCurveOrientation(Transform.Identity) == CurveOrientation.Clockwise)
                {
                    uC.Reverse();
                    reversed = true;
                    uOS = -uOS;
                }
            }

            // record the average Z location of the curve
            BoundingBox bb = uC.GetBoundingBox(true);
            double useZ = (bb.Max.Z + bb.Min.Z) / 2.0;

            // offSet

            List<PolylineCurve> osC = Offsetting.offset(uC, uOS);

            if (Math.Abs(uOS) > CAMel_Goo.Tolerance && !reversed) { foreach (PolylineCurve osPl in osC) { osPl.Reverse(); } }

            // create Operation

            List<ToolPath> tPs = new List<ToolPath>();

            int i = 1;
            foreach (PolylineCurve osPl in osC)
            {
                // Create and add name, material/tool and material form
                ToolPath osTP = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                osTP.additions.offset = Vector3d.Zero;
                osTP.name = osTP.name + " offset";

                if (osC.Count > 1) { osTP.name = osTP.name + " " + i; }
                i++;

                // return to original orientation

                osPl.Translate(new Vector3d(0, 0, -useZ));
                osPl.Transform(Transform.PlaneToPlane(Plane.WorldXY, p));

                // Add to Operation
                osTP.convertCurve(osPl, toolDir);
                tPs.Add(osTP);
            }
            return tPs;
        }

        [NotNull]
        public static List<ToolPath> localOffset([NotNull] ToolPath tP)
        {
            List<ToolPoint> oTPts = new List<ToolPoint>();
            Vector3d os = tP.additions.offset;
            double osL = os.Length;
            os.Unitize();

            // Check if there is enough to offset
            if (osL < CAMel_Goo.Tolerance || tP.Count < 2 || tP.firstP == null || tP.lastP == null) { return new List<ToolPath> {tP}; }

            // Start with first point unless the ToolPath is closed.
            ToolPoint lPt = tP.firstP, uTPt;
            if (tP.firstP.pt.DistanceTo(tP.lastP.pt) < CAMel_Goo.Tolerance) { lPt = tP[tP.Count - 2]; }
            Vector3d osD;

            for (int i = 0; i < tP.Count - 1; i++)
            {
                uTPt = tP[i].deepClone();
                // offset direction given by tangent and offset Plane
                osD = Vector3d.CrossProduct(os, tP[i + 1].pt - lPt.pt);
                osD.Unitize();
                uTPt.pt = uTPt.pt + osL * osD;
                oTPts.Add(uTPt);
                lPt = tP[i];
            }

            // Loop back to start if closed.
            ToolPoint nP = tP.lastP;
            if (tP.firstP.pt.DistanceTo(tP.lastP.pt) < CAMel_Goo.Tolerance) { nP = tP[2]; }

            uTPt = tP[tP.Count - 1].deepClone();
            osD = Vector3d.CrossProduct(os, nP.pt - lPt.pt);
            osD.Unitize();
            uTPt.pt = uTPt.pt + osL * osD;
            oTPts.Add(uTPt);

            ToolPath oTP = tP.deepCloneWithNewPoints(oTPts);

            oTP.additions.offset = Vector3d.Zero;

            return new List<ToolPath> {oTP};
        }

        // Step down into material
        [NotNull]
        public static List<List<ToolPath>> stepDown([NotNull] ToolPath tP, [NotNull] IMachine m)
        {
            if (tP.matForm == null) { Exceptions.matFormException(); }
            if (tP.matTool == null) { Exceptions.matToolException(); }
            // Give default value for negative DropMiddle
            if (tP.additions.sdDropMiddle < 0) { tP.additions.sdDropMiddle = 8.0 * tP.matForm.safeDistance; }

            IOrderedEnumerable<double> onionSort = tP.additions.sortOnion;

            // Use the material form to work out the distance to cut in the
            // material, the direction to enter the material and the number of passes.
            List<double> matDist = new List<double>();
            List<int> numSteps = new List<int>();
            int maxSteps = 0; // Maximum distance of all points.

            // ask the material form to refine the path
            // (the path is now refined at the start of processing

            ToolPath refPath = tP;

            double finishDepth;
            if (tP.matTool.finishDepth <= 0) { finishDepth = onionSort.First(); }
            else { finishDepth = tP.matTool.finishDepth + onionSort.First(); }

            double cutDepth = tP.matTool.cutDepth <= 0 ? double.PositiveInfinity : tP.matTool.cutDepth;

            foreach (ToolPoint tPt in refPath)
            {
                MFintersection inter = tP.matForm.intersect(tPt, 0).through;
                matDist.Add(inter.lineP); // distance to material surface
                if (matDist[matDist.Count - 1] < 0) { matDist[matDist.Count - 1] = 0; } // avoid negative distances (outside material)

                // calculate maximum number of cutDepth height steps down to finishDepth above material
                numSteps.Add((int) Math.Ceiling((matDist[matDist.Count - 1] - finishDepth) / cutDepth));
                if (numSteps[numSteps.Count - 1] > maxSteps) { maxSteps = numSteps[numSteps.Count - 1]; }
            }

            // make a list of depths to cut at.
            // This just steps down right now, but makes it easier to add fancier leveling, if ever worthwhile.
            // Note that max steps currently assumes only stepping down by cutDepth.

            List<double> cutLevel = new List<double>();
            for (int i = 0; i < maxSteps; i++) { cutLevel.Add((i + 1) * cutDepth); }

            // process the paths, staying away from the final cut

            // need a list for each step down as it might split into more than one path
            // and we need to keep those together to coordinate the Machine Operation
            List<List<ToolPath>> newPaths = new List<List<ToolPath>>();

            for (int i = 0; i < cutLevel.Count; i++)
            {
                newPaths.Add(new List<ToolPath>());
                ToolPath tempTP = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                tempTP.name = tP.name + " Pass " + (i + 1);
                tempTP.additions.stepDown = false;
                tempTP.additions.onion = new List<double> {0};
                tempTP.label = PathLabel.RoughCut;

                bool start = true;
                bool end = false;
                double dropLength = 0; // length of dropped curve in the middle of a path

                for (int j = 0; j < refPath.Count && !end; j++)
                {
                    double height; // height above final path
                    ToolPoint tPt;
                    if (i < numSteps[j]) // We need to cut here
                    {
                        // if this is the first point to cut we need to add the previous one
                        // if there was one, so we do not miss what was between them
                        if (start && j > 0)
                        {
                            tPt = refPath[j - 1].deepClone();
                            height = matDist[j - 1] - cutLevel[i];
                            if (height < finishDepth) { height = finishDepth; }
                            tPt.pt = m.toolDir(tPt) * height + tPt.pt; // stay finishDepth above final path

                            tempTP.Add(tPt);
                        }
                        height = matDist[j] - cutLevel[i];
                        if (height < finishDepth) { height = finishDepth; } // stay finishDepth above final path
                        tPt = refPath[j].deepClone();
                        tPt.pt = m.toolDir(tPt) * height + tPt.pt;
                        tempTP.Add(tPt);
                        start = false;
                        dropLength = 0;
                    }
                    else if (start) // We have not hit any cutting yet;
                    {
                        if (tP.additions.sdDropStart) { continue; }
                        tPt = refPath[j].deepClone();
                        height = finishDepth;
                        if (height > matDist[j]) { height = 0; }
                        tPt.pt = m.toolDir(tPt) * height + tPt.pt;
                        tempTP.Add(tPt);
                    }
                    else // We need to look ahead
                    {
                        int k;
                        for (k = j; k < refPath.Count && i >= numSteps[k]; k++) { } // Look ahead to the next cut

                        if (k == refPath.Count) // No more cutting required
                        {
                            if (tP.additions.sdDropEnd) // we are dropping the end
                            {
                                // Add point as the previous one was deep,
                                // then set end to true so we finish
                                tPt = refPath[j].deepClone();
                                height = finishDepth;
                                tPt.pt = m.toolDir(tPt) * height + tPt.pt;
                                tempTP.Add(tPt);
                                end = true;
                            }
                            else // add point
                            {
                                tPt = refPath[j].deepClone();
                                height = finishDepth;
                                tPt.pt = m.toolDir(tPt) * height + tPt.pt;
                                tempTP.Add(tPt);
                            }
                        }
                        else // into the middle
                        {
                            if (tP.additions.sdDropMiddle < 0 || k - j < 3) // we are not dropping middle or there are not enough points to justify it
                            {
                                tPt = refPath[j].deepClone();
                                height = finishDepth;
                                tPt.pt = m.toolDir(tPt) * height + tPt.pt;
                                tempTP.Add(tPt);
                            }
                            else //check length of drop
                            {
                                if (Math.Abs(dropLength) < CAMel_Goo.Tolerance) // If we are at the start of a possible drop Add the length until we hit the end or go over
                                {
                                    int l;
                                    for (l = j; dropLength < tP.additions.sdDropMiddle && l < k; l++)
                                    { dropLength += refPath[l].pt.DistanceTo(refPath[l + 1].pt); }
                                }
                                if (dropLength > tP.additions.sdDropMiddle)
                                {
                                    // add point, as previous point was in material
                                    tPt = refPath[j].deepClone();
                                    height = finishDepth;
                                    tPt.pt = m.toolDir(tPt) * height + tPt.pt;
                                    tempTP.Add(tPt);
                                    // leap forward cut path and start a new one
                                    // giving settings to add inserts and retracts

                                    tempTP.additions.retract = true;
                                    newPaths[newPaths.Count - 1]?.Add(tempTP); // add path and create a new one

                                    tempTP = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                                    tempTP.name = tP.name + " Continuing Pass " + (i + 1);
                                    tempTP.additions.insert = true;
                                    tempTP.additions.stepDown = false;
                                    tempTP.additions.onion = new List<double> {0};
                                    tempTP.label = PathLabel.RoughCut;

                                    // add k-1 point as k is deep
                                    // this will not result in a double point as we checked (k-j) >=3
                                    tPt = refPath[k - 1].deepClone();
                                    height = finishDepth;
                                    tPt.pt = m.toolDir(tPt) * height + tPt.pt;
                                    tempTP.Add(tPt);
                                    j = k - 1; //set j to k-1 so it deals with the k point next
                                }
                                else // after all that we still need to add the point
                                {
                                    tPt = refPath[j].deepClone();
                                    height = finishDepth;
                                    tPt.pt = m.toolDir(tPt) * height + tPt.pt;
                                    tempTP.Add(tPt);
                                }
                            }
                        }
                    }
                }
                newPaths[newPaths.Count - 1]?.Add(tempTP);
            }
            return newPaths;
        }

        [CanBeNull]
        // ReSharper disable once SuggestBaseTypeForParameter
        private static PolylineCurve findLead([NotNull] PolylineCurve toolL, double leadCurve, double insertWidth, int v, bool start)
        {
            // work out the rotation to get the desired normal
            double normAng = Math.PI / 2.0;
            // take into account the orientation of the path
            //if (toolL.ClosedCurveOrientation(Vector3d.ZAxis) == CurveOrientation.CounterClockwise) { normAng = -normAng; }
            // now we have the internal normal, flip if we want external.
            if (leadCurve >= 0) { normAng = -normAng; }

            PointContainment incorrectSide = PointContainment.Inside;
            CurveOrientation orient = toolL.ClosedCurveOrientation(-Vector3d.ZAxis);

            if ((orient == CurveOrientation.Clockwise && leadCurve > 0) || (orient == CurveOrientation.CounterClockwise && leadCurve < 0))
            { incorrectSide = PointContainment.Outside; }

            double uLeadCurve = Math.Abs(leadCurve);

            Point3d startPt = toolL.PointAtStart;
            // Get tangents and the Normal pointing in the direction we want the lead.
            Vector3d startTan = toolL.TangentAtStart;
            Vector3d startNorm = startTan;
            startNorm.Rotate(normAng, Vector3d.ZAxis);

            Vector3d endTan = toolL.TangentAtEnd;
            Vector3d endNorm = endTan;
            endNorm.Rotate(normAng, Vector3d.ZAxis);

            Vector3d uTan, uNorm;

            if (start)
            {
                uTan = -endTan;
                uNorm = endNorm;
            }
            else
            {
                uTan = startTan;
                uNorm = startNorm;
            }
            // Start by using the end version, we will choose the start version

            ArcCurve leadCirc = new ArcCurve(new Arc(startPt, uTan, startPt + uLeadCurve * (uNorm + uTan)));

            // step along the arc trying to find a point more that insert distance from the path
            List<double> divs = leadCirc.DivideByCount(v, true)?.ToList() ?? new List<double>();

            Polyline outP = new Polyline();

            foreach (double d in divs)
            {
                Point3d testPt = leadCirc.PointAt(d);
                outP.Add(testPt);
                if (toolL.Contains(testPt) == incorrectSide) { return null; }
                toolL.ClosestPoint(testPt, out double testDist);
                testDist = testPt.DistanceTo(toolL.PointAt(testDist));
                if (testDist > insertWidth * 0.52) { return new PolylineCurve(outP); }
            }

            // Now try to keep going straight

            for (double i = insertWidth / 10.0; i < 2 * insertWidth; i = i + insertWidth / 10.0)
            {
                Point3d testPt = leadCirc.PointAtEnd + uNorm * i;
                outP.Add(testPt);
                if (toolL.Contains(testPt) == incorrectSide) { return null; }

                toolL.ClosestPoint(testPt, out double testDist);
                testDist = testPt.DistanceTo(toolL.PointAt(testDist));
                if (testDist > insertWidth * 0.52) { return new PolylineCurve(outP); }
            }
            return null;
        }

        [NotNull]
        public static List<ToolPath> leadInOutU([NotNull] ToolPath tP, [NotNull] string activate = "", [NotNull] string deActivate = "", int irActivate = 0)
        {
            // Will add insert and retract paths as new ToolPaths
            List<ToolPath> irTps = new List<ToolPath>();

            // Update the main toolpath
            ToolPath newTP = tP.deepClone();
            if (tP.matTool == null) { Exceptions.matToolException(); }
            newTP.additions.insert = false;
            newTP.additions.retract = false;
            newTP.additions.leadCurvature = 0;

            // Add activation code to main path if tool not activated for insert/retract
            if (tP.additions.activate != 0 && irActivate == 0)
            {
                if (activate != string.Empty) { newTP.preCode = activate + "\n" + newTP.preCode; }
                if (deActivate != string.Empty) { newTP.postCode = newTP.postCode + "\n" + deActivate; }
            }

            irTps.Add(newTP);

            double leadCurve = tP.additions.leadCurvature;

            // If leadCurve == 0 or path is open can now return
            if (Math.Abs(leadCurve) < CAMel_Goo.Tolerance || !tP.isClosed()) { return irTps; }

            PolylineCurve toolL = tP.getLine();

            if (tP.additions.insert)
            {
                PolylineCurve leadIn = findLead(toolL, leadCurve, tP.matTool.insertWidth, 15, true);
                // If no suitable curve found throw an error
                if (leadIn == null) { newTP.firstP?.addWarning("No suitable curve for lead in found."); }
                else
                {
                    leadIn.Reverse();
                    List<ToolPoint> tPts = new List<ToolPoint>();
                    if (tP.firstP == null) { Exceptions.emptyPathException(); }
                    for (int i = 1; i < leadIn.PointCount; i++)
                    {
                        ToolPoint tPt = tP.firstP.deepClone();
                        tPt.pt = leadIn.Point(i);
                        tPts.Add(tPt);
                    }

                    ToolPath iTp = newTP.deepCloneWithNewPoints(new List<ToolPoint>());
                    iTp.name = iTp.name + " insert";
                    iTp.label = PathLabel.Insert;
                    if (tP.additions.activate != 0) { iTp.additions.activate = irActivate; }
                    iTp.AddRange(tPts);

                    if (iTp.Count > 0) { irTps.Insert(0, iTp); }
                }
            }

            if (tP.additions.retract)
            {
                PolylineCurve leadOut = findLead(toolL, leadCurve, tP.matTool.insertWidth, 15, false);
                if (leadOut == null) { newTP.lastP?.addWarning("No suitable curve for lead out found."); }
                // If no suitable curve found throw an error
                else
                {
                    List<ToolPoint> tPts = new List<ToolPoint>();
                    if (tP.firstP == null) { Exceptions.emptyPathException(); }
                    for (int i = 1; i < leadOut.PointCount; i++)
                    {
                        ToolPoint tPt = tP.firstP.deepClone();
                        tPt.pt = leadOut.Point(i);
                        tPts.Add(tPt);
                    }

                    ToolPath rTp = newTP.deepCloneWithNewPoints(new List<ToolPoint>());
                    rTp.name = rTp.name + " retract";
                    rTp.label = PathLabel.Retract;
                    if (tP.additions.activate != 0) { rTp.additions.activate = irActivate; }
                    rTp.AddRange(tPts);

                    if (rTp.Count > 0) { irTps.Add(rTp); }
                }
            }
            
            // Add activation codes
            if (tP.additions.activate != 0)
            {
                if (activate != string.Empty) { irTps[0].preCode = activate + "\n" + newTP.preCode; }
                if (deActivate != string.Empty) { irTps[irTps.Count - 1].postCode = newTP.postCode + "\n" + deActivate; }
            }

            return irTps;
        }
        [NotNull]
        public static List<ToolPath> leadInOutV([NotNull] ToolPath tP, [NotNull] string activate = "", [NotNull] string deActivate = "", int irActivate = 0)
        {
            // Will add insert and retract paths as new ToolPaths
            List<ToolPath> irTps = new List<ToolPath>();

            // Update the main toolpath
            ToolPath newTP = tP.deepClone();
            if (tP.matTool == null) { Exceptions.matToolException(); }
            newTP.additions.insert = false;
            newTP.additions.retract = false;
            newTP.additions.leadCurvature = 0;

            // Add activation code to main path if tool not activated for insert/retract
            if (tP.additions.activate != 0 && irActivate == 0)
            {
                if (activate != string.Empty) { newTP.preCode = activate + "\n" + newTP.preCode; }
                if (deActivate != string.Empty) { newTP.postCode = newTP.postCode + "\n" + deActivate; }
            }

            irTps.Add(newTP);

            double leadCurve = tP.additions.leadCurvature;

            // If leadCurve == 0 can now return
            if (Math.Abs(leadCurve) < CAMel_Goo.Tolerance) { return irTps; }

            PolylineCurve toolL = tP.getLine();
            const double wiggle = 2.5 * Math.PI / 180.0; // Angles from orthogonal in radians
            if (tP.additions.insert)
            {
                ToolPath iTp = newTP.deepCloneWithNewPoints(new List<ToolPoint>());
                iTp.name = iTp.name + " insert";
                iTp.label = PathLabel.Insert;
                if (tP.additions.activate != 0) { iTp.additions.activate = irActivate; }

                double r = Math.PI / 2.0 + wiggle;
                if (tP.additions.offset * -Vector3d.ZAxis < 0) { r = -Math.PI / 2.0 - wiggle; } // cut to the right
                Vector3d tan = toolL.TangentAtStart;
                tan.Rotate(r, Vector3d.ZAxis);
                if (tP.firstP == null) { Exceptions.emptyPathException(); }
                ToolPoint tPt = tP.firstP.deepClone();
                tPt.pt = tPt.pt + tan * tP.matTool.insertWidth;

                iTp.Add(tPt);
                //tPt = tP.firstP.deepClone();
                //iTp.Add(tPt);

                irTps.Insert(0, iTp);
            }

            if (tP.additions.retract)
            {
                ToolPath rTp = newTP.deepCloneWithNewPoints(new List<ToolPoint>());
                rTp.name = rTp.name + " retract";
                rTp.label = PathLabel.Retract;

                if (tP.additions.activate != 0) { rTp.additions.activate = irActivate; }

                double r = Math.PI / 2.0 - wiggle;
                if (tP.additions.offset * -Vector3d.ZAxis < 0) { r = -Math.PI / 2.0 + wiggle; } // cut to the right
                Vector3d tan = toolL.TangentAtEnd;
                tan.Rotate(r, Vector3d.ZAxis);
                if (tP.lastP == null) { Exceptions.emptyPathException(); }
                ToolPoint tPt = tP.lastP.deepClone();
                rTp.Add(tPt);
                tPt = tP.lastP.deepClone();
                tPt.pt = tPt.pt + tan * tP.matTool.insertWidth;

                rTp.Add(tPt);
                // Remove last point of toolpath to avoid point repeat

                irTps[irTps.Count - 1]?.removeLast();

                irTps.Add(rTp);
            }
            
            // Add activation codes
            if (tP.additions.activate != 0)
            {
                if (activate != string.Empty) { irTps[0].preCode = activate + "\n" + newTP.preCode; }
                if (deActivate != string.Empty) { irTps[irTps.Count - 1].postCode = newTP.postCode + "\n" + deActivate; }
            }

            return irTps;
        }

        [NotNull]
        internal static List<ToolPath> insertRetract([NotNull] ToolPath tP, [NotNull] string activate = "", [NotNull] string deActivate = "", int irActivate = 1)
        {
            // Will add insert and retract paths as new ToolPaths
            List<ToolPath> irTps = new List<ToolPath>();

            // Update the main toolpath
            ToolPath newTP = tP.deepClone();
            if (tP.matTool == null) { Exceptions.matToolException(); }
            if (tP.matForm == null) { Exceptions.matFormException(); }
            newTP.additions.insert = false;
            newTP.additions.retract = false;

            MFintersection inter;

            double uTol = tP.matForm.safeDistance * 1.05;
            ToolPoint tempTPt;

            // Add activation code to main path if tool not activated for insert/retract
            if (tP.additions.activate != 0 && irActivate == 0)
            {
                if (activate != string.Empty) { newTP.preCode = activate + "\n" + newTP.preCode; }
                if (deActivate != string.Empty) { newTP.postCode = newTP.postCode + "\n" + deActivate; }
            }

            irTps.Add(newTP);

            // check if we have something to do
            if (tP.additions.insert && newTP.Count > 0) // add insert
            {
                ToolPath iTp = newTP.deepCloneWithNewPoints(new List<ToolPoint>());
                iTp.name = iTp.name + " insert";
                iTp.label = PathLabel.Insert;

                //note we do this backwards adding points to the start of the path.

                if (tP.additions.activate != 0) { iTp.additions.activate = irActivate; }
                if (irActivate != 0 && activate != String.Empty) { iTp.preCode = activate + "\n" + newTP.preCode; }

                // get distance to surface and insert direction
                if (newTP.firstP == null) { Exceptions.emptyPathException(); }
                inter = tP.matForm.intersect(newTP.firstP, 0).through;

                // check to see if there was an intersection
                if (inter.isSet)
                {
                    // point on material surface

                    tempTPt = newTP.firstP.deepClone();
                    tempTPt.pt = inter.point;
                    tempTPt.feed = tP.matTool.feedPlunge;
                    iTp.Insert(0, tempTPt);

                    // point out at safe distance
                    if (iTp.firstP == null) { Exceptions.emptyPathException(); }
                    tempTPt = iTp.firstP.deepClone();
                    tempTPt.pt = tempTPt.pt + inter.away * uTol;
                    tempTPt.feed = 0; // we can use a rapid move
                    iTp.Insert(0, tempTPt);
                }
                else
                {
                    // check intersection with material extended to safe distance
                    inter = tP.matForm.intersect(newTP.firstP, uTol).through;
                    if (inter.isSet)
                    {
                        // point out at safe distance
                        tempTPt = newTP.firstP.deepClone();
                        tempTPt.pt = inter.point;
                        tempTPt.feed = 0; // we can use a rapid move
                        iTp.Insert(0, tempTPt);
                    } //  otherwise nothing needs to be added as we do not interact with material
                }

                if (iTp.Count > 0) { irTps.Insert(0, iTp); }
            }

            if (!tP.additions.retract || newTP.Count <= 0) { return irTps; }
            if (newTP.lastP == null) { Exceptions.emptyPathException(); }

            ToolPath rTp = newTP.deepCloneWithNewPoints(new List<ToolPoint>());
            rTp.name = rTp.name + " retract";
            rTp.label = PathLabel.Retract;

            //note we do this backwards adding points to the start of the path.

            if (tP.additions.activate != 0) { rTp.additions.activate = irActivate; }
            if (irActivate != 0 && activate != String.Empty) { rTp.preCode = activate + "\n" + newTP.preCode; }

            // get distance to surface and retract direction
            inter = tP.matForm.intersect(newTP.lastP, 0).through;
            if (inter.isSet)
            {
                // Replace last point of toolpath
                tempTPt = newTP.lastP.deepClone();
                newTP.removeLast();

                // set speed to the plunge feed rate.
                tempTPt.feed = tP.matTool.feedPlunge;

                rTp.Add(tempTPt);

                tempTPt = tempTPt.deepClone();

                // Pull back to surface
                tempTPt.pt = inter.point;

                rTp.Add(tempTPt);

                // Pull away to safe distance

                if (rTp.lastP == null) { Exceptions.emptyPathException(); }

                tempTPt = rTp.lastP.deepClone();
                tempTPt.pt = tempTPt.pt + inter.away * uTol;
                tempTPt.feed = 0; // we can use a rapid move
                rTp.Add(tempTPt);
            }
            else
            {
                // check intersection with material extended to safe distance
                inter = tP.matForm.intersect(newTP.lastP, uTol).through;
                if (!inter.isSet) { return irTps; }

                // Replace last point of toolpath
                tempTPt = newTP.lastP.deepClone();
                newTP.removeLast();

                // set speed to the plunge feed rate.
                tempTPt.feed = tP.matTool.feedPlunge;

                rTp.Add(tempTPt);

                // point out at safe distance
                tempTPt = newTP.lastP.deepClone();
                tempTPt.pt = inter.point;
                tempTPt.feed = 0; // we can use a rapid move
                rTp.Add(tempTPt);
            }

            if (rTp.Count > 0) { irTps.Add(rTp); }

            return irTps;
        }

        // Adjust the path so it will not be gouged when cut in 3-axis, or indexed 3-axis mode.
        // TODO make this guarantee that it does not gouge locally. There is a problem
        // with paths that are steep down, followed by some bottom moves followed by steep out.
        [NotNull]
        public static ToolPath threeAxisHeightOffset([NotNull] ToolPath tP, [NotNull] IMachine m)
        {
            if (tP.matTool == null) { Exceptions.matToolException(); }
            List<ToolPoint> offsetPath = new List<ToolPoint>();

            if (tP.Count < 2) { return tP; }

            Vector3d travel = tP[1].pt - tP[0].pt;
            travel.Unitize();

            Vector3d orth = Vector3d.CrossProduct(travel, m.toolDir(tP[0]));
            Vector3d uOrth = orth;

            ToolPoint point = tP.matTool.threeAxisHeightOffset(m, tP[0], travel, uOrth);

            List<Line> osLines = new List<Line> {new Line(point.pt, travel)};

            bool changeDirection = false; // Has tool direction changed?

            offsetPath.Add(point);

            // loop through the lines of the toolpath finding their offset path
            // and then traveling to the closest point to the next offset path

            for (int i = 1; i < tP.Count - 1; i++)
            {
                // Keep track of tool point direction to warn if it changes (but only once)
                if (m.toolDir(tP[i - 1]) != m.toolDir(tP[i]) && !changeDirection)
                {
                    tP[i].addWarning("Height offsetting is not designed for 5-Axis motion, it may be unpredictable.");
                    changeDirection = true;
                }
                // Find the next offset line
                travel = tP[i + 1].pt - tP[i].pt;
                orth = Vector3d.CrossProduct(travel, m.toolDir(tP[0]));
                if (Math.Abs(orth.Length) > CAMel_Goo.Tolerance) { uOrth = orth; }

                ToolPoint nextPoint = tP.matTool.threeAxisHeightOffset(m, tP[i], travel, uOrth);

                // find the next line we will travel along
                osLines.Add(new Line(nextPoint.pt, travel));

                // we need to find the last path that does not reverse when we travel along our new line.
                // if we go in the wrong direction on an offset path then we are gouging back into previously cut material.
                // In the following we discuss intersection, for lines in 3d this is given by the closest point for two lines.

                // intersect the new line with the last line we used
                Rhino.Geometry.Intersect.Intersection.LineLine(osLines[osLines.Count - 2], osLines[osLines.Count - 1], out double inter, out double nextInter);
                // find the orientation of the new path
                ToolPoint osP = offsetPath[offsetPath.Count - 1];
                if (osP == null) { Exceptions.nullPanic(); }
                double orient = (osLines[osLines.Count - 2].PointAt(inter) - osP.pt) * osLines[osLines.Count - 2].UnitTangent;

                // loop until we find a suitable line, removing previous points that are now problematic
                // checking the length of offsetPath should ensure we don't try to go past the start
                // and osLines is always at least 2 long, but we check both just in case.
                while (orient < 0 && offsetPath.Count > 1 && osLines.Count > 1)
                {
                    // remove the reversing line
                    osLines.RemoveAt(osLines.Count - 2);
                    // remove the last point on the offsetPath, which were given by the intersection we are removing
                    offsetPath.RemoveRange(offsetPath.Count - 1, 1);
                    // find the new intersection and orientation
                    Rhino.Geometry.Intersect.Intersection.LineLine(osLines[osLines.Count - 2], osLines[osLines.Count - 1], out inter, out nextInter);
                    osP = offsetPath[offsetPath.Count - 1];
                    if (osP == null) { Exceptions.nullPanic(); }
                    orient = (osLines[osLines.Count - 2].PointAt(inter) - osP.pt) * osLines[osLines.Count - 2].UnitTangent;
                }

                // if we got to the start and things are still bad we have to deal with things differently
                ToolPoint startCp;
                if (orient < 0)
                {
                    // remove the old start point and add the closest point on the new first line
                    offsetPath.RemoveAt(0);

                    // intersect our new line with the first direction
                    Rhino.Geometry.Intersect.Intersection.LineLine(osLines[0], osLines[osLines.Count - 1], out inter, out nextInter);

                    // note that we keep the information from the toolpoint before the line we are going to be cutting
                    // tP might be removed on later passes, if the line is removed.
                    startCp = tP[i].deepClone();
                    startCp.pt = osLines[osLines.Count - 1].PointAt(nextInter);
                    offsetPath.Add(startCp);
                }
                else
                {
                    // Add the new intersection we like using the closest points on the two lines (the points on each line closest to the other line)
                    // note that we keep the information from the toolpoint before the line we are going to be cutting
                    // this might be removed on later passes, if the line is removed.
                    ToolPoint endCp = tP[i].deepClone();
                    endCp.pt = osLines[osLines.Count - 2].PointAt(inter);
                    startCp = tP[i].deepClone();
                    startCp.pt = osLines[osLines.Count - 1].PointAt(nextInter);

                    // take the midpoint of the two intersections
                    // there is possibly something clever to do here
                    startCp.pt = (startCp.pt + endCp.pt) / 2;

                    offsetPath.Add(startCp);
                    //offsetPath.Add(endCP);
                }
            }

            // add the final point.

            if (tP.lastP == null) { Exceptions.emptyPathException(); }
            orth = Vector3d.CrossProduct(travel, m.toolDir(tP.lastP));
            if (Math.Abs(orth.Length) > CAMel_Goo.Tolerance) { uOrth = orth; }
            offsetPath.Add(tP.matTool.threeAxisHeightOffset(m, tP.lastP, travel, uOrth));

            ToolPath retPath = tP.deepCloneWithNewPoints(offsetPath);
            retPath.additions.threeAxisHeightOffset = false;

            if (!retPath.additions.insert)
            { retPath.firstP?.addWarning("Height Offsetting does not work between ToolPaths. This might cause unexpected behaviour."); }
            if (!retPath.additions.retract)
            { retPath.lastP?.addWarning("Height Offsetting does not work between ToolPaths. This might cause unexpected behaviour."); }

            return retPath;
        }

        // Clear the addition but don't do anything
        [NotNull]
        public static ToolPath clearThreeAxisHeightOffset([NotNull] ToolPath tP)
        {
            ToolPath newTP = tP.deepClone();
            newTP.additions.threeAxisHeightOffset = false;
            return newTP;
        }

        // Add a finishing versions of the path (including onion Skinning)
        [NotNull]
        public static List<ToolPath> finishPaths([NotNull] ToolPath tP, [NotNull] IMachine m)
        {
            List<ToolPath> fP = new List<ToolPath>();
            // get the sorted list of onion cuts
            IOrderedEnumerable<double> onionSort = tP.additions.sortOnion;

            foreach (double height in onionSort)
            {
                ToolPath newTP = tP.deepClone(height, m);
                if (newTP.name != string.Empty) { newTP.name = newTP.name + " "; }
                newTP.name = newTP.name + "Finish at height " + height.ToString("0.###");
                newTP.label = PathLabel.FinishCut;
                newTP.additions.stepDown = false;
                newTP.additions.onion = new List<double> {0};
                fP.Add(newTP);
            }

            // If onion is empty add the ToolPath anyway.
            if (fP.Count != 0) { return fP; }
            {
                ToolPath newTP = tP.deepClone();
                newTP.additions.stepDown = false;
                newTP.additions.onion = new List<double> {0};
                newTP.label = PathLabel.FinishCut;
                fP.Add(newTP);
            }
            return fP;
        }

        // The finish path is just the toolpath
        [NotNull]
        internal static List<ToolPath> oneFinishPath([NotNull] ToolPath tP)
        {
            ToolPath newTP = tP.deepClone();
            newTP.additions.stepDown = false;
            newTP.additions.onion = new List<double> {0};
            newTP.label = PathLabel.FinishCut;
            return new List<ToolPath> {newTP};
        }

        // Check for jumps in material, return
        // 0 if not in material
        // positive if in material
        // -1 if one of the paths has 0 points
        internal static double jumpCheck([NotNull] IMachine m, [NotNull] ToolPath fP, [NotNull] ToolPath tP)
        {
            if (fP.matForm == null || tP.matForm == null) { Exceptions.matFormException(); }
            if (fP.matTool == null) { Exceptions.matToolException(); }
            if (fP.lastP == null || tP.firstP == null) { Exceptions.emptyPathException(); }

            // check there is anything to transition from or to
            if (fP.Count <= 0 || tP.Count <= 0) { return -1; }

            // See if we lie in the material
            // Check end of this path and start of TP
            // For each see if it is safe in one Material Form
            // As we pull back to safe distance we allow a little wiggle.
            if (fP.matForm.intersect(fP.lastP, fP.matForm.safeDistance).thrDist > 0.0001
                && tP.matForm.intersect(fP.lastP, tP.matForm.safeDistance).thrDist > 0.0001 ||
                fP.matForm.intersect(tP.firstP, fP.matForm.safeDistance).thrDist > 0.0001
                && tP.matForm.intersect(tP.firstP, tP.matForm.safeDistance).thrDist > 0.0001)
            {
                // We trust insert and retract moves and retract to transitions.

                if (fP.label == PathLabel.Insert
                    || tP.label == PathLabel.Retract
                    || fP.label == PathLabel.Retract && tP.label == PathLabel.Transition)
                { return 0; }

                // return distance in material
                double length = fP.lastP.pt.DistanceTo(tP.firstP.pt);

                return length;
            }
            return 0;
        }

        // Check travel between toolpaths
        internal static void jumpCheck(ref CodeInfo co, [NotNull] IMachine m, [NotNull] ToolPath fP,
            [NotNull] ToolPath tP)
        {
            // check if there is a problem moving between paths
            double length = jumpCheck(m, fP, tP);
            if (length > m.pathJump)
            {
                co.addError("Long Transition between paths in material. \n"
                            + "To remove this error, don't use ignore, instead change PathJump for the machine from: "
                            + m.pathJump + " to at least: " + length);
            }
        }

        // Assume all moves are fine
        internal static void noCheck(ref CodeInfo co, [NotNull] IMachine m, [NotNull] ToolPath fP, [NotNull] ToolPath tP) { }

        public static bool noTransitionPosDir([NotNull] ToolPath fP, [NotNull] ToolPath tP)
        {
            if (fP.lastP == null || tP.firstP == null) { return false; }
            if (fP.lastP.pt == tP.firstP.pt && fP.lastP.dir == tP.firstP.dir) { return true; }
            if (fP.label == PathLabel.Insert || tP.label == PathLabel.Retract) { return true; }
            return false;
        }
        public static bool noTransitionPos([NotNull] ToolPath fP, [NotNull] ToolPath tP)
        {
            if (fP.lastP == null || tP.firstP == null) { return false; }
            if (fP.lastP.pt == tP.firstP.pt) { return true; }
            if (fP.label == PathLabel.Insert || tP.label == PathLabel.Retract) { return true; }
            return false;
        }
    }

    public static class GCode
    {
        // Standard terms

        [NotNull] internal const string DefaultCommentStart = "(";
        [NotNull] internal const string DefaultCommentEnd = ")";
        [NotNull] internal const string DefaultSectionBreak = "------------------------------------------";
        [NotNull] internal const string DefaultSpeedChangeCommand = "M03";
        [NotNull] internal const string DefaultToolChangeCommand = "G43H#";
        [NotNull] internal const string DefaultActivateCommand = "M61";
        [NotNull] internal const string DefaultDeActivateCommand = "M62";
        [NotNull] internal const string DefaultFileStart = "";
        [NotNull] internal const string DefaultFileEnd = "";
        [NotNull] internal const string DefaultExtension = "nc";

        // Formatting structure for GCode

        [NotNull]
        public static string gcLineNumber([NotNull] string l, int line) => "N" + line.ToString("0000") + "0 " + l;

        public static void gcInstStart([NotNull] IGCodeMachine m, [NotNull] ref CodeInfo co, [NotNull] MachineInstruction mI)
        {
            if (mI[0].Count == 0) { Exceptions.noToolPathException(); }
            if (mI[0][0].matTool == null) { Exceptions.matToolException(); }
            if (mI[0][0].matForm == null) { Exceptions.matFormException(); }

            co.currentMT = mI[0][0].matTool;
            co.currentMF = mI[0][0].matForm;

            DateTime thisDay = DateTime.Now;
            co.appendLineNoNum(m.fileStart);
            co.appendComment(m.sectionBreak);
            if (mI.name != string.Empty) { co.appendComment(mI.name); }
            co.appendComment("");
            co.appendComment(" Machine Instructions Created " + thisDay.ToString("f"));
            co.appendComment("  by " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            if (m.name != string.Empty) { co.appendComment("  for " + m.name); }
            co.appendComment(" Starting with: ");
            co.appendComment("  Tool: " + mI[0][0].matTool.toolName);
            co.appendComment("  in " + mI[0][0].matTool.matName + " with shape " + mI[0][0].matForm.ToString());
            co.appendComment("");
            co.appendComment(m.sectionBreak);
            co.append(m.header);
            co.append(mI.preCode);
            co.currentMT = MaterialTool.Empty; // Clear the tool information so we call a tool change.
        }
        public static void gcInstEnd([NotNull] IGCodeMachine m, [NotNull] ref CodeInfo co, [NotNull] MachineInstruction mI)
        {
            co.appendComment(m.sectionBreak);
            co.appendComment(" End of ToolPaths");
            co.appendComment(m.sectionBreak);

            co.append(mI.postCode);
            co.append(m.footer);
            co.appendLineNoNum(m.fileEnd);
        }

        public static void gcOpStart([NotNull] IGCodeMachine m, [NotNull] ref CodeInfo co, [NotNull] MachineOperation mO)
        {
            co.appendComment(m.sectionBreak);
            co.appendComment("");
            co.appendComment(" Operation: " + mO.name);
            co.appendComment("");
            co.append(mO.preCode);
        }

        // ReSharper disable once UnusedParameter.Global
        public static void gcOpEnd([NotNull] IGCodeMachine m, [NotNull] ref CodeInfo co, [NotNull] MachineOperation mO)
            => co.append(mO.postCode);

        public static void gcPathStart([NotNull] IGCodeMachine m, [NotNull] ref CodeInfo co, [NotNull] ToolPath tP)
        {
            if (tP.matTool == null) { Exceptions.matToolException(); }
            if (tP.matForm == null) { Exceptions.matFormException(); }
            co.appendComment(m.sectionBreak);
            bool preamble = false;
            if (tP.name != string.Empty)
            {
                co.appendComment(" ToolPath: " + tP.name);
                preamble = true;
            }
            if (tP.matTool != null && tP.matTool.toolName != co.currentMT.toolName)
            {
                co.appendComment(" using: " + tP.matTool.toolName + " into " + tP.matTool.matName);
                if (tP.matTool.toolNumber != co.currentMT.toolNumber) {
                    m.toolChange(ref co, tP.matTool.toolNumber);
                }
                co.currentMT = tP.matTool;
                preamble = true;
            }
            if (tP.matForm != null && tP.matForm.ToString() != co.currentMF.ToString())
            {
                co.appendComment(" material: " + tP.matForm.ToString());
                co.currentMF = tP.matForm;
                preamble = true;
            }

            if (preamble) { co.appendComment(m.sectionBreak); }

            co.append(tP.preCode);
        }
        // ReSharper disable once UnusedParameter.Global
        public static void gcPathEnd([NotNull] IGCodeMachine m, [NotNull] ref CodeInfo co, [NotNull] ToolPath tP) => co.append(tP.postCode);

        // Toolpoint writers
        // These might be simpler to pull
        // into a single "write" command taking a dictionary?
        [NotNull]
        public static string gcTwoAxis([NotNull] ToolPoint tP)
        {
            Point3d op = tP.pt;
            string gPoint = string.Empty;
            gPoint += "X" + op.X.ToString("0.000") + " Y" + op.Y.ToString("0.000");

            return gPoint;
        }
        [NotNull]
        public static string gcThreeAxis([NotNull] ToolPoint tP)
        {
            Point3d op = tP.pt;
            string gPoint = string.Empty;
            gPoint += "X" + op.X.ToString("0.000") + " Y" + op.Y.ToString("0.000") + " Z" + op.Z.ToString("0.000");

            return gPoint;
        }
        [NotNull]
        public static string gcFiveAxisAB(Point3d machPt, Vector3d ab)
        {
            StringBuilder gPtBd = new StringBuilder(@"X", 34);
            gPtBd.Append(machPt.X.ToString("0.000"));
            gPtBd.Append(@" Y" + machPt.Y.ToString("0.000"));
            gPtBd.Append(@" Z" + machPt.Z.ToString("0.000"));
            gPtBd.Append(@" A" + (180.0 * ab.X / Math.PI).ToString("0.000"));
            gPtBd.Append(@" B" + (180.0 * ab.Y / Math.PI).ToString("0.000"));

            return gPtBd.ToString();
        }

        // GCode reading
        [NotNull] private static readonly Regex _NumbPattern = new Regex(@"^([0-9\-.]+)", RegexOptions.Compiled);

        private static double getValue([NotNull] string line, char split, double old, ref bool changed)
        {
            double val = old;
            string[] splitLine = line.Split(split);
            if (splitLine.Length < 2) { return val; }

            // ReSharper disable once AssignNullToNotNullAttribute
            Match monkey = _NumbPattern.Match(splitLine[1]);
            if (monkey.Success)
            { val = double.Parse(monkey.Value); }
            if (Math.Abs(val - old) > CAMel_Goo.Tolerance) { changed = true; }
            return val;
        }
        // TODO detect tool changes and new paths
        [NotNull]
        public static MachineInstruction gcRead([NotNull] IGCodeMachine m, [NotNull, ItemNotNull] List<MaterialTool> mTs, [NotNull] string code, [NotNull] List<char> terms)
        {
            ToolPath tP = new ToolPath();
            Dictionary<char, double> values = new Dictionary<char, double>();

            foreach (char c in terms) { values.Add(c, 0); }

            using (StringReader reader = new StringReader(code))
            {
                // Loop over the lines in the string.
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    bool changed = false;
                    foreach (char t in terms)
                    { values[t] = getValue(line, t, values[t], ref changed); }
                    //interpret a G0 command.
                    if (line.Contains(@"G00") || line.Contains(@"G0 "))
                    {
                        if (values.ContainsKey('F') && Math.Abs(values['F']) > CAMel_Goo.Tolerance)
                        {
                            changed = true;
                            values['F'] = 0;
                        }
                    }
                    MaterialTool uMT = MaterialTool.Empty;
                    if (mTs.Count > 0) { uMT = mTs[0]; }
                    if (changed) { tP.Add(m.readTP(values, uMT)); }
                }
            }
            return new MachineInstruction(m) {new MachineOperation(tP)};
        }

        [NotNull]
        public static string comment([NotNull] IGCodeMachine m, [NotNull] string l)
        {
            if (l == "" || l == " ") { return " "; }

            string uL = l;
            // Avoid "nested comments"
            if (m.commentStart != "(") { return m.commentStart + " " + uL + " " + m.commentEnd; }

            uL = l.Replace('(', ']');
            uL = uL.Replace(')', ']');
            return m.commentStart + " " + uL + " " + m.commentEnd;
        }

        internal static void toolChange([NotNull] IGCodeMachine m, ref CodeInfo co, int toolNumber)
        {
            string[] lines = m.toolChangeCommand.Split(new[] {"\\n"}, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string[] tC = line.Split('#');
                bool first = true;
                foreach (string tCp in tC)
                {
                    if (first && tC.Length > 1) { co.append(tCp + toolNumber); }
                    else { co.append(tCp); }
                    first = false;
                }
            }
        }
    }
}