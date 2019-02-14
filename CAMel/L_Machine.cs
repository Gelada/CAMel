using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

using Rhino.Geometry;
using Rhino.Geometry.Intersect;

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

        ToolPoint readTP(Dictionary<char, double> vals, MaterialTool MT);
    }

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

        static public ToolPoint interpolateLinear(ToolPoint fP, ToolPoint tP, double p)
        {
            ToolPoint TPo = fP.deepClone();
            TPo.pt = tP.pt * p + fP.pt * (1 - p);
            return TPo;
        }

        // 5-Axis...
        // 5-Axis machine have some non-local issues, especially on machines
        // that can rotate fully, so need non-trivial K and IK functions
        //
        // Should really output a machine state type, but not much use for that yet.

        static public Vector3d ikFiveAxisABTable(ToolPoint TP, Vector3d Pivot, double toolLength, out Point3d MachPt)
        {
            // Always gives B from -pi to pi and A from -pi/2 to pi/2.
            double Ao = Math.Asin(TP.dir.Y);
            double Bo = Math.Atan2(-TP.dir.X, TP.dir.Z);

            if (Ao > Math.PI / 2.0)
            {
                Ao = Math.PI - Ao;
                Bo = Bo - Math.PI;
                if (Bo < 0) { Bo = Bo + 2.0 * Math.PI; }
            }

            if (Ao < -Math.PI / 2.0)
            {
                Ao = Math.PI - Ao;
                Bo = Bo - Math.PI;
                if (Bo < 0) { Bo = Bo + 2.0 * Math.PI; }
            }
     
            Point3d OP = TP.pt;

            // rotate from material orientation to machine orientation
            OP.Transform(Transform.Rotation(Bo, Vector3d.YAxis, Point3d.Origin));
            OP.Transform(Transform.Rotation(Ao, Vector3d.XAxis, Point3d.Origin));

            // translate from origin at pivot to origin set so that the tooltip is at machine origin
            OP = OP - Pivot + Vector3d.ZAxis * toolLength;

            MachPt = OP;
            return new Vector3d(Ao, Bo, 0);
        }

        static public ToolPoint kFiveAxisABTable(ToolPoint TP, Vector3d Pivot, double toolLength, Point3d MachPt, Vector3d AB)
        {
            Point3d OP = MachPt;
            // translate from the tooltip at machine origin origin to pivot at origin
            OP = OP + Pivot - Vector3d.ZAxis * toolLength;

            // rotate from machine orientation to material orientation
            OP.Transform(Transform.Rotation(-AB.X, Vector3d.XAxis, Point3d.Origin));
            OP.Transform(Transform.Rotation(-AB.Y, Vector3d.YAxis, Point3d.Origin));

            Vector3d Dir = Vector3d.ZAxis;
            // rotate from machine orientation to material orientation
            Dir.Transform(Transform.Rotation(-AB.X, Vector3d.XAxis, Point3d.Origin));
            Dir.Transform(Transform.Rotation(-AB.Y, Vector3d.YAxis, Point3d.Origin));

            ToolPoint outTP = TP.deepClone();
            outTP.pt = OP;
            outTP.dir = Dir;

            return outTP;
        }

        // Interpolate the machine axes linearly between two positions. 
        // If both axes have full rotation then there are four ways to do this.
        // If lng is true then reverse the direction on the B axis (for PocketNC)

        public static ToolPoint interpolateFiveAxisABTable(Vector3d Pivot, double toolLength, ToolPoint from, ToolPoint to, double p, bool lng)
        {
            Point3d fromMachPt = new Point3d();
            Point3d toMachPt = new Point3d();
            Vector3d fromAB = ikFiveAxisABTable(from, Pivot, toolLength, out fromMachPt);
            Vector3d toAB = ikFiveAxisABTable(to, Pivot, toolLength, out toMachPt);
            Vector3d outAB;
            Point3d outPt;

            outPt = (1 - p) * fromMachPt + p * toMachPt;
            outAB = (1 - p) * fromAB + p * toAB;
            // switch to long way round or short way round depending on gap between angles
            if ((lng && Math.Abs(fromAB.Y - toAB.Y) <= Math.PI) ||
               (!lng && Math.Abs(fromAB.Y - toAB.Y) > Math.PI))
            {
                Vector3d alt;
                if (fromAB.Y > toAB.Y) { alt = new Vector3d(0, 2 * Math.PI, 0); }
                else { alt = new Vector3d(0, -2 * Math.PI, 0); }
                outAB = (1 - p) * fromAB + p * (toAB + alt);
            }
            return kFiveAxisABTable(from, Pivot, toolLength, outPt, outAB);
        }

        public static double angDiffFiveAxisABTable(Vector3d Pivot, double toolLength, ToolPoint fP, ToolPoint tP,bool lng)
        {
            Point3d MachPt = new Point3d();
            Vector3d ang1 = ikFiveAxisABTable(fP, Pivot, toolLength, out MachPt);
            Vector3d ang2 = ikFiveAxisABTable(tP, Pivot, toolLength, out MachPt);

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
        public static List<List<ToolPath>> stepDown(ToolPath tP, IMachine M)
        {
            // Give default value for negative DropMiddle
            if (tP.Additions.sdDropMiddle < 0) { tP.Additions.sdDropMiddle = 8.0 * tP.matForm.safeDistance; }

            IOrderedEnumerable<double> onionSort = tP.Additions.sortOnion;

            // Use the material form to work out the distance to cut in the
            // material, the direction to enter the material and the number of passes.
            List<double> MatDist = new List<double>();
            List<int> NumSteps = new List<int>();
            int MaxSteps = 0; // Maximum distance of all points. 
            List<Vector3d> MatNorm = new List<Vector3d>(); // list of surface normals

            // ask the material form to refine the path

            ToolPath refPath = tP.matForm.refine(tP, M);
            MFintersection inter;

            double finishDepth;
            if (tP.matTool.finishDepth <= 0) { finishDepth = onionSort.First(); }
            else { finishDepth = tP.matTool.finishDepth + onionSort.First(); }

            double cutDepth;
            if (tP.matTool.cutDepth <= 0) { cutDepth = double.PositiveInfinity; }
            else { cutDepth = tP.matTool.cutDepth; }


            foreach (ToolPoint TP in refPath)
            {
                inter = tP.matForm.intersect(TP, 0).through;
                MatDist.Add(inter.lineP); // distance to material surface
                if (MatDist[MatDist.Count - 1] < 0) { MatDist[MatDist.Count - 1] = 0; }// avoid negative distances (outside material)
                MatNorm.Add(new Vector3d(inter.away));
                // calculate maximum number of cutDepth height steps down to finishDepth above material
                NumSteps.Add((int)Math.Ceiling((MatDist[MatDist.Count - 1] - finishDepth) / cutDepth));
                if (NumSteps[NumSteps.Count - 1] > MaxSteps) { MaxSteps = NumSteps[NumSteps.Count - 1]; }
            }

            // make a list of depths to cut at.
            // This just steps down right now, but makes it easier to add fancier levelling, if ever worthwhile. 
            // Note that maxsteps currently assumes only stepping down by cutDepth.

            List<double> CutLevel = new List<double>();
            for (int i = 0; i < MaxSteps; i++) { CutLevel.Add((i + 1) * cutDepth); }

            // process the paths, staying away from the final cut

            ToolPoint TPt;
            bool start;
            bool end;
            double droplength; // length of dropped curve in the middle of a path
            double height; // height above final path

            ToolPath tempTP;

            // need a list for each step down as it might split into more than one path
            // and we need to keep those together to coordinate the Machine Operation
            List<List<ToolPath>> NewPaths = new List<List<ToolPath>>();

            for (int i = 0; i < CutLevel.Count; i++)
            {
                NewPaths.Add(new List<ToolPath>());
                tempTP = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                tempTP.name = tP.name + " Pass " + (i + 1).ToString();
                tempTP.Additions.stepDown = false;
                tempTP.Additions.onion = new List<double>() { 0 };

                start = true;
                end = false;
                droplength = 0;

                for (int j = 0; j < refPath.Count && !end; j++)
                {
                    if (i < NumSteps[j]) // We need to cut here
                    {
                        // if this is the first point to cut we need to add the previous one
                        // if there was one, so we do not miss what was between them
                        if (start && j > 0)
                        {
                            TPt = refPath[j - 1].deepClone();
                            height = finishDepth;
                            if (height > MatDist[j - 1]) { height = 0; }
                            TPt.pt = M.toolDir(TPt) * height + TPt.pt; // stay finishDepth above final path

                            tempTP.Add(TPt);
                        }
                        height = MatDist[j] - CutLevel[i];
                        if (height < finishDepth) { height = finishDepth; } // stay finishDepth above final path
                        TPt = refPath[j].deepClone();
                        TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                        tempTP.Add(TPt);
                        start = false;
                        droplength = 0;
                    }
                    else if (start) // We have not hit any cutting yet;
                    {
                        if (!tP.Additions.sdDropStart) // we are not dropping the start
                        {
                            TPt = refPath[j].deepClone();
                            height = finishDepth;
                            if (height > MatDist[j]) { height = 0; }
                            TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                            tempTP.Add(TPt);
                        } // otherwise we do nothing
                    }
                    else // We need to look ahead
                    {
                        int k;
                        for (k = j; k < refPath.Count && i >= NumSteps[k]; k++) {; } // Look ahead to the next cut

                        if (k == refPath.Count) // No more cutting required
                        {
                            if (tP.Additions.sdDropEnd) // we are dropping the end
                            {
                                // Add point as the previous one was deep, 
                                // then set end to true so we finish
                                TPt = refPath[j].deepClone();
                                height = finishDepth;
                                TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                                tempTP.Add(TPt);
                                end = true;
                            }
                            else // add point
                            {
                                TPt = refPath[j].deepClone();
                                height = finishDepth;
                                TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                                tempTP.Add(TPt);
                            }
                        }
                        else // into the middle
                        {
                            if (tP.Additions.sdDropMiddle < 0 || (k - j) < 3) // we are not dropping middle or there are not enough points to justify it
                            {
                                TPt = refPath[j].deepClone();
                                height = finishDepth;
                                TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                                tempTP.Add(TPt);
                            }
                            else //check length of drop
                            {
                                if (droplength == 0) // If we are at the start of a possible drop Add the length until we hit the end or go over 
                                {
                                    int l;
                                    for (l = j; droplength < tP.Additions.sdDropMiddle && l < k; l++)
                                    { droplength += refPath[l].pt.DistanceTo(refPath[l + 1].pt); }
                                }
                                if (droplength > tP.Additions.sdDropMiddle)
                                {
                                    // add point, as previous point was in material
                                    TPt = refPath[j].deepClone();
                                    height = finishDepth;
                                    TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                                    tempTP.Add(TPt);
                                    // leap forward cut path and start a new one
                                    // giving settings to add inserts and retracts

                                    tempTP.Additions.retract = true;
                                    NewPaths[NewPaths.Count - 1].Add(tempTP); // add path and create a new one

                                    tempTP = (ToolPath)tP.deepCloneWithNewPoints(new List<ToolPoint>());
                                    tempTP.name = tP.name + " Continuing Pass " + i.ToString();
                                    tempTP.Additions.insert = true;
                                    tempTP.Additions.stepDown = false;
                                    tempTP.Additions.onion = new List<double>() { 0 };

                                    // add k-1 point as k is deep
                                    // this will not result in a double point as we checked (k-j) >=3
                                    TPt = refPath[k - 1].deepClone();
                                    height = finishDepth;
                                    TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                                    tempTP.Add(TPt);
                                    j = k - 1; //set j to k-1 so it deals with the k point next

                                }
                                else // after all that we still need to add the point
                                {
                                    TPt = refPath[j].deepClone();
                                    height = finishDepth;
                                    TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                                    tempTP.Add(TPt);
                                }
                            }
                        }
                    }
                }
                NewPaths[NewPaths.Count - 1].Add(tempTP);
            }
            return NewPaths;
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

        public static ToolPath leadInOut2d(ToolPath TP, string insert, string retract)
        {
            double leadCurve = TP.Additions.leadCurvature;

            ToolPath newTP = TP.deepClone();

            // Just add commands as there is no lead
            if (leadCurve == 0) {
                if (TP.Additions.insert && insert != String.Empty) { newTP.postCode = newTP.postCode + "\n" + retract; }
                if (TP.Additions.retract && retract != String.Empty) { newTP.postCode = newTP.postCode + "\n" + retract; }
                newTP.Additions.insert = false;
                newTP.Additions.retract = false;
                return newTP;
            }

            PolylineCurve toolL = TP.getLine();

            if(TP.Additions.insert)
            {
                PolylineCurve leadIn = findLead(toolL, leadCurve, TP.matTool.insertWidth, 15, true);
                // If no suitable curve found throw an error
                if (leadIn == null) { newTP.firstP.addWarning("No suitable curve for lead in found."); }
                else
                {
                    leadIn.Reverse();
                    List<ToolPoint> tPts = new List<ToolPoint>();
                    for (int i = 1; i < leadIn.PointCount; i++)
                    {
                        ToolPoint tPt = TP.firstP.deepClone();
                        tPt.pt = leadIn.Point(i);
                        tPts.Add(tPt);
                    }
                    newTP.InsertRange(0, tPts);
                }
                if (insert != String.Empty) { newTP.preCode = newTP.preCode + "\n" + insert; }
                newTP.Additions.insert = false;
            }

            if (TP.Additions.retract)
            {
                PolylineCurve leadOut = findLead(toolL, leadCurve, TP.matTool.insertWidth, 15, false);
                if (leadOut == null) { newTP.lastP.addWarning("No suitable curve for lead out found."); }
                // If no suitable curve found throw an error
                else
                {
                    for (int i = 1; i < leadOut.PointCount; i++)
                    {
                        ToolPoint tPt = TP.firstP.deepClone();
                        tPt.pt = leadOut.Point(i);
                        newTP.Add(tPt);
                    }
                }
                if (retract != String.Empty) { newTP.postCode = newTP.postCode + "\n" + retract; }
                newTP.Additions.retract = false;
            }

            newTP.Additions.leadCurvature = 0;
            return newTP;
        }

        // Adjust the path so it will not be gouged when cut in 3-axis, or indexed 3-axis mode.
        // TODO make this guarantee that it does not gouge locally. There is a problem 
        // with paths that are steep down, followed by some bottom moves followed by steep out. 
        public static ToolPath threeAxisHeightOffset(ToolPath tP, IMachine M)
        {
            if (tP.matTool == null) { matToolException(); }
            List<ToolPoint> offsetPath = new List<ToolPoint>();

            Vector3d travel = (Vector3d)(tP[1].pt - tP[0].pt);
            travel.Unitize();

            ToolPoint point;
            Vector3d orth = Vector3d.CrossProduct(travel, M.toolDir(tP[0]));
            Vector3d uOrth = orth;

            point = tP.matTool.threeAxisHeightOffset(M, tP[0], travel, uOrth);

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
                if (M.toolDir(tP[i - 1]) != M.toolDir(tP[i]) && !changeDirection)
                {
                    tP[i].warning.Add("Height offsetting is not designed for 5-Axis motion, it may be unpredictable.");
                    changeDirection = true;
                }
                // Find the next offset line
                travel = (Vector3d)(tP[i + 1].pt - tP[i].pt);
                orth = Vector3d.CrossProduct(travel, M.toolDir(tP[0]));
                if (orth.Length != 0) { uOrth = orth; }

                nextPoint = tP.matTool.threeAxisHeightOffset(M, tP[i], travel, uOrth);

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

            orth = Vector3d.CrossProduct(travel, M.toolDir(tP.lastP));
            if (orth.Length != 0) { uOrth = orth; }
            offsetPath.Add(tP.matTool.threeAxisHeightOffset(M, tP.lastP, travel, uOrth));

            ToolPath retPath = tP.deepCloneWithNewPoints(offsetPath);
            retPath.Additions.threeAxisHeightOffset = false;

            if (!retPath.Additions.insert)
            { retPath.firstP.warning.Add("Height Offsetting does not work between ToolPaths. This might cause unexpected behaviour."); }
            if (!retPath.Additions.retract)
            { retPath.lastP.warning.Add("Height Offsetting does not work between ToolPaths. This might cause unexpected behaviour."); }

            return retPath;
        }
        
        // Clear the addition but don't do anything
        public static ToolPath clearThreeAxisHeightOffset(ToolPath tP)
        {
            ToolPath newTP = tP.deepClone();
            newTP.Additions.threeAxisHeightOffset = false;
            return newTP;
        }

        // Add a finishing versions of the path (including onion Skinning)
        public static List<ToolPath> finishPaths(ToolPath tP, IMachine M)
        {
            var fP = new List<ToolPath>();
            // get the sorted list of onion cuts
            IOrderedEnumerable<double> onionSort = tP.Additions.sortOnion;

            foreach (double height in onionSort)
            {
                ToolPath newTP = tP.deepClone(height, M);
                if(newTP.name != String.Empty) { newTP.name = newTP.name + " "; }
                newTP.name = newTP.name + "(Finish at height " + height.ToString("0.###") + ")";
                newTP.Additions.stepDown = false;
                newTP.Additions.onion = new List<double>() { 0 };
                fP.Add(newTP);
            }

            // If onion is empty add the ToolPath anyway. 
            if(fP.Count == 0 )
            {
                ToolPath newTP = tP.deepClone();
                newTP.Additions.stepDown = false;
                newTP.Additions.onion = new List<double>() { 0 };
                fP.Add(newTP);
            }
            return fP;
        }

        // The finish path is just the toolpath
        internal static List<ToolPath> oneFinishPath(ToolPath tP)
        {
            ToolPath newTP = tP.deepClone();
            newTP.Additions.stepDown = false;
            newTP.Additions.onion = new List<double>() { 0 };
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

        static public void gcInstStart(IGCodeMachine M, ref CodeInfo Co, MachineInstruction MI, ToolPath startPath)
        {
            if (MI[0][0].matTool == null) { matToolException(); }
            if (MI[0][0].matForm == null) { matFormException(); }
            Co.currentMT = MI[0][0].matTool;
            Co.currentMF = MI[0][0].matForm;

            DateTime thisDay = DateTime.Now;
            Co.appendLineNoNum(M.fileStart);
            Co.appendComment(M.sectionBreak);
            if (MI.name != string.Empty) { Co.appendComment(MI.name); }
            Co.appendComment("");
            Co.appendComment(" Machine Instructions Created " + thisDay.ToString("f"));
            Co.appendComment("  by " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " "
                + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            if (M.name != string.Empty) { Co.appendComment("  for " + M.name); }
            Co.appendComment(" Starting with: ");
            Co.appendComment("  Tool: " + MI[0][0].matTool.toolName);
            Co.appendComment("  in " + MI[0][0].matTool.matName + " with shape " + MI[0][0].matForm.ToString());
            Co.appendComment("");
            Co.appendComment(M.sectionBreak);
            Co.append(M.header);
            Co.append(MI.preCode);
            Co.currentMT = new MaterialTool(); // Clear the tool information so we call a tool change. 
            M.writeCode(ref Co, startPath);
        }
        static public void gcInstEnd(IGCodeMachine M, ref CodeInfo Co, MachineInstruction MI, ToolPath finalPath, ToolPath endPath)
        {
            Co.appendComment(M.sectionBreak);
            M.writeTransition(ref Co, finalPath, endPath, true);
            M.writeCode(ref Co, endPath);

            Co.appendComment(M.sectionBreak);
            Co.appendComment(" End of ToolPaths");
            Co.appendComment(M.sectionBreak);

            Co.append(MI.postCode);
            Co.append(M.footer);
            Co.appendLineNoNum(M.fileEnd);
        }

        static public void gcOpStart(IGCodeMachine M, ref CodeInfo Co, MachineOperation MO)
        {
            Co.appendComment(M.sectionBreak);
            Co.appendComment("");
            Co.appendComment(" Operation: " + MO.name);
            Co.appendComment("");
            Co.append(MO.preCode);
        }
        static public void gcOpEnd(IGCodeMachine M, ref CodeInfo Co, MachineOperation MO)
        {
            Co.appendComment(MO.postCode);
        }

        static public TPchanges gcPathStart(IGCodeMachine M, ref CodeInfo Co, ToolPath tP)
        {
            if (tP.matTool == null) { matToolException(); }
            if (tP.matForm == null) { matFormException(); }
            TPchanges ch = new TPchanges(false, false);
            Co.appendComment(M.sectionBreak);
            bool preamble = false;
            if (tP.name != string.Empty)
            {
                Co.appendComment(" ToolPath: " + tP.name);
                preamble = true;
            }
            if (tP.matTool != null && tP.matTool.toolName != Co.currentMT.toolName)
            {
                Co.appendComment(" using: " + tP.matTool.toolName + " into " + tP.matTool.matName);
                if (M.toolLengthCompensation && tP.matTool.toolNumber != Co.currentMT.toolNumber) {
                    Co.append(M.toolChangeCommand + tP.matTool.toolNumber);
                }
                Co.currentMT = tP.matTool;
                ch.mT = true;
                preamble = true;
            }
            if (tP.matForm != null && tP.matForm.ToString() != Co.currentMF.ToString())
            {
                Co.appendComment(" material: " + tP.matForm.ToString());
                Co.currentMF = tP.matForm;
                ch.mF = true;
                preamble = true;
            }

            if (preamble) { Co.appendComment(M.sectionBreak); }

            Co.append(tP.preCode);
            return ch;
        }
        static public void gcPathEnd(IGCodeMachine M, ref CodeInfo Co, ToolPath TP)
        {
            Co.append(TP.postCode);
        }

        // Toolpoint writers
        // These might be simpler to pull 
        // into a single "write" command taking a dictionary?
        static public string gcTwoAxis(ToolPoint TP)
        {
            Point3d OP = TP.pt;
            string GPoint = string.Empty;
            GPoint += "X" + OP.X.ToString("0.000") + " Y" + OP.Y.ToString("0.000");

            return GPoint;
        }
        static public string gcThreeAxis(ToolPoint TP)
        {
            Point3d OP = TP.pt;
            string GPoint = string.Empty;
            GPoint += "X" + OP.X.ToString("0.000") + " Y" + OP.Y.ToString("0.000") + " Z" + OP.Z.ToString("0.000");

            return GPoint;
        }
        static public string gcFiveAxisAB(Point3d machPt, Vector3d AB)
        {
            StringBuilder GPtBd = new StringBuilder(@"X", 34);
            GPtBd.Append(machPt.X.ToString("0.000"));
            GPtBd.Append(@" Y"); GPtBd.Append(machPt.Y.ToString("0.000"));
            GPtBd.Append(@" Z"); GPtBd.Append(machPt.Z.ToString("0.000"));
            GPtBd.Append(@" A"); GPtBd.Append((180.0 * AB.X / Math.PI).ToString("0.000"));
            GPtBd.Append(@" B"); GPtBd.Append((180.0 * AB.Y / Math.PI).ToString("0.000"));

            return GPtBd.ToString();
        }

        // GCode reading
        private static readonly Regex numbPattern = new Regex(@"^([0-9\-.]+)", RegexOptions.Compiled);

        static private double getValue(string line, char split, double old, ref bool changed)
        {
            double val = old;
            string[] splitLine = line.Split(split);
            if (splitLine.Length > 1)
            {
                Match monkey = numbPattern.Match(splitLine[1]);
                if (monkey.Success)
                { val = double.Parse(monkey.Value); }
                if (val != old) { changed = true; }
            }
            return val;
        }
        // TODO detect tool changes and new paths
        static public ToolPath gcRead(IGCodeMachine M, List<MaterialTool> MTs, string Code, List<char> terms)
        {
            ToolPath TP = new ToolPath(); Dictionary<char, double> vals = new Dictionary<char, double>();

            foreach (char c in terms) { vals.Add(c, 0); }

            bool changed;

            using (StringReader reader = new StringReader(Code))
            {
                // Loop over the lines in the string.
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    changed = false;
                    foreach (char t in terms)
                    { vals[t] = getValue(line, t, vals[t], ref changed); }
                    //interpret a G0 command.
                    if (line.Contains(@"G00") || line.Contains(@"G0 "))
                    {
                        if (vals.ContainsKey('F') && vals['F'] != 0)
                        {
                            changed = true;
                            vals['F'] = 0;
                        }
                    }
                    MaterialTool uMT = new MaterialTool();
                    if(MTs.Count > 0) { uMT = MTs[0]; }
                    if (changed) { TP.Add(M.readTP(vals, uMT)); }
                }
            }
            return TP;
        }

        public static string comment(IGCodeMachine M, string L)
        {
            if (L == "" || L == " ") { return " "; }
            else
            {
                string uL = L;
                // Avoid "nested comments"
                if(M.commentStart == "(")
                {
                    uL = L.Replace('(', ']');
                    uL = uL.Replace(')', ']');
                }
                return M.commentStart + " " + uL + " " + M.commentEnd;
            }
        }
    }
}