using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using Rhino.Geometry;

using CAMel.Types.MaterialForm;
using static CAMel.Exceptions;

namespace CAMel.Types.Machine
{
    // Some standards to help develop GCode based machines
    public interface IGCodeMachine : IMachine
    {
        string header { get; }
        string footer { get; }

        string speedChangeCommand { get; }
        string toolChangeCommand { get; }

        string sectionBreak { get; }
        string fileStart { get; }
        string fileEnd { get; }
        string commentStart { get; }
        string commentEnd { get; }

        ToolPoint readTP(Dictionary<char, double> vals, MaterialTool mT);
    }

    // ReSharper disable once IdentifierTypo
    // ReSharper disable once InconsistentNaming
    public struct TPchanges
    {
        public bool mT { get; set; }
        public bool mF { get; set; }
        public TPchanges(bool mT, bool mF)
        {
            this.mT = mT;
            this.mF = mF;
        }
    }

    public static class Kinematics
    {
        // Collection of Inverse Kinematics

        // 2-Axis and 3-Axis don't need any work, so they just need writing functions
        // in the GCode library, plus a general purpose linear interpolation.

        public static ToolPoint interpolateLinear(ToolPoint fP, ToolPoint tP, double p)
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

        public static Vector3d ikFiveAxisABTable(ToolPoint tP, Vector3d pivot, double toolLength, out Point3d machPt)
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

        public static ToolPoint kFiveAxisABTable(ToolPoint tP, Vector3d pivot, double toolLength, Point3d machPt, Vector3d ab)
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

        public static ToolPoint interpolateFiveAxisABTable(Vector3d pivot, double toolLength, ToolPoint from, ToolPoint to, double p, bool lng)
        {
            Point3d fromMachPt;
            Point3d toMachPt;
            Vector3d fromAB = ikFiveAxisABTable(from, pivot, toolLength, out fromMachPt);
            Vector3d toAB = ikFiveAxisABTable(to, pivot, toolLength, out toMachPt);

            Point3d outPt = (1 - p) * fromMachPt + p * toMachPt;
            Vector3d outAB = (1 - p) * fromAB + p * toAB;
            // switch to long way round or short way round depending on gap between angles
            if ((lng && Math.Abs(fromAB.Y - toAB.Y) <= Math.PI) ||
               (!lng && Math.Abs(fromAB.Y - toAB.Y) > Math.PI))
            {
                Vector3d alt;
                if (fromAB.Y > toAB.Y) { alt = new Vector3d(0, 2 * Math.PI, 0); }
                else { alt = new Vector3d(0, -2 * Math.PI, 0); }
                outAB = (1 - p) * fromAB + p * (toAB + alt);
            }
            return kFiveAxisABTable(from, pivot, toolLength, outPt, outAB);
        }

        public static double angDiffFiveAxisABTable(Vector3d pivot, double toolLength, ToolPoint fP, ToolPoint tP,bool lng)
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

    }

