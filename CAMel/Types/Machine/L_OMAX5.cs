namespace CAMel.Types.Machine
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    /// <summary>Control a 5-Axis OMAX Waterjet. </summary>
    public class Omax5 : IMachine
    {
        /// <summary>Gets the name.</summary>
        public string name { get; }
        /// <summary>Gets the m ts.</summary>
        public List<MaterialTool> mTs { get; }
        /// <summary>TODO The extension.</summary>
        public string extension => "omx";

        /// <summary>Gets the tilt max.</summary>
        private double tiltMax { get; }
        /// <summary>Gets a value indicating whether tool length compensation.</summary>
        public bool toolLengthCompensation { get; }
        /// <summary>TODO The default tpa.</summary>
        public ToolPathAdditions defaultTPA => ToolPathAdditions.basicDefault;

        /// <summary>Initializes a new instance of the <see cref="Omax5"/> class.</summary>
        /// <param name="name">TODO The name.</param>
        /// <param name="mTs">TODO The m ts.</param>
        /// <param name="tiltMax">TODO The tilt max.</param>
        public Omax5([NotNull] string name, [NotNull] List<MaterialTool> mTs, double tiltMax)
        {
            this.name = name;
            this.toolLengthCompensation = false;
            this.tiltMax = tiltMax;
            this.mTs = mTs;
        }

        /// <inheritdoc />
        public string TypeDescription => "Instructions for a OMax 5-axis waterjet";

        /// <inheritdoc />
        public string TypeName => "CAMelOMax5";

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        public override string ToString() => this.name;

        // TODO?
        /// <summary>TODO The comment.</summary>
        /// <param name="l">TODO The l.</param>
        /// <returns>The <see cref="string"/>.</returns>
        public string comment(string l) => string.Empty;
        /// <summary>TODO The line number.</summary>
        /// <param name="l">TODO The l.</param>
        /// <param name="line">TODO The line.</param>
        /// <returns>The <see cref="string"/>.</returns>
        public string lineNumber(string l, int line) => l;

        /// <summary>TODO The refine.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        public ToolPath refine(ToolPath tP) => tP.matForm?.refine(tP, this) ?? tP;
        /// <summary>TODO The off set.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        public List<ToolPath> offSet(ToolPath tP) => new List<ToolPath> { tP };
        /// <summary>TODO The insert.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        public List<ToolPath> insert(ToolPath tP)
        {
            bool applyOpen = false;
            if (tP.additions.leadComm[2] > 0) { applyOpen = true; }
            switch (tP.additions.leadComm.command)
            {
                case "V":
                case "":
                    return Utility.leadInV(tP, string.Empty, applyOpen, 9);
                case "U":
                    return Utility.leadInU(tP, string.Empty, applyOpen, 9);
                default:
                    if (tP.Count > 0) { tP[0].addWarning("Lead type: " + tP.additions.leadComm.command + " not recognised. Using a V shaped lead."); }
                    return Utility.leadInV(tP, string.Empty, true, 9);
            }
        }
        /// <summary>TODO The retract.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        public List<ToolPath> retract(ToolPath tP)
        {
            bool applyOpen = false;
            if (tP.additions.leadComm[2] > 0) { applyOpen = true; }
            switch (tP.additions.leadComm.command)
            {
                case "V":
                case "":
                    return Utility.leadOutV(tP, string.Empty, applyOpen, 9);
                case "U":
                    return Utility.leadOutU(tP, string.Empty, applyOpen, 9);
                default:
                    if (tP.Count > 0) { tP[0].addWarning("Lead type: " + tP.additions.leadComm.command + " not recognised. Using a V shaped lead."); }
                    return Utility.leadOutV(tP, string.Empty, true, 9);
            }
        }

        /// <summary>TODO The step down.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        public List<List<ToolPath>> stepDown(ToolPath tP) => new List<List<ToolPath>>();
        /// <summary>TODO The three axis height offset.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        public ToolPath threeAxisHeightOffset(ToolPath tP) => Utility.clearThreeAxisHeightOffset(tP);
        /// <summary>TODO The finish paths.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        public List<ToolPath> finishPaths(ToolPath tP) => Utility.oneFinishPath(tP);

        // Use spherical interpolation (as the range of angles is rather small)
        /// <summary>TODO The interpolate.</summary>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="par">TODO The par.</param>
        /// <param name="lng">TODO The lng.</param>
        /// <returns>The <see cref="ToolPoint"/>.</returns>
        public ToolPoint interpolate(ToolPoint fP, ToolPoint tP, MaterialTool mT, double par, bool lng)
        {
            ToolPoint iPt = new ToolPoint { pt = (1 - par) * fP.pt + par * tP.pt };
            Vector3d cr = Vector3d.CrossProduct(fP.dir, tP.dir);
            double an = Vector3d.VectorAngle(fP.dir, tP.dir);
            iPt.dir = fP.dir;
            iPt.dir.Rotate(an * par, cr);

            iPt.feed = fP.feed;
            iPt.speed = fP.speed;

            return new ToolPoint();
        }

        // Use spherical interpolation
        /// <summary>TODO The ang diff.</summary>
        /// <param name="tP1">TODO The t p 1.</param>
        /// <param name="tP2">TODO The t p 2.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="lng">TODO The lng.</param>
        /// <returns>The <see cref="double"/>.</returns>
        public double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool mT, bool lng) =>
            Vector3d.VectorAngle(tP1.dir, tP2.dir);

        /// <summary>TODO The read code.</summary>
        /// <param name="code">TODO The code.</param>
        /// <returns>The <see cref="MachineInstruction"/>.</returns>
        public MachineInstruction readCode(string code)
        {
            MachineInstruction mI = new MachineInstruction(this);

            ToolPath tP = new ToolPath();

            ToolPoint oldEndPt = null;

            using (StringReader reader = new StringReader(code))
            {
                // Loop over the lines in the string.
                string line;

                double rot = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    ToolPoint tPt = readTP(line, rot, out ToolPoint endPt, out int quality);
                    if (tPt == null) { continue; }
                    rot = Kinematics.xRotation(tPt.mDir, out Point3d cen).X;

                    if (quality != 0)
                    {
                        if (tP.additions.activate == 0) { tP.additions.activate = quality; }
                        else
                        {
                            mI.Add(new MachineOperation(tP));
                            tP = new ToolPath { additions = { activate = quality } };
                        }
                    }

                    // add an extra point if the tilts do not line up.
                    if (oldEndPt != null && tPt.dir != oldEndPt.dir)
                    {
                        oldEndPt.pt = tPt.pt;
                        tP.Add(oldEndPt);
                    }

                    tP.Add(tPt);
                    oldEndPt = endPt;
                }
            }

            mI.Add(new MachineOperation(tP));
            return mI;
        }

        /// <summary>Try to read a line of .omx code. Add an error to the toolpoint if there are problems</summary>
        /// <param name="l">Line of code</param>
        /// <param name="rot">Current orientation of rotary axis. </param>
        /// <param name="endPt">A <see cref="ToolPoint"/> that returns the direction information at the end of the entity.</param>
        /// <param name="quality">Return the quality (held at the toolpath level). </param>
        /// <returns>The <see cref="ToolPoint"/> given by the line.</returns>
        [CanBeNull]
        private static ToolPoint readTP([NotNull] string l, double rot, [NotNull] out ToolPoint endPt, out int quality)
        {
            quality = 0;
            endPt = new ToolPoint();
            if (!l.StartsWith("[0]", StringComparison.Ordinal)) { return null; }
            string[] items = l.Split(',');
            ToolPoint tPt = new ToolPoint();
            bool badRead = false;
            Transform remRot = Transform.Rotation(-rot, Vector3d.YAxis, Point3d.Origin);

            // Read position
            if (items.Length < 4) { return null; }
            if (double.TryParse(items[1], out double x) && double.TryParse(items[2], out double y)
                                                        && double.TryParse(items[3], out double z))
            {
                tPt.pt = new Point3d(x, y, z);
                tPt.pt.Transform(remRot);
            }
            else { badRead = true; }

            // read quality
            if (items.Length < 8 || !int.TryParse(items[7], out quality)) { badRead = true; }

            // check format of XData
            if (items.Length > 15 && int.TryParse(items[14], out int fm))
            {
                switch (fm)
                {
                    case 8: // Incremental Rotary
                        if (items.Length > 16 && items[15] != null)
                        {
                            if (double.TryParse(items[16], out double r)) { tPt.mDir.Rotate(rot + Math.PI * r / 180.0, Vector3d.XAxis); }
                        }

                        break;
                    case 9: // Absolute Rotary
                        if (items.Length > 16 && items[15] != null)
                        {
                            if (double.TryParse(items[16], out double r)) { tPt.mDir.Rotate(Math.PI * r / 180.0, Vector3d.YAxis); }
                        }

                        break;
                    case 27: // Jet Direction
                        if (items.Length > 16 && items[15] != null)
                        {
                            string[] dirs = items[15].Split('|');
                            List<double> dirVals = new List<double>();
                            foreach (string dir in dirs)
                            {
                                if (double.TryParse(dir, out double v)) { dirVals.Add(v); }
                            }

                            if (dirVals.Count == 6)
                            {
                                tPt.dir = -new Vector3d(dirVals[0], dirVals[1], dirVals[2]);
                                endPt.dir = -new Vector3d(dirVals[3], dirVals[4], dirVals[5]);
                                tPt.dir.Transform(remRot);
                                endPt.dir.Transform(remRot);
                            }
                            else { badRead = true; }
                        }
                        break;
                    case 0:
                        break;
                    default:
                        badRead = true;
                        break;
                }
            }
            else { badRead = true; }

            if (badRead) { tPt.addError("Unreadable code: " + l); }

            return tPt;
        }

        /// <summary>TODO The tool dir.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="Vector3d"/>.</returns>
        public Vector3d toolDir(ToolPoint tP) => tP.dir;

        /// <summary>TODO The write code.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="tP">TODO The t p.</param>
        public void writeCode(ref CodeInfo co, ToolPath tP)
        {
            if (tP.Count == 0) { return; }
            if (tP.matTool == null) { Exceptions.matToolException(); }

            // Double check tP does not have additions.
            if (tP.additions.any) { Exceptions.additionsException(); }

            OMXCode.omxPathStart(this, ref co, tP);

            int pathQuality = tP.additions.activate;

            Point3d lastPt = new Point3d(co.machineState["X"], co.machineState["Y"], co.machineState["Z"]);
            Vector3d lastDir = new Vector3d(co.machineState["dX"], co.machineState["dY"], co.machineState["dZ"]);
            int lastQ = (int)co.machineState["Q"];
            bool first = co.machineState["Fi"] > 0;

            foreach (ToolPoint tPt in tP.Where(tPt => tPt != null))
            {
                // Errors will be recorded 1 line early
                tPt.writeErrorAndWarnings(ref co);

                string ptCode = OMXCode.omxTiltPt(co, lastPt, lastDir, tPt, lastQ, this.tiltMax, tP.additions.offset);

                ptCode = tPt.preCode + ptCode + tPt.postCode;

                lastPt = tPt.pt;
                lastDir = tPt.dir;
                lastQ = Math.Abs(tPt.feed) < CAMel_Goo.Tolerance ? 0 : pathQuality;

                if (first)
                {
                    first = false;
                    //continue;
                }

                co.append(ptCode);
            }

            co.machineState["X"] = lastPt.X;
            co.machineState["Y"] = lastPt.Y;
            co.machineState["Z"] = lastPt.Z;
            co.machineState["dX"] = lastDir.X;
            co.machineState["dY"] = lastDir.Y;
            co.machineState["dZ"] = lastDir.Z;
            co.machineState["Q"] = lastQ;
            co.machineState["Fi"] = first ? 1 : -1;

            OMXCode.omxPathEnd(this, ref co, tP);
        }

        /// <summary>TODO The write file start.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mI">TODO The m i.</param>
        public void writeFileStart(ref CodeInfo co, MachineInstruction mI)
        {
            // Set up Machine State
            ToolPoint fPt = mI.startPath.firstP;
            if (fPt == null) { Exceptions.nullPanic(); }

            co.machineState.Clear();
            co.machineState.Add("X", fPt.pt.X);
            co.machineState.Add("Y", fPt.pt.Y);
            co.machineState.Add("Z", fPt.pt.Z);
            co.machineState.Add("dX", fPt.dir.X);
            co.machineState.Add("dY", fPt.dir.Y);
            co.machineState.Add("dZ", fPt.dir.Z);
            co.machineState.Add("Q", mI[0][0].additions.activate);
            co.machineState.Add("Fi", 1); // Know this is setup data not the record of a point

            // Set up possible rotary axis position
            co.machineState.Add("rA", 0);
            co.machineState.Add("rCX", 0);
            co.machineState.Add("rCY", 0);
            co.machineState.Add("rCZ", 0);

            // HACK: (slight) repeat the final point so it gets written
            // To write omx entities we need to know the end position, so write points one behind.
            mI.RemoveAt(0);
            mI[mI.Count - 1][mI[mI.Count - 1].Count - 1].Add(mI.lastP?.deepClone());

            OMXCode.omxInstStart(this, ref co, mI);
        }

        /// <summary>TODO The write file end.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mI">TODO The m i.</param>
        public void writeFileEnd(ref CodeInfo co, MachineInstruction mI) => OMXCode.omxInstEnd(this, ref co, mI);
        /// <summary>TODO The write op start.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mO">TODO The m o.</param>
        public void writeOpStart(ref CodeInfo co, MachineOperation mO) => OMXCode.omxOpStart(this, ref co, mO);
        /// <summary>TODO The write op end.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mO">TODO The m o.</param>
        public void writeOpEnd(ref CodeInfo co, MachineOperation mO) => OMXCode.omxOpEnd(this, ref co, mO);
        /// <summary>TODO The tool change.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="toolNumber">TODO The tool number.</param>
        public void toolChange(ref CodeInfo co, int toolNumber) { }
        /// <summary>TODO The jump check.</summary>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="double"/>.</returns>
        public double jumpCheck(ToolPath fP, ToolPath tP) => 0;
        /// <summary>TODO The jump check.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        public void jumpCheck(ref CodeInfo co, ToolPath fP, ToolPath tP) => Utility.noCheck(ref co, this, fP, tP);

        /// <summary>TODO The transition.</summary>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        public List<ToolPath> transition(ToolPath fP, ToolPath tP, bool retractQ = true, bool insertQ = true) => Utility.transition(this, fP, tP, retractQ, insertQ);

        /// <summary>TODO The transition.</summary>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        public ToolPath transitionPath(ToolPath fP, ToolPath tP)
        {
            if (fP.lastP == null || tP.firstP == null) { Exceptions.nullPanic(); }

            ToolPath move = fP.deepCloneWithNewPoints(new List<ToolPoint>());
            move.name = string.Empty;
            move.preCode = string.Empty;
            move.postCode = string.Empty;
            move.additions.activate = 0;
            move.label = PathLabel.Transition;

            move.Add(new ToolPoint(fP.lastP.tDir, fP.lastP.mDir, -1, 0));

            move.Add(new ToolPoint((2 * fP.lastP.pt + tP.firstP.pt) / 3, new Vector3d(0, 0, 1), fP.lastP.mDir, -1, 0));
            move.Add(new ToolPoint((fP.lastP.pt + 2 * tP.firstP.pt) / 3, new Vector3d(0, 0, 1), fP.lastP.mDir, - 1, 0));

            return move;
        }
    }

    // ReSharper disable once InconsistentNaming
    /// <summary>TODO The omx code.</summary>
    internal static class OMXCode
    {
        private const string rotaryMove = @"Set Rotary";

        /// <summary>TODO The omx tilt pt.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="lastPt">TODO The last pt.</param>
        /// <param name="lastDir">TODO The last dir.</param>
        /// <param name="tPt">TODO The t pt.</param>
        /// <param name="lastQ">TODO The last q.</param>
        /// <param name="tiltMax">TODO The tilt max.</param>
        /// <param name="os">TODO The os.</param>
        /// <returns>The <see cref="string"/>.</returns>
        [NotNull]
        public static string omxTiltPt(
            [NotNull] CodeInfo co, Point3d lastPt,
            Vector3d lastDir, [NotNull] ToolPoint tPt,
            int lastQ, double tiltMax, Vector3d os)
        {
            // Adjust to position of rotary
            double rot = co.machineState["rA"];
            Point3d origin = new Point3d(co.machineState["rCX"], co.machineState["rCY"], co.machineState["rCZ"]);
            Transform rotary = Transform.Rotation(-rot, Vector3d.XAxis, origin);

            Point3d uPt = lastPt;
            Vector3d uS = lastDir;
            Vector3d uE = tPt.dir;

            // rotate to match the rotary axis
            if (tPt.preCode != rotaryMove)
            {
                uPt.Transform(rotary);
                uS.Transform(rotary);
            }

            uE.Transform(rotary);

            // Work on tilts
            double tiltStart = Vector3d.VectorAngle(Vector3d.ZAxis, uS);
            double tiltEnd = Vector3d.VectorAngle(Vector3d.ZAxis, uE);

            // (throw bounds error if B goes past +-bMax degrees or A is not between aMin and aMax)
            if (Math.Abs(tiltStart) > tiltMax || Math.Abs(tiltEnd) > tiltMax)
            {
                co.addError("Tilt too large");
            }

            // Check rotary position and throw errors if needed.
            Vector3d xRot = Kinematics.xRotation(tPt.mDir, out Point3d cen);
            if (Math.Abs(xRot.Y) > CAMel_Goo.Tolerance)
            {
                co.addError("Only the rotary axis around X can be used.");
            }
            else if (Math.Abs(rot - xRot.X) > CAMel_Goo.Tolerance)
            {
                co.addError("Each Toolpath must have a consistent rotary orientation.");
            }

            // Adjust ranges
            co.growRange("X", lastPt.X);
            co.growRange("Y", lastPt.Y);
            co.growRange("Z", lastPt.Z);
            co.growRange("T", Math.Max(tiltStart, tiltEnd));
            co.growRange("R", rot);

            if (tPt.preCode == rotaryMove)
            {
                return omxRotPt9(lastPt, xRot.X, 0, lastQ, os);
            }

            return omxTiltPt27(lastPt, lastDir, tPt.dir, 0, lastQ, os);
        }

        /// <summary>TODO The v to side.</summary>
        /// <param name="v">TODO The v.</param>
        /// <returns>The <see cref="int"/>.</returns>
        private static int vToSide(Vector3d v)
        {
            double d = v * -Vector3d.ZAxis;
            if (d > CAMel_Goo.Tolerance) { return 1; }
            return d < -CAMel_Goo.Tolerance ? 2 : 0;
        }

        /// <summary>OMAX Toolpoint with Absolute Rotary XData 9.</summary>
        /// <param name="machPt">Point</param>
        /// <param name="rot">Previous rotation</param>
        /// <param name="newRot">New Rotation</param>
        /// <param name="bow">Curve to path</param>
        /// <param name="quality">Cut Quality</param>
        /// <param name="os">Offset Side</param>
        /// <returns>The .omx <see cref="string"/> for the toolpoint. </returns>
        [NotNull]
        private static string omxRotPt9(Point3d machPt, double newRot, double bow, int quality, Vector3d os)
        {
            Point3d uPt = machPt;

            StringBuilder gPtBd = new StringBuilder("[0],");
            gPtBd.Append(uPt.X.ToString("0.0000") + ", ");
            gPtBd.Append(uPt.Y.ToString("0.0000") + ", ");
            gPtBd.Append(uPt.Z.ToString("0.0000") + ", ");
            gPtBd.Append("0, "); // tiltStart
            gPtBd.Append("0, "); // tiltEnd
            gPtBd.Append(bow.ToString("0.0000") + ", ");
            int uQuality = quality;
            gPtBd.Append(uQuality + ", ");

            gPtBd.Append(vToSide(os) + ", ");
            gPtBd.Append("R, R, R, R, R, 9, "); // Reserved items and XType

            gPtBd.Append((newRot * 180.0 / Math.PI).ToString("0.0000"));

            gPtBd.Append(",[END]");

            return gPtBd.ToString();
        }

        /// <summary>OMAX Toolpoint with Tilt Direction for entity XData 27</summary>
        /// <param name="machPt">Tool Position</param>
        /// <param name="tiltS">Starting tilt direction. </param>
        /// <param name="tiltE">End tilt direction. </param>
        /// <param name="bow">Curve to path.</param>
        /// <param name="quality">Cut Quality.</param>
        /// <param name="os">Offset Side.</param>
        /// <returns>The .omx <see cref="string"/> for the toolpoint. </returns>
        [NotNull]
        private static string omxTiltPt27(Point3d machPt, Vector3d tiltS, Vector3d tiltE, double bow, int quality, Vector3d os)
        {
            Point3d uPt = machPt;

            // flip tool directions
            Vector3d uS = -tiltS;
            Vector3d uE = -tiltE;

            StringBuilder gPtBd = new StringBuilder("[0],");
            gPtBd.Append(uPt.X.ToString("0.0000") + ", ");
            gPtBd.Append(uPt.Y.ToString("0.0000") + ", ");
            gPtBd.Append(uPt.Z.ToString("0.0000") + ", ");
            gPtBd.Append("0, "); // tiltStart
            gPtBd.Append("0, "); // tiltEnd
            gPtBd.Append(bow.ToString("0.0000") + ", ");
            int uQuality = quality;
            gPtBd.Append(uQuality + ", ");

            gPtBd.Append(vToSide(os) + ", ");
            gPtBd.Append("R, R, R, R, R, 27, "); // Reserved items and XType

            gPtBd.Append(
                uS.X.ToString("0.0000") + "|" + uS.Y.ToString("0.0000") + "|" + uS.Z.ToString("0.0000") +
                "|"); // start tool direction
            gPtBd.Append(
                uE.X.ToString("0.0000") + "|" + uE.Y.ToString("0.0000") + "|" +
                uE.Z.ToString("0.0000")); // end tool direction

            gPtBd.Append(",[END]");

            return gPtBd.ToString();
        }

        /// <summary>TODO The omx tilt pt 23.</summary>
        /// <param name="machPt">TODO The mach pt.</param>
        /// <param name="tiltS">TODO The tilt s.</param>
        /// <param name="tiltE">TODO The tilt e.</param>
        /// <param name="bow">TODO The bow.</param>
        /// <param name="quality">TODO The quality.</param>
        /// <param name="os">TODO The os.</param>
        /// <returns>The <see cref="string"/>.</returns>
        [NotNull, UsedImplicitly]
        private static string omxTiltPt23(Point3d machPt, Vector3d tiltS, Vector3d tiltE, double bow, int quality, Vector3d os)
        {
            StringBuilder gPtBd = new StringBuilder("[0],");
            gPtBd.Append(machPt.X.ToString("0.0000") + ", ");
            gPtBd.Append(machPt.Y.ToString("0.0000") + ", ");
            gPtBd.Append(machPt.Z.ToString("0.0000") + ", ");
            gPtBd.Append("0, "); // tiltStart
            gPtBd.Append("0, "); // tiltEnd
            gPtBd.Append(bow.ToString("0.0000") + ", ");
            int uQuality = quality;
            gPtBd.Append(uQuality + ", ");

            gPtBd.Append(vToSide(os) + ", ");
            gPtBd.Append("R, R, R, R, R, 23, "); // Reserved items and XType

            // flip tool directions
            Vector3d uS = -tiltS;
            Vector3d uE = -tiltE;

            // TODO set up for positive vs negative using atan2
            double tiltStartX = Vector3d.VectorAngle(-Vector3d.ZAxis, new Vector3d(uS.X, 0, uS.Z));
            double tiltStartY = Vector3d.VectorAngle(-Vector3d.ZAxis, new Vector3d(0, uS.Y, uS.Z));
            double tiltEndX = Vector3d.VectorAngle(-Vector3d.ZAxis, new Vector3d(uE.X, 0, uE.Z));
            double tiltEndY = Vector3d.VectorAngle(-Vector3d.ZAxis, new Vector3d(0, uE.Y, uE.Z));

            gPtBd.Append(tiltStartX.ToString("0.0000") + "|" + tiltStartY.ToString("0.0000"));
            gPtBd.Append("|" + tiltEndX.ToString("0.0000") + "|" + tiltEndY.ToString("0.0000"));

            gPtBd.Append(",[END]");

            return gPtBd.ToString();
        }

        /// <summary>TODO The omx inst start.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="co">TODO The co.</param>
        /// <param name="mI">TODO The m i.</param>
        public static void omxInstStart(
            [NotNull] IMachine m, [NotNull] ref CodeInfo co,
            [NotNull] MachineInstruction mI)
        {
            if (mI[0].Count == 0) { Exceptions.noToolPathException(); }
            if (mI[0][0].matTool == null) { Exceptions.matToolException(); }
            if (mI[0][0].matForm == null) { Exceptions.matFormException(); }

            co.currentMT = mI[0][0].matTool;
            co.currentMF = mI[0][0].matForm;
            DateTime thisDay = DateTime.Now;

            // start OMAX file
            co.append(
                "This is an OMAX (.OMX) file.  Do not modify the first 2 lines of this file. For information on this file format contact softwareengineering@omax.com or visit http://www.omax.com.");
            co.append("2"); // file format
            co.append("3"); // format version
            co.append(thisDay.ToOADate().ToString(CultureInfo.InvariantCulture)); // date
            co.append("[Reserved]");
            co.append("[Reserved]");
            co.append("[Reserved]");
            co.append("[Reserved]");
            co.append("[Reserved]");
            co.append("[Reserved]");
            co.append("[COMMENT]");
            co.append(" Machine Instructions Created " + thisDay.ToString("f"));
            System.Reflection.AssemblyName camel = System.Reflection.Assembly.GetExecutingAssembly().GetName();
            DateTime buildTime = new DateTime(2000, 1, 1)
                                 + new TimeSpan(camel.Version?.Build ?? 0, 0, 0, 0)
                                 + TimeSpan.FromSeconds((camel.Version?.Revision ?? 0) * 2);

            co.append(
                "  by " + camel.Name + " "
                + camel.Version?.ToString(2)
                + " built " + buildTime.ToString("U"));
            if (m.name != string.Empty) { co.appendComment("  for " + m.name); }
            co.append("[END]");

            co.currentMT = MaterialTool.Empty; // Clear the tool information so we call a tool change.
        }
        // ReSharper disable once UnusedParameter.Global
        /// <summary>TODO The omx inst end.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="co">TODO The co.</param>
        /// <param name="mI">TODO The m i.</param>
        public static void omxInstEnd(
            [NotNull] IMachine m, [NotNull] ref CodeInfo co,
            [NotNull] MachineInstruction mI)
            => co.append(mI.postCode);

        // ReSharper disable once UnusedParameter.Global
        /// <summary>TODO The omx op start.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="co">TODO The co.</param>
        /// <param name="mO">TODO The m o.</param>
        public static void omxOpStart(
            [NotNull] IMachine m, [NotNull] ref CodeInfo co,
            [NotNull] MachineOperation mO)
            => co.append(mO.preCode);

        // ReSharper disable once UnusedParameter.Global
        /// <summary>TODO The omx op end.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="co">TODO The co.</param>
        /// <param name="mO">TODO The m o.</param>
        public static void
            omxOpEnd([NotNull] IMachine m, [NotNull] ref CodeInfo co, [NotNull] MachineOperation mO) =>
            co.append(mO.postCode);
        // ReSharper disable once UnusedParameter.Global
        /// <summary>TODO The omx path start.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="co">TODO The co.</param>
        /// <param name="tP">TODO The t p.</param>
        public static void omxPathStart([NotNull] IMachine m, [NotNull] ref CodeInfo co, [NotNull] ToolPath tP)
        {
            // Work out if there are any rotary commands to give
            string opts = tP.additions.machineOptions;

            if (tP.label == PathLabel.Insert)
            {
                double oldR = co.machineState["rA"];
                Point3d origin = new Point3d(co.machineState["rCX"], co.machineState["rCY"], co.machineState["rCZ"]);

                double newR = Kinematics.xRotation(tP[0].mDir, out Point3d rCen).X;

                if (Math.Abs(newR - oldR) > CAMel_Goo.Tolerance)
                {
                    tP[0].preCode = rotaryMove;
                    co.machineState["rA"] = newR;
                    co.machineState["rCX"] = rCen.X;
                    co.machineState["rCY"] = rCen.Y;
                    co.machineState["rCZ"] = rCen.Z;
                }
            }

            co.append(tP.preCode);
        }
        // ReSharper disable once UnusedParameter.Global
        /// <summary>TODO The omx path end.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="co">TODO The co.</param>
        /// <param name="tP">TODO The t p.</param>
        public static void omxPathEnd([NotNull] IMachine m, [NotNull] ref CodeInfo co, [NotNull] ToolPath tP) =>
            co.append(tP.postCode);
    }
}