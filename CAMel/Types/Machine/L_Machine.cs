namespace CAMel.Types.Machine
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using CAMel.Types.MaterialForm;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    // Some standards to help develop GCode based machines
    /// <summary>TODO The GCodeMachine interface.</summary>
    public interface IGCodeMachine : IMachine
    {
        /// <summary>Gets the header.</summary>
        [NotNull]
        string header { get; }
        /// <summary>Gets the footer.</summary>
        [NotNull]
        string footer { get; }

        /// <summary>Gets the speed change command.</summary>
        [NotNull, UsedImplicitly]
        string speedChangeCommand { get; }
        /// <summary>Gets the tool change command.</summary>
        [NotNull]
        string toolChangeCommand { get; }

        /// <summary>Gets the section break.</summary>
        [NotNull]
        string sectionBreak { get; }
        /// <summary>Gets the file start.</summary>
        [NotNull]
        string fileStart { get; }
        /// <summary>Gets the file end.</summary>
        [NotNull]
        string fileEnd { get; }
        /// <summary>Gets the comment start.</summary>
        [NotNull]
        string commentStart { get; }
        /// <summary>Gets the comment end.</summary>
        [NotNull]
        string commentEnd { get; }

        /// <summary>TODO The read tp.</summary>
        /// <param name="values">TODO The values.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <returns>The <see cref="ToolPoint"/>.</returns>
        [NotNull]
        ToolPoint readTP([NotNull] Dictionary<char, double> values, [NotNull] MaterialTool mT);
    }

    /// <summary>TODO The kinematics.</summary>
    public static class Kinematics
    {
        // Collection of Inverse Kinematics

        // 2-Axis and 3-Axis don't need any work, so they just need writing functions
        // in the GCode library, plus a general purpose linear interpolation.
        /// <summary>TODO The interpolate linear.</summary>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="p">TODO The p.</param>
        /// <returns>The <see cref="ToolPoint"/>.</returns>
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
        /// <summary>TODO The ik five axis ab table.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="pivot">TODO The pivot.</param>
        /// <param name="toolLength">TODO The tool length.</param>
        /// <param name="machPt">TODO The mach pt.</param>
        /// <returns>The <see cref="Vector3d"/>.</returns>
        public static Vector3d ikFiveAxisABTable([NotNull] ToolPoint tP, Vector3d pivot, double toolLength, out Point3d machPt)
        {
            // Always gives B from -pi to pi and A from -pi/2 to pi/2.
            double ao = Math.Asin(tP.dir.Y);
            double bo = Math.Atan2(-tP.dir.X, tP.dir.Z);

            if (ao > Math.PI / 2.0)
            {
                ao = Math.PI - ao;
                bo -= Math.PI;
                if (bo < 0) { bo += 2.0 * Math.PI; }
            }

            if (ao < -Math.PI / 2.0)
            {
                ao = Math.PI - ao;
                bo -= Math.PI;
                if (bo < 0) { bo += 2.0 * Math.PI; }
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

        /// <summary>TODO The k five axis ab table.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="pivot">TODO The pivot.</param>
        /// <param name="toolLength">TODO The tool length.</param>
        /// <param name="machPt">TODO The mach pt.</param>
        /// <param name="ab">TODO The ab.</param>
        /// <returns>The <see cref="ToolPoint"/>.</returns>
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
        /// <summary>TODO The interpolate five axis ab table.</summary>
        /// <param name="pivot">TODO The pivot.</param>
        /// <param name="toolLength">TODO The tool length.</param>
        /// <param name="from">TODO The from.</param>
        /// <param name="to">TODO The to.</param>
        /// <param name="p">TODO The p.</param>
        /// <param name="lng">TODO The lng.</param>
        /// <returns>The <see cref="ToolPoint"/>.</returns>
        [NotNull]
        public static ToolPoint interpolateFiveAxisABTable(Vector3d pivot, double toolLength, [NotNull] ToolPoint from, [NotNull] ToolPoint to, double p, bool lng)
        {
            Vector3d fromAB = ikFiveAxisABTable(from, pivot, toolLength, out Point3d fromMachPt);
            Vector3d toAB = ikFiveAxisABTable(to, pivot, toolLength, out Point3d toMachPt);

            Point3d outPt = (1 - p) * from.pt + p * to.pt;
            Vector3d outAB = (1 - p) * fromAB + p * toAB;
            ToolPoint tP;

            // switch to long way round or short way round depending on gap between angles
            if ((!lng || !(Math.Abs(fromAB.Y - toAB.Y) <= Math.PI)) && (lng || !(Math.Abs(fromAB.Y - toAB.Y) > Math.PI)))
            {
                tP = kFiveAxisABTable(from, pivot, toolLength, outPt, outAB);
                tP.pt = outPt;
                return tP;
            }

            Vector3d alt = fromAB.Y > toAB.Y ? new Vector3d(0, 2 * Math.PI, 0) : new Vector3d(0, -2 * Math.PI, 0);
            outAB = (1 - p) * fromAB + p * (toAB + alt);

            tP = kFiveAxisABTable(from, pivot, toolLength, outPt, outAB);
            tP.pt = outPt;
            return tP;
        }

        /// <summary>TODO The ang diff five axis ab table.</summary>
        /// <param name="pivot">TODO The pivot.</param>
        /// <param name="toolLength">TODO The tool length.</param>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="lng">TODO The lng.</param>
        /// <returns>The <see cref="double"/>.</returns>
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

        /// <summary>TODO The angle refine.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="maxAngle">TODO The max angle.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        [NotNull]
        public static ToolPath angleRefine([NotNull] IMachine m, [NotNull] ToolPath tP, double maxAngle)
        {
            if (tP.matTool == null) { Exceptions.matToolException(); }
            if (tP.Count < 2) { return tP.deepClone(); }

            ToolPath sRef = tP.deepCloneWithNewPoints(new List<ToolPoint>());
            sRef.Add(tP[0]);

            for (int i = 1; i < tP.Count; i++)
            {
                double angCh = m.angDiff(tP[i - 1], tP[i], tP.matTool, false);
                if (angCh > maxAngle)
                {
                    int newSt = (int)Math.Ceiling(angCh / maxAngle);
                    for (int j = 1; j < newSt; j++)
                    {
                        sRef.Add(m.interpolate(tP[i - 1], tP[i], tP.matTool, j / (double)newSt, false));
                    }
                }

                sRef.Add(tP[i]);
            }

            return sRef;
        }
        /// <summary>Find rotation around X. </summary>
        /// <param name="pl">Frame (Plane) to convert to angles.</param>
        /// <param name="cen">Out parameter giving the centre of rotation. </param>
        /// <returns>The rotation around X (X coefficient) and rotation of X axis (Y coefficient). <see cref="Vector3d"/>.</returns>
        public static Vector3d xRotation(Plane pl, out Point3d cen)
        {
            Plane uPl = pl;

            if (uPl.XAxis == -Vector3d.XAxis) { uPl.Rotate(Math.PI, uPl.ZAxis); }

            // Check the X Axis has not changed
            double xAng = Vector3d.VectorAngle(Vector3d.XAxis, pl.XAxis);

            // Find rotation around X axis
            double rot = Math.Atan2(-uPl.ZAxis.Y, uPl.ZAxis.Z);

            Vector3d YZshift = (Vector3d)uPl.Origin;
            YZshift.X = 0;
            cen = Point3d.Origin;
            if (rot != 0)
            {
                double radius = YZshift.Length / (2.0 * Math.Cos(rot / 2.0));
            }

            return new Vector3d(rot, xAng, 0);
        }
    }

    /// <summary>Collections of useful functions to create machines.</summary>
    public static class Utility
    {
        // planeOffset works with self-intersection of a closed curve
        // It looses possible toolpoint information and uses toolDir
        // for all points
        /// <summary>Offset a path on a plane</summary>
        /// <param name="tP">Toolpath to offset</param>
        /// <param name="toolDir">Tool dorection for offset path</param>
        /// <returns>Offset Paths</returns>
        [NotNull]
        public static List<ToolPath> planeOffset([NotNull] ToolPath tP, Vector3d toolDir)
        {
            if (tP.additions.offset.SquareLength < CAMel_Goo.Tolerance) { return new List<ToolPath> { tP }; }

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
            if (uC.ClosedCurveOrientation(Transform.Identity) == CurveOrientation.Clockwise)
            {
                uC.Reverse();
                reversed = true;
                uOS = -uOS;
            }

            // record the average Z location of the curve
            BoundingBox bb = uC.GetBoundingBox(true);
            double useZ = (bb.Max.Z + bb.Min.Z) / 2.0;

            // offSet
            List<PolylineCurve> osC = Offsetting.offset(uC, uOS);

            if (reversed) { foreach (PolylineCurve osPl in osC) { osPl.Reverse(); } }

            // create Operation
            List<ToolPath> tPs = new List<ToolPath>();

            int i = 1;
            foreach (PolylineCurve osPl in osC)
            {
                // Create and add name, material/tool and material form
                ToolPath osTP = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                osTP.additions.offset = Vector3d.Zero;
                osTP.name += " -offset-";

                if (osC.Count > 1) { osTP.name = osTP.name + " " + i; }
                i++;

                // return to original orientation
                osPl.Translate(new Vector3d(0, 0, useZ));
                osPl.Transform(Transform.PlaneToPlane(Plane.WorldXY, p));

                // Add to Operation
                osTP.convertCurve(osPl, toolDir);
                tPs.Add(osTP);
            }

            return tPs;
        }

        /// <summary>Offset a path locally</summary>
        /// <param name="tP">ToolPath to offset</param>
        /// <returns>Offset paths.</returns>
        [NotNull]
        public static List<ToolPath> localOffset([NotNull] ToolPath tP)
        {
            List<ToolPoint> oTPts = new List<ToolPoint>();
            Vector3d os = tP.additions.offset;
            double osL = os.Length;
            os.Unitize();

            // Check if there is enough to offset
            if (osL < CAMel_Goo.Tolerance || tP.Count < 2 || tP.firstP == null || tP.lastP == null) { return new List<ToolPath> { tP }; }

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
                uTPt.pt += osL * osD;
                oTPts.Add(uTPt);
                lPt = tP[i];
            }

            // Loop back to start if closed.
            ToolPoint nP = tP.lastP;
            if (tP.firstP.pt.DistanceTo(tP.lastP.pt) < CAMel_Goo.Tolerance) { nP = tP[2]; }

            uTPt = tP[tP.Count - 1].deepClone();
            osD = Vector3d.CrossProduct(os, nP.pt - lPt.pt);
            osD.Unitize();
            uTPt.pt += osL * osD;
            oTPts.Add(uTPt);

            ToolPath oTP = tP.deepCloneWithNewPoints(oTPts);

            oTP.additions.offset = Vector3d.Zero;

            return new List<ToolPath> { oTP };
        }

        // Step down into material
        /// <summary>TODO The step down.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="m">TODO The m.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
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

            // ReSharper disable once AssignNullToNotNullAttribute
            foreach (MFintersection inter in refPath.TakeWhile(tPt => tPt != null).Select(tPt => tP.matForm.intersect(tPt, 0).through))
            {
                matDist.Add(inter.lineP); // distance to material surface
                if (matDist[matDist.Count - 1] < 0) { matDist[matDist.Count - 1] = 0; } // avoid negative distances (outside material)

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
                tempTP.additions.stepDown = 0;
                tempTP.additions.onion = new List<double> { 0 };
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
                            else // check length of drop
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
                                    tempTP.additions.stepDown = 0;
                                    tempTP.additions.onion = new List<double> { 0 };
                                    tempTP.label = PathLabel.RoughCut;

                                    // add k-1 point as k is deep
                                    // this will not result in a double point as we checked (k-j) >=3
                                    tPt = refPath[k - 1].deepClone();
                                    height = finishDepth;
                                    tPt.pt = m.toolDir(tPt) * height + tPt.pt;
                                    tempTP.Add(tPt);
                                    j = k - 1; // set j to k-1 so it deals with the k point next
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

        /// <summary>TODO The find lead.</summary>
        /// <param name="toolL">TODO The tool l.</param>
        /// <param name="leadCurve">TODO The lead curve.</param>
        /// <param name="insertWidth">TODO The insert width.</param>
        /// <param name="v">TODO The v.</param>
        /// <param name="start">TODO The start.</param>
        /// <returns>The <see cref="PolylineCurve"/>.</returns>
        [CanBeNull]
        // ReSharper disable once SuggestBaseTypeForParameter
        private static PolylineCurve findLead([NotNull] PolylineCurve toolL, double leadCurve, double insertWidth, int v, bool start)
        {
            // work out the rotation to get the desired normal
            double normAng = Math.PI / 2.0;

            // now we have the internal normal, flip if we want external.
            if (leadCurve >= 0) { normAng = -normAng; }

            PointContainment incorrectSide = PointContainment.Inside;
            CurveOrientation orient = toolL.ClosedCurveOrientation(Vector3d.ZAxis);

            // ReSharper disable twice ArrangeRedundantParentheses
            if ((orient == CurveOrientation.Clockwise && leadCurve > 0) || (orient == CurveOrientation.CounterClockwise && leadCurve < 0))
            { incorrectSide = PointContainment.Outside; }

            double uLeadCurve = Math.Abs(leadCurve);

            Point3d startPt = toolL.PointAtStart;
            Point3d endPt = toolL.PointAtEnd;

            // Get tangents and the Normal pointing in the direction we want the lead.
            Vector3d startTan = toolL.TangentAtStart;
            Vector3d startNorm = startTan;
            startNorm.Rotate(normAng, Vector3d.ZAxis);

            Vector3d endTan = toolL.TangentAtEnd;
            Vector3d endNorm = endTan;
            endNorm.Rotate(normAng, Vector3d.ZAxis);

            Vector3d uTan, uNorm;
            Point3d uPt;

            if (toolL.IsClosed && !start) // end of closed curve
            {
                uPt = endPt;
                uTan = startTan;
                uNorm = startNorm;
            }
            else if (toolL.IsClosed && start) // start of closed curve
            {
                uPt = startPt;
                uTan = -endTan;
                uNorm = endNorm;
            }
            else if (start) // start of open curve
            {
                uPt = startPt;
                uTan = -startTan;
                uNorm = -startNorm;
            }
            else // end of open curve
            {
                uPt = endPt;
                uTan = endTan;
                uNorm = -endNorm;
            }

            ArcCurve leadCirc = new ArcCurve(new Arc(uPt, uTan, uPt + uLeadCurve * (uNorm + uTan)));

            // step along the arc trying to find a point more that insert distance from the path
            List<double> divs = leadCirc.DivideByCount(v, true)?.ToList() ?? new List<double>();

            Polyline outP = new Polyline();

            foreach (Point3d testPt in divs.Select(d => leadCirc.PointAt(d)))
            {
                outP.Add(testPt);
                //if (toolL.Contains(testPt) == incorrectSide) { return null; }
                toolL.ClosestPoint(testPt, out double testDist);
                testDist = testPt.DistanceTo(toolL.PointAt(testDist));
                if (testDist > insertWidth * 0.52) { return new PolylineCurve(outP); }
            }

            // Now try to keep going straight
            for (double i = insertWidth / 10.0; i < 2 * insertWidth; i += insertWidth / 10.0)
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

        /// <summary>TODO The lead in u.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="activate">TODO The activate.</param>
        /// <param name="deActivate">TODO The de activate.</param>
        /// <param name="irActivate">TODO The ir activate.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        public static List<ToolPath> leadInU([NotNull] ToolPath tP, [NotNull] string activate = "", bool applyOpen = false,  int irActivate = 0)
        {
            // Will add insert path as new ToolPath
            List<ToolPath> irTps = new List<ToolPath>();

            // Update the main toolpath
            ToolPath newTP = tP.deepClone();
            if (tP.matTool == null) { Exceptions.matToolException(); }
            newTP.additions.insert = false;

            irTps.Add(newTP);

            double leadCurve = tP.additions.leadComm[0];
            if(tP.side == CutSide.Right) { leadCurve = -leadCurve; }

            // add insert path if needed
            if (tP.additions.insert && Math.Abs(leadCurve) > CAMel_Goo.Tolerance && (applyOpen || tP.isClosed()))
            {
                PolylineCurve toolL = tP.getLine();
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
                    iTp.name += " insert";
                    iTp.label = PathLabel.Insert;
                    if (tP.additions.activate != 0) { iTp.additions.activate = irActivate; }
                    iTp.additions.retract = false;
                    iTp.AddRange(tPts);

                    if (iTp.Count > 0) { irTps.Insert(0, iTp); }
                }
            }

            // Add activation codes
            // ReSharper disable once InvertIf
            if (tP.additions.activate != 0 && activate != string.Empty && irTps[0] != null)
            {
                irTps[0].preCode = activate + "\n" + irTps[0].preCode; 
            }

            return irTps;
        }

        /// <summary>TODO The lead out u.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="activate">TODO The activate.</param>
        /// <param name="deActivate">TODO The de activate.</param>
        /// <param name="irActivate">TODO The ir activate.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        public static List<ToolPath> leadOutU([NotNull] ToolPath tP, [NotNull] string deActivate = "", bool applyOpen = false, int irActivate = 0)
        {
            // Will add retract path as new ToolPath
            List<ToolPath> irTps = new List<ToolPath>();

            // Update the main toolpath
            ToolPath newTP = tP.deepClone();
            if (tP.matTool == null) { Exceptions.matToolException(); }
            newTP.additions.retract = false;

            irTps.Add(newTP);

            double leadCurve = tP.additions.leadComm[0];
            if (tP.side == CutSide.Right) { leadCurve = -leadCurve; }

            // Add retract path if needed 
            if (tP.additions.retract && Math.Abs(leadCurve) > CAMel_Goo.Tolerance && (applyOpen || tP.isClosed()))
            {
                PolylineCurve toolL = tP.getLine();
                PolylineCurve leadOut = findLead(toolL, leadCurve, tP.matTool.insertWidth, 15, false);

                // If no suitable curve found throw an error
                if (leadOut == null) { newTP.lastP?.addWarning("No suitable curve for lead out found."); }
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
                    rTp.name += " retract";
                    rTp.label = PathLabel.Retract;
                    if (tP.additions.activate != 0) { rTp.additions.activate = irActivate; }
                    rTp.AddRange(tPts);

                    if (rTp.Count > 0) { irTps.Add(rTp); }
                }
            }

            // Add deactivation codes
            // ReSharper disable once InvertIf
            if (tP.additions.activate != 0 && deActivate != string.Empty && irTps[irTps.Count - 1] != null)
            {
                // ReSharper disable once PossibleNullReferenceException
                irTps[irTps.Count - 1].postCode = irTps[irTps.Count - 1].postCode + "\n" + deActivate;
            }

            return irTps;
        }

        /// <summary>TODO The lead in v.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="activate">TODO The activate.</param>
        /// <param name="deActivate">TODO The de activate.</param>
        /// <param name="irActivate">TODO The ir activate.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        public static List<ToolPath> leadInV([NotNull] ToolPath tP, [NotNull] string activate = "", bool applyOpen = false, int irActivate = 0)
        {
            // Will add insert path as new ToolPath
            List<ToolPath> irTps = new List<ToolPath>();

            // Update the main toolpath
            ToolPath newTP = tP.deepClone();
            if (tP.matTool == null) { Exceptions.matToolException(); }
            newTP.additions.insert = false;

            irTps.Add(newTP);

            double leadCurve = tP.additions.leadComm[0];

            // add insert path if needed
            if (tP.additions.insert && Math.Abs(leadCurve) > CAMel_Goo.Tolerance && (applyOpen || tP.isClosed()))
            {
                PolylineCurve toolL = tP.getLine();
                ToolPath iTp = newTP.deepCloneWithNewPoints(new List<ToolPoint>());
                iTp.name += " insert";
                iTp.label = PathLabel.Insert;
                if (tP.additions.activate != 0) { iTp.additions.activate = irActivate; }
                iTp.additions.retract = false;

                double r = (180.0-leadCurve) * Math.PI / 180.0; // Angle of V in Radians;
                if (tP.side == CutSide.Right) { r = -r; } // cut to the right
                Vector3d tan = toolL.TangentAtStart;
                tan.Rotate(r, Vector3d.ZAxis);
                if (tP.firstP == null) { Exceptions.emptyPathException(); }
                ToolPoint tPt = tP.firstP.deepClone();
                tPt.pt += tan * tP.matTool.insertWidth;

                iTp.Add(tPt);

                irTps.Insert(0, iTp);
            }

            // Add activation codes
            // ReSharper disable once InvertIf
            if (tP.additions.activate != 0 && activate != string.Empty && irTps[0] != null)
            {
                irTps[0].preCode = activate + "\n" + irTps[0].preCode; 
            }

            return irTps;
        }

        /// <summary>TODO The lead out v.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="activate">TODO The activate.</param>
        /// <param name="deActivate">TODO The de activate.</param>
        /// <param name="irActivate">TODO The ir activate.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        public static List<ToolPath> leadOutV([NotNull] ToolPath tP, [NotNull] string deActivate = "", bool applyOpen = false, int irActivate = 0)
        {
            // Will add retract path as new ToolPath
            List<ToolPath> irTps = new List<ToolPath>();

            // Update the main toolpath
            ToolPath newTP = tP.deepClone();
            if (tP.matTool == null) { Exceptions.matToolException(); }
            newTP.additions.retract = false;

            irTps.Add(newTP);

            double leadCurve = tP.additions.leadComm[0];

            // Add retract path if needed 
            if (tP.additions.retract && Math.Abs(leadCurve) > CAMel_Goo.Tolerance && (applyOpen || tP.isClosed()))
            {
                PolylineCurve toolL = tP.getLine();
                ToolPath rTp = newTP.deepCloneWithNewPoints(new List<ToolPoint>());
                rTp.name += " retract";
                rTp.label = PathLabel.Retract;
                if (tP.additions.activate != 0) { rTp.additions.activate = irActivate; }

                double r = leadCurve * Math.PI / 180.0; // Angle of V in Radians;
                if (tP.side == CutSide.Right) { r = -r; } // cut to the right
                Vector3d tan = toolL.TangentAtEnd;
                tan.Rotate(r, Vector3d.ZAxis);
                if (tP.lastP == null) { Exceptions.emptyPathException(); }
                ToolPoint tPt = tP.lastP.deepClone();
                rTp.Add(tPt);
                tPt = tP.lastP.deepClone();
                tPt.pt += tan * tP.matTool.insertWidth;

                rTp.Add(tPt);


                irTps.Add(rTp);
            }

            // Add activation codes
            // ReSharper disable once InvertIf
            if (tP.additions.activate != 0 && deActivate != string.Empty && irTps[irTps.Count - 1] != null)
            {
                // ReSharper disable once PossibleNullReferenceException
                irTps[irTps.Count - 1].postCode = irTps[irTps.Count - 1].postCode + "\n" + deActivate; 
            }

            return irTps;
        }

        /// <summary>TODO The insert.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="activate">TODO The activate.</param>
        /// <param name="deActivate">TODO The de activate.</param>
        /// <param name="irActivate">TODO The ir activate.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        internal static List<ToolPath> insert([NotNull] ToolPath tP, [NotNull] string activate = "", int irActivate = 0)
        {
            // Will add insert path as new ToolPath
            List<ToolPath> iTps = new List<ToolPath>();

            // Update the main toolpath
            ToolPath newTP = tP.deepClone();
            if (tP.matTool == null) { Exceptions.matToolException(); }
            if (tP.matForm == null) { Exceptions.matFormException(); }
            newTP.additions.insert = false;

            MFintersection inter;

            double uTol = tP.matForm.safeDistance * 1.05;
            ToolPoint tempTPt;

            iTps.Add(newTP);

            // check if we have something to do
            if (tP.additions.insert && newTP.Count > 0) // add insert
            {
                ToolPath iTp = newTP.deepCloneWithNewPoints(new List<ToolPoint>());
                iTp.additions = new ToolPathAdditions(); // no additions needed for an insert path.
                iTp.name += " insert";
                iTp.label = PathLabel.Insert;

                // note we do this backwards adding points to the start of the path.
                if (tP.additions.activate != 0) { iTp.additions.activate = irActivate; }

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
                    tempTPt.pt += inter.away * uTol;
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
                    } // otherwise nothing needs to be added as we do not interact with material
                }

                if (iTp.Count > 0) { iTps.Insert(0, iTp); }
            }

            // add activation codes
            if (tP.additions.activate != 0 && activate != string.Empty && iTps[0] != null)
            {
                iTps[0].preCode = activate + "\n" + iTps[0].preCode;
            }

            return iTps;
        }

        /// <summary>TODO The retract.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="activate">TODO The activate.</param>
        /// <param name="deActivate">TODO The de activate.</param>
        /// <param name="irActivate">TODO The ir activate.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        internal static List<ToolPath> retract([NotNull] ToolPath tP, [NotNull] string deActivate = "", int irActivate = 1)
        {
            // Will add insert and retract paths as new ToolPaths
            List<ToolPath> rTps = new List<ToolPath>();

            // Update the main toolpath
            ToolPath newTP = tP.deepClone();
            if (tP.matTool == null) { Exceptions.matToolException(); }
            if (tP.matForm == null) { Exceptions.matFormException(); }
            newTP.additions.retract = false;

            MFintersection inter;

            double uTol = tP.matForm.safeDistance * 1.05;
            ToolPoint tempTPt;

            rTps.Add(newTP);

            if (!tP.additions.retract || newTP.Count <= 0) { return rTps; }
            if (newTP.lastP == null) { Exceptions.emptyPathException(); }

            ToolPath rTp = newTP.deepCloneWithNewPoints(new List<ToolPoint>());
            rTp.additions = new ToolPathAdditions(); // no additions needed for an retract path.
            rTp.name += " retract";
            rTp.label = PathLabel.Retract;

            // note we do this backwards adding points to the start of the path.
            if (tP.additions.activate != 0) { rTp.additions.activate = irActivate; }

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
                tempTPt.pt += inter.away * uTol;
                tempTPt.feed = 0; // we can use a rapid move
                rTp.Add(tempTPt);
            }
            else
            {
                // check intersection with material extended to safe distance
                inter = tP.matForm.intersect(newTP.lastP, uTol).through;
                if (!inter.isSet) { return rTps; }

                // Replace last point of toolpath
                tempTPt = newTP.lastP.deepClone();
                newTP.removeLast();

                // set speed to the plunge feed rate.
                tempTPt.feed = tP.matTool.feedPlunge;

                rTp.Add(tempTPt);

                // point out at safe distance
                tempTPt = newTP.lastP?.deepClone() ?? new ToolPoint();
                tempTPt.pt = inter.point;
                tempTPt.feed = 0; // we can use a rapid move
                rTp.Add(tempTPt);
            }

            if (rTp.Count > 0) { rTps.Add(rTp); }

            // Add deactivation codes
            // ReSharper disable once InvertIf
            if (tP.additions.activate != 0 && deActivate != string.Empty && rTps[rTps.Count - 1] != null)
            {
                // ReSharper disable once PossibleNullReferenceException
                rTps[rTps.Count - 1].postCode = rTps[rTps.Count - 1].postCode + "\n" + deActivate;
            }
            return rTps;
        }

        internal static List<ToolPath> transition([NotNull] IMachine m, [NotNull] ToolPath fP, [NotNull] ToolPath tP, bool retractQ = true, bool insertQ = true)
        {
            List<ToolPath> trans = new List<ToolPath>();

            List<ToolPath> retr = m.retract(fP);
            List<ToolPath> inse = m.insert(tP);

            ToolPath tr = m.transitionPath(retr[retr.Count - 1], inse[0]);

            if (retractQ) {trans.AddRange(retr);}
            trans.Add(tr);
            if (insertQ) { trans.AddRange(inse); }

            return trans;
        }

        // Adjust the path so it will not be gouged when cut in 3-axis, or indexed 3-axis mode.
        // TODO make this guarantee that it does not gouge locally. There is a problem
        // with paths that are steep down, followed by some bottom moves followed by steep out.
        /// <summary>TODO The three axis height offset.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="m">TODO The m.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
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
        /// <summary>TODO The clear three axis height offset.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        [NotNull]
        public static ToolPath clearThreeAxisHeightOffset([NotNull] ToolPath tP)
        {
            ToolPath newTP = tP.deepClone();
            newTP.additions.threeAxisHeightOffset = false;
            return newTP;
        }

        // Add a finishing versions of the path (including onion Skinning)
        /// <summary>TODO The finish paths.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="m">TODO The m.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        public static List<ToolPath> finishPaths([NotNull] ToolPath tP, [NotNull] IMachine m)
        {
            List<ToolPath> fP = new List<ToolPath>();

            // get the sorted list of onion cuts
            IOrderedEnumerable<double> onionSort = tP.additions.sortOnion;

            foreach (double height in onionSort)
            {
                ToolPath newTP = tP.deepClone(height, m);
                if (newTP.name != string.Empty) { newTP.name += " "; }
                newTP.name = newTP.name + "Finish at height " + height.ToString("0.###");
                newTP.label = PathLabel.FinishCut;
                newTP.additions.stepDown = 0;
                newTP.additions.onion = new List<double> { 0 };
                fP.Add(newTP);
            }

            // If onion is empty add the ToolPath anyway.
            if (fP.Count != 0) { return fP; }
            {
                ToolPath newTP = tP.deepClone();
                newTP.additions.stepDown = 0;
                newTP.additions.onion = new List<double> { 0 };
                newTP.label = PathLabel.FinishCut;
                fP.Add(newTP);
            }

            return fP;
        }

        // The finish path is just the toolpath
        /// <summary>TODO The one finish path.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        internal static List<ToolPath> oneFinishPath([NotNull] ToolPath tP)
        {
            ToolPath newTP = tP.deepClone();
            newTP.additions.stepDown = 0;
            newTP.additions.onion = new List<double> { 0 };
            newTP.label = PathLabel.FinishCut;
            return new List<ToolPath> { newTP };
        }

        // Check for jumps in material, return
        // 0 if not in material
        // positive if in material
        // -1 if one of the paths has 0 points
        /// <summary>TODO The jump check.</summary>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="double"/>.</returns>
        internal static double jumpCheck([NotNull] ToolPath fP, [NotNull] ToolPath tP)
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
            // ReSharper disable once InvertIf
            if (fP.matForm.intersect(fP.lastP, fP.matForm.safeDistance).thrDist > 0.0001
                && tP.matForm.intersect(fP.lastP, tP.matForm.safeDistance).thrDist > 0.0001 ||
                fP.matForm.intersect(tP.firstP, fP.matForm.safeDistance).thrDist > 0.0001
                && tP.matForm.intersect(tP.firstP, tP.matForm.safeDistance).thrDist > 0.0001)
            {
                // We trust insert and retract moves and retract to transitions.
                if (fP.label == PathLabel.Insert
                    || tP.label == PathLabel.Retract
                    || fP.label == PathLabel.Retract && tP.label == PathLabel.Transition
                    || fP.label == PathLabel.Retract && tP.label == PathLabel.Insert)
                { return 0; }

                // return distance in material
                double length = fP.lastP.pt.DistanceTo(tP.firstP.pt);

                return length;
            }

            return 0;
        }

        // Check travel between toolpaths
        /// <summary>TODO The jump check.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="m">TODO The m.</param>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        internal static void jumpCheck(ref CodeInfo co, [NotNull] IMachine m, [NotNull] ToolPath fP, [NotNull] ToolPath tP)
        {
            // check if there is a problem moving between paths
            double length = jumpCheck(fP, tP);
            if (length > fP.matTool?.pathJump)
            {
                co.addWarning(
                    "Long Transition between paths in material. \n"
                    + "To remove this error, don't use ignore, instead change PathJump for the material/tool from: "
                    + fP.matTool.pathJump + " to at least: " + (length + .01).ToString("0.00"));
            }
        }

        // Assume all moves are fine
        /// <summary>TODO The no check.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="m">TODO The m.</param>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        internal static void noCheck(ref CodeInfo co, [NotNull] IMachine m, [NotNull] ToolPath fP, [NotNull] ToolPath tP) { }
    }

    /// <summary>TODO The g code.</summary>
    public static class GCode
    {
        // Standard terms
        /// <summary>TODO The default comment start.</summary>
        [NotNull] internal const string DefaultCommentStart = "(";
        /// <summary>TODO The default comment end.</summary>
        [NotNull] internal const string DefaultCommentEnd = ")";
        /// <summary>TODO The default section break.</summary>
        [NotNull] internal const string DefaultSectionBreak = "";
        /// <summary>TODO The default speed change command.</summary>
        [NotNull] internal const string DefaultSpeedChangeCommand = "M03";
        /// <summary>TODO The default tool change command.</summary>
        [NotNull] internal const string DefaultToolChangeCommand = "G43H#";
        /// <summary>TODO The default activate command.</summary>
        [NotNull] internal const string DefaultActivateCommand = "M61";
        /// <summary>TODO The default de activate command.</summary>
        [NotNull] internal const string DefaultDeActivateCommand = "M62";
        /// <summary>TODO The default file start.</summary>
        [NotNull] internal const string DefaultFileStart = "";
        /// <summary>TODO The default file end.</summary>
        [NotNull] internal const string DefaultFileEnd = "";
        /// <summary>TODO The default extension.</summary>
        [NotNull] internal const string DefaultExtension = "nc";

        // Formatting structure for GCode
        /// <summary>TODO The gc line number.</summary>
        /// <param name="l">TODO The l.</param>
        /// <param name="line">TODO The line.</param>
        /// <returns>The <see cref="string"/>.</returns>
        [NotNull]
        public static string gcLineNumber([NotNull] string l, int line) => "N" + line.ToString("0000") + "0 " + l;

        /// <summary>TODO The gc inst start.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="co">TODO The co.</param>
        /// <param name="mI">TODO The m i.</param>
        public static void gcInstStart([NotNull] IGCodeMachine m, [NotNull] ref CodeInfo co, [NotNull] MachineInstruction mI)
        {
            if (mI[0].Count == 0) { Exceptions.noToolPathException(); }
            if (mI[0][0].matTool == null) { Exceptions.matToolException(); }
            if (mI[0][0].matForm == null) { Exceptions.matFormException(); }

            co.currentMT = mI[0][0].matTool;
            co.currentMF = mI[0][0].matForm;

            DateTime thisDay = DateTime.Now;
            co.appendLineNoNum(m.fileStart);
            if (mI.name != string.Empty) { co.appendComment(mI.name); }
            co.appendComment(" Machine Instructions Created " + thisDay.ToString("f"));
            System.Reflection.AssemblyName camel = System.Reflection.Assembly.GetExecutingAssembly().GetName();
            DateTime buildTime = new DateTime(2000, 1, 1)
                                 + new TimeSpan(camel.Version?.Build ?? 0, 0, 0, 0)
                                 + TimeSpan.FromSeconds((camel.Version?.Revision ?? 0) * 2);

            co.appendComment(
                "  by " + camel.Name + " "
                + camel.Version?.ToString(2)
                + " built " + buildTime.ToString("U"));
            if (m.name != string.Empty) { co.appendComment("  for " + m.name); }
            co.appendComment(" Starting with: ");
            co.appendComment("  Tool: " + mI[0][0].matTool.toolName+ " in " + mI[0][0].matTool.matName);
            co.appendComment("  with shape " + mI[0][0].matForm.ToString());
            co.appendComment(m.sectionBreak);
            co.append(m.header);
            co.append(mI.preCode);
            co.currentMT = MaterialTool.Empty; // Clear the tool information so we call a tool change.
        }

        /// <summary>TODO The gc inst end.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="co">TODO The co.</param>
        /// <param name="mI">TODO The m i.</param>
        public static void gcInstEnd([NotNull] IGCodeMachine m, [NotNull] ref CodeInfo co, [NotNull] MachineInstruction mI)
        {
            co.appendComment(m.sectionBreak);
            co.appendComment(" End of ToolPaths");
            co.appendComment(m.sectionBreak);

            co.append(mI.postCode);
            co.append(m.footer);
            co.appendLineNoNum(m.fileEnd);
        }

        /// <summary>TODO The gc op start.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="co">TODO The co.</param>
        /// <param name="mO">TODO The m o.</param>
        public static void gcOpStart([NotNull] IGCodeMachine m, [NotNull] ref CodeInfo co, [NotNull] MachineOperation mO)
        {
            co.appendComment(m.sectionBreak);
            if (mO.name == String.Empty) { co.appendComment(" Operation"); }
            else { co.appendComment(" Operation: " + mO.name); }
            co.appendComment(m.sectionBreak);
            co.append(mO.preCode);
        }

        // ReSharper disable once UnusedParameter.Global
        /// <summary>TODO The gc op end.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="co">TODO The co.</param>
        /// <param name="mO">TODO The m o.</param>
        public static void gcOpEnd([NotNull] IGCodeMachine m, [NotNull] ref CodeInfo co, [NotNull] MachineOperation mO)
            => co.append(mO.postCode);

        /// <summary>TODO The gc path start.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="co">TODO The co.</param>
        /// <param name="tP">TODO The t p.</param>
        public static void gcPathStart([NotNull] IGCodeMachine m, [NotNull] ref CodeInfo co, [NotNull] ToolPath tP)
        {
            if (tP.matTool == null) { Exceptions.matToolException(); }
            if (tP.matForm == null) { Exceptions.matFormException(); }

            if (tP.name == string.Empty) { co.appendComment("ToolPath"); }
            else { co.appendComment(" ToolPath: " + tP.name); } 

            if (tP.matTool != null && tP.matTool.toolName != co.currentMT.toolName)
            {
                co.appendComment(" using: " + tP.matTool.toolName + " into " + tP.matTool.matName);
                if (tP.matTool.toolNumber != co.currentMT.toolNumber) {
                    m.toolChange(ref co, tP.matTool.toolNumber);
                }

                co.currentMT = tP.matTool;
            }

            if (tP.matForm != null && tP.matForm.ToString() != co.currentMF.ToString())
            {
                co.appendComment(" material: " + tP.matForm.ToString());
                co.currentMF = tP.matForm;
            }

            co.append(tP.preCode);
        }
        // ReSharper disable once UnusedParameter.Global
        /// <summary>TODO The gc path end.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="co">TODO The co.</param>
        /// <param name="tP">TODO The t p.</param>
        public static void gcPathEnd([NotNull] IGCodeMachine m, [NotNull] ref CodeInfo co, [NotNull] ToolPath tP) => co.append(tP.postCode);

        // Toolpoint writers
        // These might be simpler to pull
        // into a single "write" command taking a dictionary?
        /// <summary>TODO The gc two axis.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="string"/>.</returns>
        [NotNull]
        public static string gcTwoAxis([NotNull] ToolPoint tP)
        {
            Point3d op = tP.pt;
            string gPoint = string.Empty;
            gPoint += "X" + op.X.ToString("0.000") + " Y" + op.Y.ToString("0.000");

            return gPoint;
        }

        /// <summary>TODO The gc three axis.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="string"/>.</returns>
        [NotNull]
        public static string gcThreeAxis([NotNull] ToolPoint tP)
        {
            Point3d op = tP.pt;
            string gPoint = string.Empty;
            gPoint += "X" + op.X.ToString("0.000") + " Y" + op.Y.ToString("0.000") + " Z" + op.Z.ToString("0.000");

            return gPoint;
        }

        /// <summary>TODO The gc five axis ab.</summary>
        /// <param name="machPt">TODO The mach pt.</param>
        /// <param name="ab">TODO The ab.</param>
        /// <returns>The <see cref="string"/>.</returns>
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
        /// <summary>TODO The numb pattern.</summary>
        [NotNull] private static readonly Regex NumbPattern = new Regex(@"^([0-9\-.]+)", RegexOptions.Compiled);

        /// <summary>TODO The get value.</summary>
        /// <param name="line">TODO The line.</param>
        /// <param name="split">TODO The split.</param>
        /// <param name="old">TODO The old.</param>
        /// <param name="changed">TODO The changed.</param>
        /// <returns>The <see cref="double"/>.</returns>
        private static double getValue([NotNull] string line, char split, double old, ref bool changed)
        {
            double val = old;
            string[] splitLine = line.Split(split);
            if (splitLine.Length < 2) { return val; }

            // ReSharper disable once AssignNullToNotNullAttribute
            Match monkey = NumbPattern.Match(splitLine[1]);
            if (monkey.Success)
            { val = double.Parse(monkey.Value); }
            if (Math.Abs(val - old) > CAMel_Goo.Tolerance) { changed = true; }
            return val;
        }

        // TODO detect tool changes and new paths
        /// <summary>TODO The gc read.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="mTs">TODO The m ts.</param>
        /// <param name="code">TODO The code.</param>
        /// <param name="terms">TODO The terms.</param>
        /// <returns>The <see cref="MachineInstruction"/>.</returns>
        [NotNull]
        public static MachineInstruction gcRead([NotNull] IGCodeMachine m, [NotNull, ItemNotNull] List<MaterialTool> mTs, [NotNull] string code, [NotNull] List<char> terms)
        {
            ToolPath tP = new ToolPath();
            Dictionary<char, double> values = terms.ToDictionary<char, char, double>(c => c, c => 0);

            using (StringReader reader = new StringReader(code))
            {
                // Loop over the lines in the string.
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    bool changed = false;
                    foreach (char t in terms)
                    { values[t] = getValue(line, t, values[t], ref changed); }

                    // interpret a G0 command.
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

            return new MachineInstruction(m) { new MachineOperation(tP) };
        }

        /// <summary>TODO The comment.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="l">TODO The l.</param>
        /// <returns>The <see cref="string"/>.</returns>
        [NotNull]
        public static string comment([NotNull] IGCodeMachine m, [NotNull] string l)
        {
            if (l == string.Empty || l == " ") { return " "; }

            string uL = l;

            // Avoid "nested comments"
            if (m.commentStart != "(") { return m.commentStart + " " + uL + " " + m.commentEnd; }

            uL = l.Replace('(', ']');
            uL = uL.Replace(')', ']');
            return m.commentStart + " " + uL + " " + m.commentEnd;
        }

        /// <summary>TODO The tool change.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="co">TODO The co.</param>
        /// <param name="toolNumber">TODO The tool number.</param>
        internal static void toolChange([NotNull] IGCodeMachine m, ref CodeInfo co, int toolNumber)
        {
            string[] lines = m.toolChangeCommand.Split(new[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

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