    public static class Utility
    {
        // Step down into material
        public static List<List<ToolPath>> stepDown(ToolPath tP, IMachine m)
        {
            // Give default value for negative DropMiddle
            if (tP.additions.sdDropMiddle < 0) { tP.additions.sdDropMiddle = 8.0 * tP.matForm.safeDistance; }

            IOrderedEnumerable<double> onionSort = tP.additions.sortOnion;

            // Use the material form to work out the distance to cut in the
            // material, the direction to enter the material and the number of passes.
            List<double> matDist = new List<double>();
            List<int> numSteps = new List<int>();
            int maxSteps = 0; // Maximum distance of all points.

            // ask the material form to refine the path

            ToolPath refPath = tP.matForm.refine(tP, m);

            double finishDepth;
            if (tP.matTool.finishDepth <= 0) { finishDepth = onionSort.First(); }
            else { finishDepth = tP.matTool.finishDepth + onionSort.First(); }

            double cutDepth;
            if (tP.matTool.cutDepth <= 0) { cutDepth = double.PositiveInfinity; }
            else { cutDepth = tP.matTool.cutDepth; }


            foreach (ToolPoint tPt in refPath)
            {
                MFintersection inter = tP.matForm.intersect(tPt, 0).through;
                matDist.Add(inter.lineP); // distance to material surface
                if (matDist[matDist.Count - 1] < 0) { matDist[matDist.Count - 1] = 0; }// avoid negative distances (outside material)

                // calculate maximum number of cutDepth height steps down to finishDepth above material
                numSteps.Add((int)Math.Ceiling((matDist[matDist.Count - 1] - finishDepth) / cutDepth));
                if (numSteps[numSteps.Count - 1] > maxSteps) { maxSteps = numSteps[numSteps.Count - 1]; }
            }

            // make a list of depths to cut at.
            // This just steps down right now, but makes it easier to add fancier levelling, if ever worthwhile.
            // Note that maxsteps currently assumes only stepping down by cutDepth.

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
                tempTP.name = tP.name + " Pass " + (i + 1).ToString();
                tempTP.additions.stepDown = false;
                tempTP.additions.onion = new List<double>() { 0 };

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
                            height = finishDepth;
                            if (height > matDist[j - 1]) { height = 0; }
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
                        if (!tP.additions.sdDropStart) // we are not dropping the start
                        {
                            tPt = refPath[j].deepClone();
                            height = finishDepth;
                            if (height > matDist[j]) { height = 0; }
                            tPt.pt = m.toolDir(tPt) * height + tPt.pt;
                            tempTP.Add(tPt);
                        } // otherwise we do nothing
                    }
                    else // We need to look ahead
                    {
                        int k;
                        for (k = j; k < refPath.Count && i >= numSteps[k]; k++) {} // Look ahead to the next cut

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
                            if (tP.additions.sdDropMiddle < 0 || (k - j) < 3) // we are not dropping middle or there are not enough points to justify it
                            {
                                tPt = refPath[j].deepClone();
                                height = finishDepth;
                                tPt.pt = m.toolDir(tPt) * height + tPt.pt;
                                tempTP.Add(tPt);
                            }
                            else //check length of drop
                            {
                                if (Math.Abs(dropLength) < CAMel_Goo.tolerance) // If we are at the start of a possible drop Add the length until we hit the end or go over
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
                                    newPaths[newPaths.Count - 1].Add(tempTP); // add path and create a new one

                                    tempTP = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                                    tempTP.name = tP.name + " Continuing Pass " + i.ToString();
                                    tempTP.additions.insert = true;
                                    tempTP.additions.stepDown = false;
                                    tempTP.additions.onion = new List<double>() { 0 };

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
                newPaths[newPaths.Count - 1].Add(tempTP);
            }
            return newPaths;
        }

        private static PolylineCurve findLead(PolylineCurve toolL, double leadCurve, double insertWidth, int v, bool start)
        {
            // work out the rotation to get the desired normal
            double normAng = Math.PI / 2.0;
            // take into account the orientation of the path
            if (toolL.ClosedCurveOrientation(Vector3d.ZAxis) == CurveOrientation.CounterClockwise) { normAng = -normAng; }
            // now we have the internal normal, flip if we want external.
            if (leadCurve >= 0) { normAng = -normAng; }

            PointContainment incorrectSide = PointContainment.Inside;
            if (leadCurve < 0) { incorrectSide = PointContainment.Outside; }
            double uLeadCurve = Math.Abs(leadCurve);

            Point3d startPt = toolL.PointAtStart;
            // Get tangents and the Normal pointing in the direction we want the lead.
            Vector3d startTan = toolL.TangentAtStart;
            Vector3d startNorm = startTan; startNorm.Rotate(normAng, Vector3d.ZAxis);

            Vector3d endTan = toolL.TangentAtEnd;
            Vector3d endNorm = endTan; endNorm.Rotate(normAng, Vector3d.ZAxis);

            Vector3d uTan, uNorm;

            if (start) { uTan = -endTan; uNorm = endNorm; }
            else { uTan = startTan; uNorm = startNorm; }
            // Start by using the end version, we will choose the start version

            ArcCurve leadCirc = new ArcCurve(new Arc(startPt, uTan, startPt + uLeadCurve * (uNorm + uTan)));

            // step along the arc trying to find a point more that insert distance from the path
            List<double> divs = leadCirc.DivideByCount(v, true).ToList();
            Polyline outP = new Polyline();


            foreach (double d in divs)
            {
                Point3d testPt = leadCirc.PointAt(d);
                outP.Add(testPt);
                double testdist;
                if (toolL.Contains(testPt) == incorrectSide ) { return null; }
                toolL.ClosestPoint(testPt, out testdist);
                testdist = testPt.DistanceTo(toolL.PointAt(testdist));
                if (testdist > insertWidth * 0.52) { return new PolylineCurve(outP); }
            }

            // Now try to keep going straight

            for (double i = insertWidth / 10.0; i < 2 * insertWidth; i = i + insertWidth / 10.0)
            {
                Point3d testPt = leadCirc.PointAtEnd + uNorm * i;
                outP.Add(testPt);
                double testdist;
                if (toolL.Contains(testPt) == incorrectSide) { return null; }

                toolL.ClosestPoint(testPt, out testdist);
                testdist = testPt.DistanceTo(toolL.PointAt(testdist));
                if (testdist > insertWidth * 0.52) { return new PolylineCurve(outP); }
            }
            return null;
        }

