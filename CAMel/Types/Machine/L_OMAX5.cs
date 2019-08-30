using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.Types.Machine
{
    public class Omax5 : IMachine
    {
        public double pathJump { get; }
        //public string sectionBreak { get; }
        public string name { get; }
        //public string commentStart { get; }
        //public string commentEnd { get; }
        public List<MaterialTool> mTs { get; }

        public string extension => "omx";

        private double tiltMax { get; }
        public bool toolLengthCompensation { get; }
        //TODO
        public ToolPathAdditions defaultTPA => ToolPathAdditions.basicDefault;

        public Omax5([NotNull] string name, double pathJump, [NotNull] List<MaterialTool> mTs, double tiltMax)
        {
            this.name = name;
            this.toolLengthCompensation = false;
            //this.commentStart = GCode.DefaultCommentStart;
            //this.commentEnd = GCode.DefaultCommentEnd;
            //this.sectionBreak = GCode.DefaultSectionBreak;
            this.pathJump = pathJump;
            this.tiltMax = tiltMax;
            this.mTs = mTs;
        }

        public string TypeDescription => "Instructions for a OMax 5-axis waterjet";

        public string TypeName => "CAMelOMax5";

        public override string ToString() => this.name;

        // TODO?
        public string comment(string l) => string.Empty;
        public string lineNumber(string l, int line) => l;

        public List<ToolPath> offSet(ToolPath tP) => new List<ToolPath> {tP};
        public List<ToolPath> insertRetract(ToolPath tP) => Utility.leadInOutV(tP, string.Empty, string.Empty, 9);
        public List<List<ToolPath>> stepDown(ToolPath tP) => new List<List<ToolPath>>();
        public ToolPath threeAxisHeightOffset(ToolPath tP) => Utility.clearThreeAxisHeightOffset(tP);
        public List<ToolPath> finishPaths(ToolPath tP) => Utility.oneFinishPath(tP);

        // Use spherical interpolation (as the range of angles is rather small)
        public ToolPoint interpolate(ToolPoint fP, ToolPoint tP, MaterialTool mT, double par, bool lng)
        {
            ToolPoint iPt = new ToolPoint {pt = (1 - par) * fP.pt + par * tP.pt};
            Vector3d cr = Vector3d.CrossProduct(fP.dir, tP.dir);
            double an = Vector3d.VectorAngle(fP.dir, tP.dir);
            iPt.dir = fP.dir;
            iPt.dir.Rotate(an * par, cr);

            iPt.feed = fP.feed;
            iPt.speed = fP.speed;

            return new ToolPoint();
        }

        // Use spherical interpolation
        public double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool mT, bool lng) =>
            Vector3d.VectorAngle(tP1.dir, tP2.dir);

        public MachineInstruction readCode(string code)
        {
            MachineInstruction mI = new MachineInstruction(this);

            ToolPath tP = new ToolPath();

            ToolPoint oldEndPt = null;

            using (StringReader reader = new StringReader(code))
            {
                // Loop over the lines in the string.
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    ToolPoint tPt = readTP(line, out ToolPoint endPt, out int quality);
                    if (tPt == null) { continue; }
                    if (quality != 0)
                    {
                        if (tP.additions.activate == 0) { tP.additions.activate = quality; }
                        else
                        {
                            mI.Add(new MachineOperation(tP));
                            tP = new ToolPath {additions = {activate = quality}};
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
        // Try to read a line of omx code. Add an error to the toolpoint if there are problems
        [CanBeNull]
        private static ToolPoint readTP([NotNull] string l, [NotNull] out ToolPoint endPt, out int quality)
        {
            endPt = new ToolPoint();
            quality = 0;
            if (!l.StartsWith("[0]")) { return null; }
            string[] items = l.Split(',');
            ToolPoint tPt = new ToolPoint();
            endPt = new ToolPoint();
            int fm = 0;
            bool badRead = false;
            // Read position
            if (items.Length < 4) { return null; }
            if (double.TryParse(items[1], out double x) &&
                double.TryParse(items[2], out double y) &&
                double.TryParse(items[3], out double z)) { tPt.pt = new Point3d(x, y, z); }
            else { badRead = true; }
            // read quality
            if (items.Length < 8 || !int.TryParse(items[7], out quality)) { badRead = true; }
            // check format of XData
            if (items.Length > 15 && int.TryParse(items[14], out fm) && fm == 27)
            {
                if (items.Length > 16 && items[15] != null)
                {
                    string[] dirs = items[15].Split('|');
                    List<double> dirVals = new List<double>();
                    foreach (string dir in dirs)
                    { if (double.TryParse(dir, out double v)) { dirVals.Add(v); } }
                    if (dirVals.Count == 6)
                    {
                        tPt.dir = -new Vector3d(dirVals[0], dirVals[1], dirVals[2]);
                        endPt.dir = -new Vector3d(dirVals[3], dirVals[4], dirVals[5]);
                    }
                    else { badRead = true; }
                }
                else { badRead = true; }
            }
            else if (fm != 0) { badRead = true; }

            if (badRead) { tPt.addError("Unreadable code: " + l); }

            return tPt;
        }

        public Vector3d toolDir(ToolPoint tP) => tP.dir;

        public void writeCode(ref CodeInfo co, ToolPath tP)
        {
            if (tP.Count == 0) { return; }
            if (tP.matTool == null) { Exceptions.matToolException(); }

            OMXCode.omxPathStart(this, ref co, tP);

            int pathQuality = tP.additions.activate;

            Point3d lastPt = new Point3d(co.machineState["X"], co.machineState["Y"], co.machineState["Z"]);
            Vector3d lastDir = new Vector3d(co.machineState["dX"], co.machineState["dY"], co.machineState["dZ"]);
            int lastQ = (int) co.machineState["Q"];
            bool first = co.machineState["Fi"] > 0;

            foreach (ToolPoint tPt in tP)
            {
                if (tPt == null) { continue; }
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
                    continue;
                }

                co.append(ptCode);
            }

            // Pass machine state information

            co.machineState.Clear();
            co.machineState.Add("X", lastPt.X);
            co.machineState.Add("Y", lastPt.Y);
            co.machineState.Add("Z", lastPt.Z);
            co.machineState.Add("dX", lastDir.X);
            co.machineState.Add("dY", lastDir.Y);
            co.machineState.Add("dZ", lastDir.Z);
            co.machineState.Add("Q", lastQ);
            co.machineState.Add("Fi", first ? 1 : -1);

            OMXCode.omxPathEnd(this, ref co, tP);
        }

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

            // HACK: (slight) repeat the final point so it gets written
            // To write omx entities we need to know the end position, so write points one behind.
            mI.RemoveAt(0);
            mI[mI.Count - 1][mI[mI.Count - 1].Count - 1].Add(mI.lastP.deepClone());

            OMXCode.omxInstStart(this, ref co, mI);
        }
        public void writeFileEnd(ref CodeInfo co, MachineInstruction mI) => OMXCode.omxInstEnd(this, ref co, mI);
        public void writeOpStart(ref CodeInfo co, MachineOperation mO) => OMXCode.omxOpStart(this, ref co, mO);
        public void writeOpEnd(ref CodeInfo co, MachineOperation mO) => OMXCode.omxOpEnd(this, ref co, mO);
        public void toolChange(ref CodeInfo co, int toolNumber) { }
        public void jumpCheck(ref CodeInfo co, ToolPath fP, ToolPath tP) => Utility.noCheck(ref co, this, fP, tP);

        public ToolPath transition(ToolPath fP, ToolPath tP)
        {
            if (fP.lastP == null || tP.firstP == null) { Exceptions.nullPanic(); }

            ToolPath move = fP.deepCloneWithNewPoints(new List<ToolPoint>());
            move.name = string.Empty;
            move.preCode = string.Empty;
            move.postCode = string.Empty;
            move.additions.activate = 0;

            move.Add(new ToolPoint(fP.lastP.pt, fP.lastP.dir, -1, 0));

            move.Add(new ToolPoint((2 * fP.lastP.pt + tP.firstP.pt) / 3, new Vector3d(0, 0, 1), -1, 0));
            move.Add(new ToolPoint((fP.lastP.pt + 2 * tP.firstP.pt) / 3, new Vector3d(0, 0, 1), -1, 0));

            return move;
        }
    }

    // ReSharper disable once InconsistentNaming
    internal static class OMXCode
    {
        [NotNull]
        public static string omxTiltPt([NotNull] CodeInfo co, Point3d lastPt, Vector3d lastDir,
            [NotNull] ToolPoint tPt, int lastQ, double tiltMax, Vector3d os)
        {
            // Work on tilts

            double tiltStart = Vector3d.VectorAngle(Vector3d.ZAxis, lastDir);
            double tiltEnd = Vector3d.VectorAngle(Vector3d.ZAxis, tPt.dir);

            // (throw bounds error if B goes past +-bMax degrees or A is not between aMin and aMax)

            if (Math.Abs(tiltStart) > tiltMax || Math.Abs(tiltEnd) > tiltMax)
            {
                co.addError("Tilt too large");
            }

            // Adjust ranges

            co.growRange("X", lastPt.X);
            co.growRange("Y", lastPt.Y);
            co.growRange("Z", lastPt.Z);
            co.growRange("T", Math.Max(tiltStart, tiltEnd));

            return omxTiltPt(lastPt, lastDir, tPt.dir, 0, lastQ, os);
        }

        private static int vToSide(Vector3d v)
        {
            double d = v * -Vector3d.ZAxis;
            if (d > CAMel_Goo.Tolerance) { return 1; }
            return d < -CAMel_Goo.Tolerance ? 2 : 0;
        }

        [NotNull]
        private static string omxTiltPt(Point3d machPt, Vector3d tiltS, Vector3d tiltE, double bow, int quality, Vector3d os)
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
            gPtBd.Append("R, R, R, R, R, 27, "); // Reserved items and XType

            // flip tool directions

            Vector3d uS = -tiltS;
            Vector3d uE = -tiltE;

            gPtBd.Append(uS.X.ToString("0.0000") + "|" + uS.Y.ToString("0.0000") + "|" + uS.Z.ToString("0.0000") +
                         "|"); // start tool direction
            gPtBd.Append(uE.X.ToString("0.0000") + "|" + uE.Y.ToString("0.0000") + "|" +
                         uE.Z.ToString("0.0000")); // end tool direction

            gPtBd.Append(",[END]");

            return gPtBd.ToString();
        }

        public static void omxInstStart([NotNull] IMachine m, [NotNull] ref CodeInfo co,
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
            co.append("[COMMENT]");
            co.append(" Machine Instructions Created " + thisDay.ToString("f"));
            co.append("  by " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " "
                      + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            if (m.name != string.Empty) { co.appendComment("  for " + m.name); }
            co.append("[END]");

            co.currentMT = MaterialTool.Empty; // Clear the tool information so we call a tool change.
        }
        // ReSharper disable once UnusedParameter.Global
        public static void omxInstEnd([NotNull] IMachine m, [NotNull] ref CodeInfo co,
            [NotNull] MachineInstruction mI) => co.append(mI.postCode);

        // ReSharper disable once UnusedParameter.Global
        public static void omxOpStart([NotNull] IMachine m, [NotNull] ref CodeInfo co,
            [NotNull] MachineOperation mO) => co.append(mO.preCode);

        // ReSharper disable once UnusedParameter.Global
        public static void
            omxOpEnd([NotNull] IMachine m, [NotNull] ref CodeInfo co, [NotNull] MachineOperation mO) =>
            co.append(mO.postCode);
        // ReSharper disable once UnusedParameter.Global
        public static void omxPathStart([NotNull] IMachine m, [NotNull] ref CodeInfo co, [NotNull] ToolPath tP) =>
            co.append(tP.preCode);
        // ReSharper disable once UnusedParameter.Global
        public static void omxPathEnd([NotNull] IMachine m, [NotNull] ref CodeInfo co, [NotNull] ToolPath tP) =>
            co.append(tP.postCode);
    }
}