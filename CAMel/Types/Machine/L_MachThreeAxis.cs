namespace CAMel.Types.Machine
{
    using System;
    using System.Collections.Generic;

    using CAMel.Types.MaterialForm;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    public class ThreeAxisFactory
    {
        [NotNull] public string name { get; set; }
        [NotNull] public string extension { get; set; }
        [PublicAPI] public bool toolLengthCompensation { get; set; }
        [NotNull] public string sectionBreak { get; set; }
        [NotNull] public string speedChangeCommand { get; set; }
        [NotNull] public string toolChangeCommand { get; set; }
        [NotNull] public string fileStart { get; set; }
        [NotNull] public string fileEnd { get; set; }
        [NotNull] public string header { get; set; }
        [NotNull] public string footer { get; set; }
        [NotNull] public string commentStart { get; set; }
        [NotNull] public string commentEnd { get; set; }
        [NotNull] public List<MaterialTool> mTs { get; set; }

        public ThreeAxisFactory()
        {
            this.name = string.Empty;
            this.extension = GCode.DefaultExtension;
            this.header = string.Empty;
            this.footer = string.Empty;
            this.toolLengthCompensation = false;
            this.commentStart = GCode.DefaultCommentStart;
            this.commentEnd = GCode.DefaultCommentEnd;
            this.sectionBreak = "---------------------------------";
            this.fileStart = GCode.DefaultFileStart;
            this.fileEnd = GCode.DefaultFileEnd;
            this.speedChangeCommand = GCode.DefaultSpeedChangeCommand;
            this.toolChangeCommand = GCode.DefaultToolChangeCommand;
            this.mTs = new List<MaterialTool>();
        }
        [PublicAPI]
        // ReSharper disable once SuggestBaseTypeForParameter
        public ThreeAxisFactory([NotNull] ThreeAxis ta)
        {
            this.name = ta.name;
            this.extension = ta.extension;
            this.toolLengthCompensation = ta.toolLengthCompensation;
            this.header = ta.header;
            this.footer = ta.footer;
            this.speedChangeCommand = ta.speedChangeCommand;
            this.toolChangeCommand = ta.toolChangeCommand;
            this.commentStart = ta.commentStart;
            this.commentEnd = ta.commentEnd;
            this.sectionBreak = ta.sectionBreak;
            this.fileStart = ta.fileStart;
            this.fileEnd = ta.fileEnd;
            this.mTs = new List<MaterialTool>();
            this.mTs.AddRange(ta.mTs);
        }
    }

    public class ThreeAxis : IGCodeMachine
    {
        public string name { get; }
        public string extension { get; }
        public bool toolLengthCompensation { get; }
        public string sectionBreak { get; }
        public string speedChangeCommand { get; }
        public string toolChangeCommand { get; }
        public string fileStart { get; }
        public string fileEnd { get; }
        public string header { get; }
        public string footer { get; }
        public string commentStart { get; }
        public string commentEnd { get; }
        [NotNull] private readonly List<char> terms;
        public List<MaterialTool> mTs { get; }

        public ToolPathAdditions defaultTPA => ToolPathAdditions.basicDefault;

        public ThreeAxis([NotNull] ThreeAxisFactory ta)
        {
            this.name = ta.name;
            this.extension = ta.extension;
            this.toolLengthCompensation = ta.toolLengthCompensation;
            this.header = ta.header;
            this.footer = ta.footer;
            this.speedChangeCommand = ta.speedChangeCommand;
            this.toolChangeCommand = ta.toolChangeCommand;
            this.commentStart = ta.commentStart;
            this.commentEnd = ta.commentEnd;
            this.sectionBreak = ta.sectionBreak;
            this.fileStart = ta.fileStart;
            this.fileEnd = ta.fileEnd;
            this.mTs = ta.mTs;
            this.terms = new List<char> {'X', 'Y', 'Z', 'S', 'F'};
        }

        public string TypeDescription => @"Instructions for a 3-Axis machine";

        public string TypeName => @"CAMelThreeAxis";

        public override string ToString() => "3Axis: " + this.name;

        public string comment(string l) => GCode.comment(this, l);
        public string lineNumber(string l, int line) => GCode.gcLineNumber(l, line);
        public ToolPath refine(ToolPath tP) => tP.matForm?.refine(tP, this) ?? tP;
        public List<ToolPath> offSet(ToolPath tP) => tP.planarOffset(out Vector3d dir) ? Utility.planeOffset(tP, dir) : Utility.localOffset(tP);
        public List<ToolPath> insertRetract(ToolPath tP) => Utility.insertRetract(tP);
        public List<List<ToolPath>> stepDown(ToolPath tP) => Utility.stepDown(tP, this);
        public ToolPath threeAxisHeightOffset(ToolPath tP) => Utility.threeAxisHeightOffset(tP, this);
        public List<ToolPath> finishPaths(ToolPath tP) => Utility.finishPaths(tP, this);

        public ToolPoint interpolate(ToolPoint fP, ToolPoint tP, MaterialTool mT, double par, bool lng)
            => Kinematics.interpolateLinear(fP, tP, par);

        public double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool mT, bool lng) => 0;

        public MachineInstruction readCode(string code) => GCode.gcRead(this, this.mTs, code, this.terms);
        public ToolPoint readTP(Dictionary<char, double> values, MaterialTool mT) => new ToolPoint(new Point3d(values['X'], values['Y'], values['Z']), new Vector3d(0, 0, 0), values['S'], values['F']);

        public Vector3d toolDir(ToolPoint tP) => Vector3d.ZAxis;

        public void writeCode(ref CodeInfo co, [ItemNotNull] ToolPath tP)
        {
            if (tP.matTool == null) { Exceptions.matToolException(); }
            // Double check tP does not have additions.
            if (tP.additions.any) { Exceptions.additionsException(); }

            if (tP.Count <= 0) { return; }

            GCode.gcPathStart(this, ref co, tP);

            bool fChange = false;
            bool sChange = false;

            double feed = co.machineState["F"];
            double speed = co.machineState["S"];
            if (feed < 0)
            {
                feed = tP.matTool.feedCut;
                fChange = true;
            }
            if (speed < 0)
            {
                speed = tP.matTool.speed;
                sChange = true;
            }

            foreach (ToolPoint tPt in tP)
            {
                tPt.writeErrorAndWarnings(ref co);

                // Establish new feed value
                if (Math.Abs(tPt.feed - feed) > CAMel_Goo.Tolerance)
                {
                    if (tPt.feed >= 0)
                    {
                        fChange = true;
                        feed = tPt.feed;
                    }
                    else if (Math.Abs(feed - tP.matTool.feedCut) > CAMel_Goo.Tolerance
                    ) // Default to the cut feed rate.
                    {
                        fChange = true;
                        feed = tP.matTool.feedCut;
                    }
                }

                // Establish new speed value
                if (Math.Abs(tPt.speed - speed) > CAMel_Goo.Tolerance)
                {
                    if (tPt.speed > 0)
                    {
                        sChange = true;
                        speed = tPt.speed;
                    }
                }

                // Add the position information
                string ptCode = GCode.gcThreeAxis(tPt);

                // Act if feed has changed
                if (fChange && feed >= 0)
                {
                    if (Math.Abs(feed) < CAMel_Goo.Tolerance) { ptCode = "G00 " + ptCode; }
                    else { ptCode = "G01 " + ptCode + " F" + feed.ToString("0"); }
                }
                fChange = false;

                // Act if speed has changed
                if (sChange && speed >= 0)
                {
                    ptCode = this.speedChangeCommand + " S" + speed.ToString("0") + "\n" + ptCode;
                }
                sChange = false;

                if (tPt.name != string.Empty) { ptCode = ptCode + " " + comment(tPt.name); }

                ptCode = tPt.preCode + ptCode + tPt.postCode;

                co.append(ptCode);

                // Adjust ranges

                co.growRange("X", tPt.pt.X);
                co.growRange("Y", tPt.pt.Y);
                co.growRange("Z", tPt.pt.Z);
            }
            // Pass machine state information
            if (tP.lastP == null) { Exceptions.nullPanic(); }
            co.machineState.Clear();
            co.machineState.Add("X", tP.lastP.pt.X);
            co.machineState.Add("Y", tP.lastP.pt.Y);
            co.machineState.Add("Z", tP.lastP.pt.Z);
            co.machineState.Add("F", feed);
            co.machineState.Add("S", speed);

            GCode.gcPathEnd(this, ref co, tP);
        }
        public void writeFileStart(ref CodeInfo co, MachineInstruction mI)
        {
            // Set up Machine State

            if (mI.firstP == null) { Exceptions.noToolPathException(); }

            co.machineState.Clear();
            co.machineState.Add("X", mI.firstP.pt.X);
            co.machineState.Add("Y", mI.firstP.pt.Y);
            co.machineState.Add("Z", mI.firstP.pt.Z);
            co.machineState.Add("F", -1);
            co.machineState.Add("S", -1);

            GCode.gcInstStart(this, ref co, mI);
        }
        public void writeFileEnd(ref CodeInfo co, MachineInstruction mI) => GCode.gcInstEnd(this, ref co, mI);
        public void writeOpStart(ref CodeInfo co, MachineOperation mO) => GCode.gcOpStart(this, ref co, mO);
        public void writeOpEnd(ref CodeInfo co, MachineOperation mO) => GCode.gcOpEnd(this, ref co, mO);
        public void toolChange(ref CodeInfo co, int toolNumber) => GCode.toolChange(this, ref co, toolNumber);
        public double jumpCheck(ToolPath fP, ToolPath tP) => Utility.jumpCheck(this, fP, tP);
        public void jumpCheck(ref CodeInfo co, ToolPath fP, ToolPath tP) => Utility.jumpCheck(ref co, this, fP, tP);

        public ToolPath transition(ToolPath fP, ToolPath tP)
        {
            if (fP.matForm == null || tP.matForm == null) { Exceptions.matFormException(); }
            if (fP.matTool == null) { Exceptions.matToolException(); }
            if (fP.lastP == null || tP.firstP == null) { Exceptions.nullPanic(); }

            // Start with a straight line, see how close it
            // comes to danger. If its too close add a new
            // point and try again.

            List<Point3d> route = new List<Point3d> {fP.lastP.pt, tP.firstP.pt};

            int i;

            // loop through intersecting with safe bubble and adding points
            for (i = 0; i < route.Count - 1 && i < 100;)
            {
                if (tP.matForm.intersect(route[i], route[i + 1], tP.matForm.safeDistance, out MFintersects inters))
                {
                    MFintersects fromMid = tP.matForm.intersect(inters.mid, inters.midOut, tP.matForm.safeDistance * 1.1);
                    route.Insert(i + 1, inters.mid + fromMid.thrDist * inters.midOut);
                }
                else { i++; }
            }

            // get rid of start and end points that are already in the paths
            route.RemoveAt(0);
            route.RemoveAt(route.Count - 1);

            ToolPath move = tP.deepCloneWithNewPoints(new List<ToolPoint>());
            move.name = "Transition";
            move.preCode = string.Empty;
            move.postCode = string.Empty;
            move.label = PathLabel.Transition;

            foreach (Point3d pt in route)
            {
                // add new point at speed 0 to describe rapid move.
                move.Add(new ToolPoint(pt, new Vector3d(), -1, 0));
            }

            return move;
        }
    }
}