        public static ToolPath leadInOut2D(ToolPath tP, string insert, string retract)
        {
            double leadCurve = tP.additions.leadCurvature;

            ToolPath newTP = tP.deepClone();

            // Just add commands as there is no lead
            if (Math.Abs(leadCurve) < CAMel_Goo.tolerance) {
                if (tP.additions.insert && insert != String.Empty) { newTP.postCode = newTP.postCode + "\n" + retract; }
                if (tP.additions.retract && retract != String.Empty) { newTP.postCode = newTP.postCode + "\n" + retract; }
                newTP.additions.insert = false;
                newTP.additions.retract = false;
                return newTP;
            }

            PolylineCurve toolL = tP.getLine();

            if(tP.additions.insert)
            {
                PolylineCurve leadIn = findLead(toolL, leadCurve, tP.matTool.insertWidth, 15, true);
                // If no suitable curve found throw an error
                if (leadIn == null) { newTP.firstP.addWarning("No suitable curve for lead in found."); }
                else
                {
                    leadIn.Reverse();
                    List<ToolPoint> tPts = new List<ToolPoint>();
                    for (int i = 1; i < leadIn.PointCount; i++)
                    {
                        ToolPoint tPt = tP.firstP.deepClone();
                        tPt.pt = leadIn.Point(i);
                        tPts.Add(tPt);
                    }
                    newTP.InsertRange(0, tPts);
                }
                if (insert != String.Empty) { newTP.preCode = newTP.preCode + "\n" + insert; }
                newTP.additions.insert = false;
            }

            if (tP.additions.retract)
            {
                PolylineCurve leadOut = findLead(toolL, leadCurve, tP.matTool.insertWidth, 15, false);
                if (leadOut == null) { newTP.lastP.addWarning("No suitable curve for lead out found."); }
                // If no suitable curve found throw an error
                else
                {
                    for (int i = 1; i < leadOut.PointCount; i++)
                    {
                        ToolPoint tPt = tP.firstP.deepClone();
                        tPt.pt = leadOut.Point(i);
                        newTP.Add(tPt);
                    }
                }
                if (retract != String.Empty) { newTP.postCode = newTP.postCode + "\n" + retract; }
                newTP.additions.retract = false;
            }

            newTP.additions.leadCurvature = 0;
            return newTP;
        }

