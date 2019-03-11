using System;
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

        [NotNull] [UsedImplicitly] string speedChangeCommand { get; }
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

        public static double angDiffFiveAxisABTable(Vector3d pivot, double toolLength, [NotNull] ToolPoint fP, [NotNull] ToolPoint tP,bool lng)
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
        [NotNull]
        public static List<List<ToolPath>> stepDown([NotNull] ToolPath tP, [NotNull] IMachine m)
        {
            if(tP.matForm == null) { Exceptions.matFormException(); }
            if (tP.matTool == null) { Exceptions.matToolException(); }
            if (tP.additions == null) { Exceptions.additionsNullException(); }
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

            double cutDepth = tP.matTool.cutDepth <= 0 ? double.PositiveInfinity : tP.matTool.cutDepth;

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
                if (tempTP.additions == null) { Exceptions.nullPanic();}
                tempTP.additions.stepDown = false;
                tempTP.additions.onion = new List<double> { 0 };

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

                                    if (tempTP.additions == null) { Exceptions.nullPanic(); }

                                    tempTP.additions.retract = true;
                                    newPaths[newPaths.Count - 1]?.Add(tempTP); // add path and create a new one

                                    tempTP = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                                    if (tempTP.additions == null) { Exceptions.nullPanic(); }
                                    tempTP.name = tP.name + " Continuing Pass " + i;
                                    tempTP.additions.insert = true;
                                    tempTP.additions.stepDown = false;
                                    tempTP.additions.onion = new List<double> { 0 };

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
            List<double> divs = leadCirc.DivideByCount(v, true)?.ToList() ?? new List<double>();

            Polyline outP = new Polyline();

            foreach (double d in divs)
            {
                Point3d testPt = leadCirc.PointAt(d);
                outP.Add(testPt);
                if (toolL.Contains(testPt) == incorrectSide ) { return null; }
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
        public static ToolPath leadInOut2D([NotNull] ToolPath tP, [NotNull] string activate = "", [NotNull] string deActivate = "", bool keepActivate = false)
        {
            if(tP.matTool == null) { Exceptions.matToolException(); }
            if(tP.additions == null) { Exceptions.additionsNullException(); }
            double leadCurve = tP.additions.leadCurvature;

            ToolPath newTP = tP.deepClone();
            if(newTP.additions == null) { Exceptions.nullPanic(); }
            if(!keepActivate) { newTP.additions.activate = 0; }
            newTP.additions.insert = false;
            newTP.additions.retract = false;

            if (tP.additions.activate != 0 && activate != string.Empty) { newTP.preCode = activate + "\n" + newTP.preCode; }
            if (tP.additions.activate != 0 && deActivate != string.Empty) { newTP.postCode = newTP.postCode + "\n" + deActivate; }

            // If leadCurve == 0 can now return
            if (Math.Abs(leadCurve) < CAMel_Goo.Tolerance) { return newTP; }

            PolylineCurve toolL = tP.getLine();

            if(tP.additions.insert)
            {
                PolylineCurve leadIn = findLead(toolL, leadCurve, tP.matTool.insertWidth, 15, true);
                // If no suitable curve found throw an error
                if (leadIn == null) { newTP.firstP?.addWarning("No suitable curve for lead in found."); }
                else
                {
                    leadIn.Reverse();
                    List<ToolPoint> tPts = new List<ToolPoint>();
                    if(tP.firstP == null) { Exceptions.nullPanic(); }
                    for (int i = 1; i < leadIn.PointCount; i++)
                    {
                        ToolPoint tPt = tP.firstP.deepClone();
                        tPt.pt = leadIn.Point(i);
                        tPts.Add(tPt);
                    }
                    newTP.InsertRange(0, tPts);
                }
                if (newTP.additions == null) { Exceptions.nullPanic(); }
            }

            if (tP.additions.retract)
            {
                PolylineCurve leadOut = findLead(toolL, leadCurve, tP.matTool.insertWidth, 15, false);
                if (leadOut == null) { newTP.lastP?.addWarning("No suitable curve for lead out found."); }
                // If no suitable curve found throw an error
                else
                {
                    for (int i = 1; i < leadOut.PointCount; i++)
                    {
                        if (tP.firstP == null) { Exceptions.nullPanic(); }
                        ToolPoint tPt = tP.firstP.deepClone();
                        tPt.pt = leadOut.Point(i);
                        newTP.Add(tPt);
                    }
                }
                if (newTP.additions == null) { Exceptions.nullPanic(); }
            }

            newTP.additions.leadCurvature = 0;
            return newTP;
        }

        [NotNull]
        internal static ToolPath insertRetract([NotNull] ToolPath tP, [NotNull] string activate = "", [NotNull] string deActivate = "", bool keepActivate = false)
        {
            ToolPath newTP = tP.deepClone();
            if (tP.matTool == null) { Exceptions.matToolException(); }
            if (tP.matForm == null) { Exceptions.matFormException(); }
            if (tP.additions == null) { Exceptions.additionsNullException(); }
            if (newTP.additions == null) { Exceptions.additionsNullException(); }
            newTP.additions.insert = false;
            newTP.additions.retract = false;
            if(!keepActivate) {newTP.additions.activate = 0;}

            MFintersection inter;

            double uTol = tP.matForm.safeDistance * 1.05;
            ToolPoint tempTP;

            if (tP.additions.activate != 0 && activate != string.Empty) { newTP.preCode = activate + "\n" + newTP.preCode; }
            if (tP.additions.activate != 0 && deActivate != string.Empty) { newTP.postCode = newTP.postCode + "\n" + deActivate; }

            // check if we have something to do
            if (tP.additions.insert && newTP.Count > 0) // add insert
            {
                //note we do this backwards adding points to the start of the path.

                // get distance to surface and insert direction
                if (newTP.firstP == null) { Exceptions.nullPanic(); }
                inter = tP.matForm.intersect(newTP.firstP, 0).through;

                // check to see if there was an intersection
                if (inter.isSet)
                {
                    // point on material surface

                    tempTP = newTP.firstP.deepClone();
                    tempTP.pt = inter.point;
                    tempTP.feed = tP.matTool.feedPlunge;
                    newTP.Insert(0, tempTP);

                    // point out at safe distance
                    if (newTP.firstP == null) { Exceptions.nullPanic(); }
                    tempTP = newTP.firstP.deepClone();
                    tempTP.pt = tempTP.pt + inter.away * uTol;
                    tempTP.feed = 0; // we can use a rapid move
                    newTP.Insert(0, tempTP);
                }
                else
                {
                    // check intersection with material extended to safe distance
                    inter = tP.matForm.intersect(newTP.firstP, uTol).through;
                    if (inter.isSet)
                    {
                        // point out at safe distance
                        tempTP = newTP.firstP.deepClone();
                        tempTP.pt = inter.point;
                        tempTP.feed = 0; // we can use a rapid move
                        newTP.Insert(0, tempTP);
                    } //  otherwise nothing needs to be added as we do not interact with material
                }
            }

            if (!tP.additions.retract || newTP.Count <= 0) { return newTP; }
            if (newTP.lastP == null) { Exceptions.nullPanic(); }

            // get distance to surface and retract direction
            inter = tP.matForm.intersect(newTP.lastP, 0).through;
            if (inter.isSet)
            {
                tempTP = newTP.lastP.deepClone();

                // set speed to the plunge feed rate.
                tempTP.feed = tP.matTool.feedPlunge;

                // Pull back to surface
                tempTP.pt = inter.point;

                newTP.Add(tempTP);

                // Pull away to safe distance

                if (newTP.lastP == null) { Exceptions.nullPanic(); }

                tempTP = newTP.lastP.deepClone();
                tempTP.pt = tempTP.pt + inter.away * uTol;
                tempTP.feed = 0; // we can use a rapid move
                newTP.Add(tempTP);
            }
            else
            {
                // check intersection with material extended to safe distance
                inter = tP.matForm.intersect(newTP.lastP, uTol).through;
                if (!inter.isSet) { return newTP; }

                // point out at safe distance
                tempTP = newTP.lastP.deepClone();
                tempTP.pt = inter.point;
                tempTP.feed = 0; // we can use a rapid move
                newTP.Add(tempTP);
            }
            return newTP;
        }



        // Adjust the path so it will not be gouged when cut in 3-axis, or indexed 3-axis mode.
        // TODO make this guarantee that it does not gouge locally. There is a problem
        // with paths that are steep down, followed by some bottom moves followed by steep out.
        [NotNull]
        public static ToolPath threeAxisHeightOffset([NotNull] ToolPath tP, [NotNull] IMachine m)
        {
            if (tP.matTool == null) { Exceptions.matToolException(); }
            List<ToolPoint> offsetPath = new List<ToolPoint>();

            if (tP.Count < 2) { return tP;}

            Vector3d travel = tP[1].pt - tP[0].pt;
            travel.Unitize();

            Vector3d orth = Vector3d.CrossProduct(travel, m.toolDir(tP[0]));
            Vector3d uOrth = orth;

            ToolPoint point = tP.matTool.threeAxisHeightOffset(m, tP[0], travel, uOrth);

            List<Line> osLines = new List<Line> { new Line(point.pt, travel) };

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
                if(osP == null) { Exceptions.nullPanic(); }
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

            if (tP.lastP == null) { Exceptions.nullPanic(); }
            orth = Vector3d.CrossProduct(travel, m.toolDir(tP.lastP));
            if (Math.Abs(orth.Length) > CAMel_Goo.Tolerance) { uOrth = orth; }
            offsetPath.Add(tP.matTool.threeAxisHeightOffset(m, tP.lastP, travel, uOrth));

            ToolPath retPath = tP.deepCloneWithNewPoints(offsetPath);
            if(retPath.additions == null) { Exceptions.nullPanic(); }
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
            if (newTP.additions == null) { Exceptions.additionsNullException(); }
            newTP.additions.threeAxisHeightOffset = false;
            return newTP;
        }

        // Add a finishing versions of the path (including onion Skinning)
        [NotNull]
        public static List<ToolPath> finishPaths([NotNull] ToolPath tP, [NotNull] IMachine m)
        {
            if(tP.additions == null) { Exceptions.additionsNullException(); }
            List<ToolPath> fP = new List<ToolPath>();
            // get the sorted list of onion cuts
            IOrderedEnumerable<double> onionSort = tP.additions.sortOnion;

            foreach (double height in onionSort)
            {
                ToolPath newTP = tP.deepClone(height, m);
                if (newTP.additions == null) { Exceptions.nullPanic(); }
                if (newTP.name != string.Empty) { newTP.name = newTP.name + " "; }
                newTP.name = newTP.name + "(Finish at height " + height.ToString("0.###") + ")";
                newTP.additions.stepDown = false;
                newTP.additions.onion = new List<double> { 0 };
                fP.Add(newTP);
            }

            // If onion is empty add the ToolPath anyway.
            if (fP.Count != 0) { return fP; }
            {
                ToolPath newTP = tP.deepClone();
                if (newTP.additions == null) { Exceptions.nullPanic(); }
                newTP.additions.stepDown = false;
                newTP.additions.onion = new List<double> { 0 };
                fP.Add(newTP);
            }
            return fP;
        }

        // The finish path is just the toolpath
        [NotNull]
        internal static List<ToolPath> oneFinishPath([NotNull] ToolPath tP)
        {
            if (tP.additions == null) { Exceptions.additionsNullException(); }
            ToolPath newTP = tP.deepClone();
            if (newTP.additions == null) { Exceptions.nullPanic(); }
            newTP.additions.stepDown = false;
            newTP.additions.onion = new List<double> { 0 };
            return new List<ToolPath> { newTP };
        }
    }

    public static class GCode
    {
        // Standard terms

        [NotNull] internal const string DefaultCommentStart = "(";
        [NotNull] internal const string DefaultCommentEnd = ")";
        [NotNull] internal const string DefaultSectionBreak = "------------------------------------------";
        [NotNull] internal const string DefaultSpeedChangeCommand = "M03";
        [NotNull] internal const string DefaultToolChangeCommand = "G43H";
        [NotNull] internal const string DefaultActivateCommand = "M61";
        [NotNull] internal const string DefaultDeActivateCommand = "M62";
        [NotNull] internal const string DefaultFileStart = "";
        [NotNull] internal const string DefaultFileEnd = "";

        // Formatting structure for GCode

        public static void gcInstStart([NotNull] IGCodeMachine m, [NotNull] ref CodeInfo co, [NotNull] MachineInstruction mI, [NotNull] ToolPath startPath)
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
            co.currentMT = MaterialTool.Empty; // Clear the tool information so we call a tool change.
            m.writeCode(ref co, startPath);
        }
        public static void gcInstEnd([NotNull] IGCodeMachine m, [NotNull] ref CodeInfo co, [NotNull] MachineInstruction mI, [NotNull] ToolPath finalPath, [NotNull] ToolPath endPath)
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
                if (m.toolLengthCompensation && tP.matTool.toolNumber != co.currentMT.toolNumber) {
                    co.append(m.toolChangeCommand + tP.matTool.toolNumber);
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
            gPtBd.Append(@" Y"); gPtBd.Append(machPt.Y.ToString("0.000"));
            gPtBd.Append(@" Z"); gPtBd.Append(machPt.Z.ToString("0.000"));
            gPtBd.Append(@" A"); gPtBd.Append((180.0 * ab.X / Math.PI).ToString("0.000"));
            gPtBd.Append(@" B"); gPtBd.Append((180.0 * ab.Y / Math.PI).ToString("0.000"));

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
        public static MachineInstruction gcRead([NotNull] IGCodeMachine m, [NotNull][ItemNotNull] List<MaterialTool> mTs, [NotNull] string code, [NotNull] List<char> terms)
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
                    if(mTs.Count > 0) { uMT = mTs[0]; }
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
    }
}