        // Adjust the path so it will not be gouged when cut in 3-axis, or indexed 3-axis mode.
        // TODO make this guarantee that it does not gouge locally. There is a problem
        // with paths that are steep down, followed by some bottom moves followed by steep out.
        public static ToolPath threeAxisHeightOffset(ToolPath tP, IMachine m)
        {
            if (tP.matTool == null) { matToolException(); return null; }
            List<ToolPoint> offsetPath = new List<ToolPoint>();

            Vector3d travel = tP[1].pt - tP[0].pt;
            travel.Unitize();

            ToolPoint point;
            Vector3d orth = Vector3d.CrossProduct(travel, m.toolDir(tP[0]));
            Vector3d uOrth = orth;

            point = tP.matTool.threeAxisHeightOffset(m, tP[0], travel, uOrth);

            List<Line> osLines = new List<Line> { new Line(point.pt, travel) };

            double inter;
            ToolPoint nextPoint;
            double nextinter;
            double orient;
            ToolPoint endCP, startCP;

            bool changeDirection = false; // Has tool direction changed?

            offsetPath.Add(point);

            // loop through the lines of the toolpath finding their offset path
            // and then travelling to the closest point to the next offset path

            for (int i = 1; i < tP.Count - 1; i++)
            {
                // Keep track of tool point direction to warn if it changes (but only once)
                if (m.toolDir(tP[i - 1]) != m.toolDir(tP[i]) && !changeDirection)
                {
                    tP[i].warning.Add("Height offsetting is not designed for 5-Axis motion, it may be unpredictable.");
                    changeDirection = true;
                }
                // Find the next offset line
                travel = tP[i + 1].pt - tP[i].pt;
                orth = Vector3d.CrossProduct(travel, m.toolDir(tP[0]));
                if (Math.Abs(orth.Length) > CAMel_Goo.tolerance) { uOrth = orth; }

                nextPoint = tP.matTool.threeAxisHeightOffset(m, tP[i], travel, uOrth);

                // find the next line we will travel along
                osLines.Add(new Line(nextPoint.pt, travel));

                // we need to find the last path that does not reverse when we travel along our new line.
                // if we go in the wrong direction on an offset path then we are gouging back into previously cut material.
                // In the following we discuss intersection, for lines in 3d this is given by the closest point for two lines.

                // intersect the new line with the last line we used
                Rhino.Geometry.Intersect.Intersection.LineLine(osLines[osLines.Count - 2], osLines[osLines.Count - 1], out inter, out nextinter);
                // find the orientation of the new path
                orient = (osLines[osLines.Count - 2].PointAt(inter) - offsetPath[offsetPath.Count - 1].pt) * osLines[osLines.Count - 2].UnitTangent;

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
                    Rhino.Geometry.Intersect.Intersection.LineLine(osLines[osLines.Count - 2], osLines[osLines.Count - 1], out inter, out nextinter);
                    orient = (osLines[osLines.Count - 2].PointAt(inter) - offsetPath[offsetPath.Count - 1].pt) * osLines[osLines.Count - 2].UnitTangent;
                }

                // if we got to the start and things are still bad we have to deal with things differently
                if (orient < 0)
                {
                    // remove the old start point and add the closest point on the new first line
                    offsetPath.RemoveAt(0);

                    // intersect our new line with the first direction
                    Rhino.Geometry.Intersect.Intersection.LineLine(osLines[0], osLines[osLines.Count - 1], out inter, out nextinter);

                    // note that we keep the information from the toolpoint before the line we are going to be cutting
                    // tP might be removed on later passes, if the line is removed.
                    startCP = tP[i].deepClone();
                    startCP.pt = osLines[osLines.Count - 1].PointAt(nextinter);
                    offsetPath.Add(startCP);

                }
                else
                {
                    // Add the new intersection we like using the closest points on the two lines (the points on each line closest to the other line)
                    // note that we keep the information from the toolpoint before the line we are going to be cutting
                    // this might be removed on later passes, if the line is removed.
                    endCP = tP[i].deepClone();
                    endCP.pt = osLines[osLines.Count - 2].PointAt(inter);
                    startCP = tP[i].deepClone();
                    startCP.pt = osLines[osLines.Count - 1].PointAt(nextinter);

                    // take the midpoint of the two intersections
                    // there is possibly something clever to do here
                    startCP.pt = (startCP.pt + endCP.pt) / 2;

                    offsetPath.Add(startCP);
                    //offsetPath.Add(endCP);
                }
            }

            // add the final point.

            orth = Vector3d.CrossProduct(travel, m.toolDir(tP.lastP));
            if (Math.Abs(orth.Length) > CAMel_Goo.tolerance) { uOrth = orth; }
            offsetPath.Add(tP.matTool.threeAxisHeightOffset(m, tP.lastP, travel, uOrth));

            ToolPath retPath = tP.deepCloneWithNewPoints(offsetPath);
            retPath.additions.threeAxisHeightOffset = false;

            if (!retPath.additions.insert)
            { retPath.firstP.warning.Add("Height Offsetting does not work between ToolPaths. This might cause unexpected behaviour."); }
            if (!retPath.additions.retract)
            { retPath.lastP.warning.Add("Height Offsetting does not work between ToolPaths. This might cause unexpected behaviour."); }

            return retPath;
        }

        // Clear the addition but don't do anything
        public static ToolPath clearThreeAxisHeightOffset(ToolPath tP)
        {
            ToolPath newTP = tP.deepClone();
            newTP.additions.threeAxisHeightOffset = false;
            return newTP;
        }

        // Add a finishing versions of the path (including onion Skinning)
        public static List<ToolPath> finishPaths(ToolPath tP, IMachine m)
        {
            var fP = new List<ToolPath>();
            // get the sorted list of onion cuts
            IOrderedEnumerable<double> onionSort = tP.additions.sortOnion;

            foreach (double height in onionSort)
            {
                ToolPath newTP = tP.deepClone(height, m);
                if(newTP.name != String.Empty) { newTP.name = newTP.name + " "; }
                newTP.name = newTP.name + "(Finish at height " + height.ToString("0.###") + ")";
                newTP.additions.stepDown = false;
                newTP.additions.onion = new List<double>() { 0 };
                fP.Add(newTP);
            }

            // If onion is empty add the ToolPath anyway.
            if(fP.Count == 0 )
            {
                ToolPath newTP = tP.deepClone();
                newTP.additions.stepDown = false;
                newTP.additions.onion = new List<double>() { 0 };
                fP.Add(newTP);
            }
            return fP;
        }

        // The finish path is just the toolpath
        internal static List<ToolPath> oneFinishPath(ToolPath tP)
        {
            ToolPath newTP = tP.deepClone();
            newTP.additions.stepDown = false;
            newTP.additions.onion = new List<double>() { 0 };
            return new List<ToolPath>() { newTP };
        }
    }

    public static class GCode
    {
        // Standard terms

        internal static readonly string defaultCommentStart = "(";
        internal static readonly string defaultCommentEnd = ")";
        internal static readonly string defaultSectionBreak = "------------------------------------------";
        internal static readonly string defaultSpeedChangeCommand = "M03";
        internal static readonly string defaultToolChangeCommand = "G43H";
        internal static readonly string defaultInsertCommand = "M61";
        internal static readonly string defaultRetractCommand = "M62";
        internal static readonly string defaultFileStart = string.Empty;
        internal static readonly string defaultFileEnd = string.Empty;

        // Formatting structure for GCode

        public static void gcInstStart(IGCodeMachine m, ref CodeInfo co, MachineInstruction mI, ToolPath startPath)
        {
            if (mI == null || mI.Count == 0) { noOperationException(); return; }
            if (mI[0] == null || mI[0].Count == 0) { noToolPathException(); return; }
            if (mI[0][0].matTool == null) { matToolException(); return; }
            if (mI[0][0].matForm == null) { matFormException(); return; }

            co.currentMT = mI[0][0].matTool;
            co.currentMF = mI[0][0].matForm;

            DateTime thisDay = DateTime.Now;
            co.appendLineNoNum(m.fileStart);
            co.appendComment(m.sectionBreak);
            if (mI.name != string.Empty) { co.appendComment(mI.name); }
            co.appendComment("");
            co.appendComment(" Machine Instructions Created " + thisDay.ToString("f"));
            co.appendComment("  by " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " "
                + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            if (m.name != string.Empty) { co.appendComment("  for " + m.name); }
            co.appendComment(" Starting with: ");
            co.appendComment("  Tool: " + mI[0][0].matTool.toolName);
            co.appendComment("  in " + mI[0][0].matTool.matName + " with shape " + mI[0][0].matForm.ToString());
            co.appendComment("");
            co.appendComment(m.sectionBreak);
            co.append(m.header);
            co.append(mI.preCode);
            co.currentMT = new MaterialTool(); // Clear the tool information so we call a tool change.
            m.writeCode(ref co, startPath);
        }
        public static void gcInstEnd(IGCodeMachine m, ref CodeInfo co, MachineInstruction mI, ToolPath finalPath, ToolPath endPath)
        {
            co.appendComment(m.sectionBreak);
            m.writeTransition(ref co, finalPath, endPath, true);
            m.writeCode(ref co, endPath);

            co.appendComment(m.sectionBreak);
            co.appendComment(" End of ToolPaths");
            co.appendComment(m.sectionBreak);

            co.append(mI.postCode);
            co.append(m.footer);
            co.appendLineNoNum(m.fileEnd);
        }

        public static void gcOpStart(IGCodeMachine m, ref CodeInfo co, MachineOperation mO)
        {
            co.appendComment(m.sectionBreak);
            co.appendComment("");
            co.appendComment(" Operation: " + mO.name);
            co.appendComment("");
            co.append(mO.preCode);
        }
        public static void gcOpEnd(IGCodeMachine m, ref CodeInfo co, MachineOperation mO)
        {
            co.appendComment(mO.postCode);
        }

        public static void gcPathStart(IGCodeMachine m, ref CodeInfo co, ToolPath tP)
        {
            if (tP.matTool == null) { matToolException(); }
            if (tP.matForm == null) { matFormException(); }
            TPchanges ch = new TPchanges(false, false);
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
                if (m.toolLengthCompensation && tP.matTool.toolNumber != co.currentMT.toolNumber) {
                    co.append(m.toolChangeCommand + tP.matTool.toolNumber);
                }
                co.currentMT = tP.matTool;
                ch.mT = true;
                preamble = true;
            }
            if (tP.matForm != null && tP.matForm.ToString() != co.currentMF.ToString())
            {
                co.appendComment(" material: " + tP.matForm.ToString());
                co.currentMF = tP.matForm;
                ch.mF = true;
                preamble = true;
            }

            if (preamble) { co.appendComment(m.sectionBreak); }

            co.append(tP.preCode);
        }
        public static void gcPathEnd(IGCodeMachine m, ref CodeInfo co, ToolPath tP)
        {
            co.append(tP.postCode);
        }

        // Toolpoint writers
        // These might be simpler to pull
        // into a single "write" command taking a dictionary?
        public static string gcTwoAxis(ToolPoint tP)
        {
            Point3d op = tP.pt;
            string gPoint = string.Empty;
            gPoint += "X" + op.X.ToString("0.000") + " Y" + op.Y.ToString("0.000");

            return gPoint;
        }
        public static string gcThreeAxis(ToolPoint tP)
        {
            Point3d op = tP.pt;
            string gPoint = string.Empty;
            gPoint += "X" + op.X.ToString("0.000") + " Y" + op.Y.ToString("0.000") + " Z" + op.Z.ToString("0.000");

            return gPoint;
        }
        public static string gcFiveAxisAB(Point3d machPt, Vector3d ab)
        {
            StringBuilder gPtBd = new StringBuilder(@"X", 34);
            gPtBd.Append(machPt.X.ToString("0.000"));
            gPtBd.Append(@" Y"); gPtBd.Append(machPt.Y.ToString("0.000"));
            gPtBd.Append(@" Z"); gPtBd.Append(machPt.Z.ToString("0.000"));
            gPtBd.Append(@" A"); gPtBd.Append((180.0 * ab.X / Math.PI).ToString("0.000"));
            gPtBd.Append(@" B"); gPtBd.Append((180.0 * ab.Y / Math.PI).ToString("0.000"));

            return gPtBd.ToString();
        }

        // GCode reading
        private static readonly Regex _numbPattern = new Regex(@"^([0-9\-.]+)", RegexOptions.Compiled);

        private static double getValue(string line, char split, double old, ref bool changed)
        {
            double val = old;
            string[] splitLine = line.Split(split);
            if (splitLine.Length > 1)
            {
                Match monkey = _numbPattern.Match(splitLine[1]);
                if (monkey.Success)
                { val = double.Parse(monkey.Value); }
                if (Math.Abs(val - old) > CAMel_Goo.tolerance) { changed = true; }
            }
            return val;
        }
        // TODO detect tool changes and new paths
        public static ToolPath gcRead(IGCodeMachine m, List<MaterialTool> mTs, string code, List<char> terms)
        {
            ToolPath tP = new ToolPath();
            Dictionary<char, double> vals = new Dictionary<char, double>();

            foreach (char c in terms) { vals.Add(c, 0); }

            using (StringReader reader = new StringReader(code))
            {
                // Loop over the lines in the string.
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    bool changed = false;
                    foreach (char t in terms)
                    { vals[t] = getValue(line, t, vals[t], ref changed); }
                    //interpret a G0 command.
                    if (line.Contains(@"G00") || line.Contains(@"G0 "))
                    {
                        if (vals.ContainsKey('F') && Math.Abs(vals['F']) > CAMel_Goo.tolerance)
                        {
                            changed = true;
                            vals['F'] = 0;
                        }
                    }
                    MaterialTool uMT = new MaterialTool();
                    if(mTs.Count > 0) { uMT = mTs[0]; }
                    if (changed) { tP.Add(m.readTP(vals, uMT)); }
                }
            }
            return tP;
        }

        public static string comment(IGCodeMachine m, string l)
        {
            if (l == "" || l == " ") { return " "; }
            else
            {
                string uL = l;
                // Avoid "nested comments"
                if(m.commentStart == "(")
                {
                    uL = l.Replace('(', ']');
                    uL = uL.Replace(')', ']');
                }
                return m.commentStart + " " + uL + " " + m.commentEnd;
            }
        }
    }
}