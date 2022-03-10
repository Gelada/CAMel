﻿namespace CAMel.Types.Machine
{
    using System;
    using System.Collections.Generic;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    /// <summary>TODO The two axis factory.</summary>
    public class TwoAxisFactory
    {
        /// <summary>Gets or sets the name.</summary>
        [NotNull]
        public string name { get; set; }
        /// <summary>Gets or sets the extension.</summary>
        [NotNull]
        public string extension { get; set; }
        /// <summary>Gets or sets the section break.</summary>
        [NotNull]
        public string sectionBreak { get; set; }
        /// <summary>Gets or sets the speed change command.</summary>
        [NotNull]
        public string speedChangeCommand { get; set; }
        /// <summary>Gets or sets the tool change command.</summary>
        [NotNull]
        public string toolChangeCommand { get; set; }
        /// <summary>Gets or sets the file start.</summary>
        [NotNull]
        public string fileStart { get; set; }
        /// <summary>Gets or sets the file end.</summary>
        [NotNull]
        public string fileEnd { get; set; }
        /// <summary>Gets or sets the header.</summary>
        [NotNull]
        public string header { get; set; }
        /// <summary>Gets or sets the footer.</summary>
        [NotNull]
        public string footer { get; set; }
        /// <summary>Gets or sets the comment start.</summary>
        [NotNull]
        public string commentStart { get; set; }
        /// <summary>Gets or sets the comment end.</summary>
        [NotNull]
        public string commentEnd { get; set; }
        /// <summary>Gets or sets the m ts.</summary>
        [NotNull]
        public List<MaterialTool> mTs { get; set; }
        /// <summary>Gets or sets the tool activate.</summary>
        [NotNull]
        public string toolActivate { get; set; }
        /// <summary>Gets or sets the tool de activate.</summary>
        [NotNull]
        public string toolDeActivate { get; set; }

        /// <summary>Initializes a new instance of the <see cref="TwoAxisFactory"/> class.</summary>
        public TwoAxisFactory()
        {
            this.name = string.Empty;
            this.extension = GCode.DefaultExtension;
            this.header = string.Empty;
            this.footer = string.Empty;
            this.toolActivate = GCode.DefaultActivateCommand;
            this.toolDeActivate = GCode.DefaultDeActivateCommand;
            this.commentStart = GCode.DefaultCommentStart;
            this.commentEnd = GCode.DefaultCommentEnd;
            this.sectionBreak = "---------------------------------";
            this.fileStart = GCode.DefaultFileStart;
            this.fileEnd = GCode.DefaultFileEnd;
            this.speedChangeCommand = GCode.DefaultSpeedChangeCommand;
            this.toolChangeCommand = GCode.DefaultToolChangeCommand;
            this.mTs = new List<MaterialTool>();
        }

        /// <summary>Initializes a new instance of the <see cref="TwoAxisFactory"/> class.</summary>
        /// <param name="ta">TODO The ta.</param>
        [PublicAPI]
        public TwoAxisFactory([NotNull] TwoAxis ta)
        {
            this.name = ta.name;
            this.extension = ta.extension;
            this.header = ta.header;
            this.footer = ta.footer;
            this.speedChangeCommand = ta.speedChangeCommand;
            this.toolActivate = ta.toolActivate;
            this.toolDeActivate = ta.toolDeActivate;
            this.commentStart = ta.commentStart;
            this.commentEnd = ta.commentEnd;
            this.sectionBreak = ta.sectionBreak;
            this.fileStart = ta.fileStart;
            this.fileEnd = ta.fileEnd;
            this.toolChangeCommand = ta.toolChangeCommand;
            this.mTs = new List<MaterialTool>();
            this.mTs.AddRange(ta.mTs);
        }
    }

    /// <summary>TODO The two axis.</summary>
    public class TwoAxis : IGCodeMachine
    {
        /// <summary>Gets the name.</summary>
        public string name { get; }
        /// <summary>Gets the extension.</summary>
        public string extension { get; }
        /// <summary>Gets a value indicating whether tool length compensation.</summary>
        public bool toolLengthCompensation { get; }
        /// <inheritdoc />
        /// <summary>Gets the section break.</summary>
        public string sectionBreak { get; }
        /// <inheritdoc />
        /// <summary>Gets the speed change command.</summary>
        public string speedChangeCommand { get; }
        /// <inheritdoc />
        /// <summary>Gets the tool change command.</summary>
        public string toolChangeCommand { get; }
        /// <inheritdoc />
        /// <summary>Gets the file start.</summary>
        public string fileStart { get; }
        /// <inheritdoc />
        /// <summary>Gets the file end.</summary>
        public string fileEnd { get; }
        /// <inheritdoc />
        /// <summary>Gets the header.</summary>
        public string header { get; }
        /// <inheritdoc />
        /// <summary>Gets the footer.</summary>
        public string footer { get; }
        /// <inheritdoc />
        /// <summary>Gets the comment start.</summary>
        public string commentStart { get; }
        /// <inheritdoc />
        /// <summary>Gets the comment end.</summary>
        public string commentEnd { get; }
        /// <summary>TODO The terms.</summary>
        [NotNull] private readonly List<char> terms;
        /// <summary>Gets the m ts.</summary>
        public List<MaterialTool> mTs { get; }
        /// <summary>Gets the tool activate.</summary>
        [NotNull]
        internal string toolActivate { get; }
        /// <summary>Gets the tool de activate.</summary>
        [NotNull]
        internal string toolDeActivate { get; }

        /// <summary>TODO The default tpa.</summary>
        public ToolPathAdditions defaultTPA => ToolPathAdditions.twoAxisDefault;

        /// <summary>Initializes a new instance of the <see cref="TwoAxis"/> class.</summary>
        /// <param name="ta">TODO The ta.</param>
        public TwoAxis([NotNull] TwoAxisFactory ta)
        {
            this.name = ta.name;
            this.extension = ta.extension;
            this.header = ta.header;
            this.footer = ta.footer;
            this.toolLengthCompensation = false;
            this.speedChangeCommand = ta.speedChangeCommand;
            this.toolActivate = ta.toolActivate;
            this.toolDeActivate = ta.toolDeActivate;
            this.commentStart = ta.commentStart;
            this.commentEnd = ta.commentEnd;
            this.sectionBreak = ta.sectionBreak;
            this.fileStart = ta.fileStart;
            this.fileEnd = ta.fileEnd;
            this.toolChangeCommand = ta.toolChangeCommand;
            this.mTs = ta.mTs;
            this.terms = new List<char> { 'X', 'Y', 'S', 'F' };
        }

        /// <inheritdoc />
        /// <summary>TODO The type description.</summary>
        public string TypeDescription => @"Instructions for a 2-Axis machine";
        /// <inheritdoc />
        /// <summary>TODO The type name.</summary>
        public string TypeName => @"CAMelTwoAxis";

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="T:System.String" />.</returns>
        public override string ToString() => "2Axis: " + this.name;
        /// <summary>TODO The line number.</summary>
        /// <param name="l">TODO The l.</param>
        /// <param name="line">TODO The line.</param>
        /// <returns>The <see cref="string"/>.</returns>
        public string lineNumber(string l, int line) => GCode.gcLineNumber(l, line);

        /// <summary>TODO The comment.</summary>
        /// <param name="l">TODO The l.</param>
        /// <returns>The <see cref="string"/>.</returns>
        public string comment(string l) => GCode.comment(this, l);
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
        public List<ToolPath> offSet(ToolPath tP) => Utility.planeOffset(tP, Vector3d.ZAxis);
        /// <summary>TODO The insert. </summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        /// 
        // TODO move to a utility for leads for insert and retract
        public List<ToolPath> insert(ToolPath tP)
        {
            switch (tP.additions.leadComm.command)
            {
                case "U":
                case "":
                    return Utility.leadInU(tP, this.toolActivate, this.toolDeActivate);
                case "V":
                    return Utility.leadInV(tP, this.toolActivate, this.toolDeActivate);
                default:
                    if (tP.Count > 0) { tP[0].addWarning("Lead type: " + tP.additions.leadComm.command + " not recognised. Using a U shaped lead."); }
                    return Utility.leadInU(tP, this.toolActivate, this.toolDeActivate);
            }
        }
        /// <summary>TODO The insert. </summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        /// 
        // TODO move to a utility for leads for insert and retract
        public List<ToolPath> retract(ToolPath tP)
        {
            switch (tP.additions.leadComm.command)
            {
                case "U":
                case "":
                    return Utility.leadOutU(tP, this.toolActivate, this.toolDeActivate);
                case "V":
                    return Utility.leadOutV(tP, this.toolActivate, this.toolDeActivate);
                default:
                    if (tP.Count > 0) { tP[0].addWarning("Lead type: " + tP.additions.leadComm.command + " not recognised. Using a U shaped lead."); }
                    return Utility.leadOutU(tP, this.toolActivate, this.toolDeActivate);
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

        /// <summary>TODO The interpolate.</summary>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="par">TODO The par.</param>
        /// <param name="lng">TODO The lng.</param>
        /// <returns>The <see cref="ToolPoint"/>.</returns>
        public ToolPoint interpolate(ToolPoint fP, ToolPoint tP, MaterialTool mT, double par, bool lng)
            => Kinematics.interpolateLinear(fP, tP, par);
        /// <summary>TODO The ang diff.</summary>
        /// <param name="tP1">TODO The t p 1.</param>
        /// <param name="tP2">TODO The t p 2.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="lng">TODO The lng.</param>
        /// <returns>The <see cref="double"/>.</returns>
        public double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool mT, bool lng) => 0;

        /// <summary>TODO The read code.</summary>
        /// <param name="code">TODO The code.</param>
        /// <returns>The <see cref="MachineInstruction"/>.</returns>
        public MachineInstruction readCode(string code) => GCode.gcRead(this, this.mTs, code, this.terms);
        /// <inheritdoc />
        /// <summary>TODO The read tp.</summary>
        /// <param name="values">TODO The values.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <returns>The <see cref="T:CAMel.Types.ToolPoint" />.</returns>
        public ToolPoint readTP(Dictionary<char, double> values, MaterialTool mT) => new ToolPoint(new Point3d(values['X'], values['Y'], 0), new Vector3d(0, 0, 0), values['S'], values['F']);

        /// <summary>TODO The tool dir.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="Vector3d"/>.</returns>
        public Vector3d toolDir(ToolPoint tP) => Vector3d.ZAxis;

        /// <summary>TODO The write code.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="tP">TODO The t p.</param>
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
                    else if (Math.Abs(feed - tP.matTool.feedCut) > CAMel_Goo.Tolerance) // Default to the cut feed rate.
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
                string ptCode = GCode.gcTwoAxis(tPt);

                // Act if feed has changed
                if (fChange)
                {
                    if (Math.Abs(feed) < CAMel_Goo.Tolerance) { ptCode = "G00 " + ptCode; }
                    else
                    {
                        ptCode = "G01 " + ptCode;
                        if (feed > 0) { ptCode = ptCode + " F" + feed.ToString("0"); }
                    }
                }

                fChange = false;

                // Act if speed has changed
                if (sChange && speed >= 0)
                { ptCode = this.speedChangeCommand + " S" + speed.ToString("0") + "\n" + ptCode; }
                sChange = false;

                if (tPt.name != string.Empty) { ptCode = ptCode + " " + this.comment(tPt.name); }

                ptCode = tPt.preCode + ptCode + tPt.postCode;

                co.append(ptCode);

                // Adjust ranges
                co.growRange("X", tPt.pt.X);
                co.growRange("Y", tPt.pt.Y);
            }

            // Pass machine state information
            if (tP.lastP == null) { Exceptions.nullPanic(); }
            co.machineState.Clear();
            co.machineState.Add("X", tP.lastP.pt.X);
            co.machineState.Add("Y", tP.lastP.pt.Y);
            co.machineState.Add("F", feed);
            co.machineState.Add("S", speed);

            GCode.gcPathEnd(this, ref co, tP);
        }

        /// <summary>TODO The write file start.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mI">TODO The m i.</param>
        public void writeFileStart(ref CodeInfo co, MachineInstruction mI)
        {
            // Set up Machine State
            if (mI.firstP == null) { Exceptions.nullPanic(); }
            co.machineState.Clear();
            co.machineState.Add("X", mI.firstP.pt.X);
            co.machineState.Add("Y", mI.firstP.pt.Y);
            co.machineState.Add("F", -1);
            co.machineState.Add("S", -1);

            GCode.gcInstStart(this, ref co, mI);
        }

        /// <summary>TODO The write file end.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mI">TODO The m i.</param>
        public void writeFileEnd(ref CodeInfo co, MachineInstruction mI) => GCode.gcInstEnd(this, ref co, mI);
        /// <summary>TODO The write op end.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mO">TODO The m o.</param>
        public void writeOpEnd(ref CodeInfo co, MachineOperation mO) => GCode.gcOpEnd(this, ref co, mO);
        /// <summary>TODO The write op start.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mO">TODO The m o.</param>
        public void writeOpStart(ref CodeInfo co, MachineOperation mO) => GCode.gcOpStart(this, ref co, mO);
        /// <summary>TODO The tool change.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="toolNumber">TODO The tool number.</param>
        public void toolChange(ref CodeInfo co, int toolNumber) => GCode.toolChange(this, ref co, toolNumber);
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
        /// <summary>TODO The transitionPath.</summary>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        public ToolPath transitionPath(ToolPath fP, ToolPath tP)
        {
            if (fP.matForm == null || tP.matForm == null) { Exceptions.matFormException(); }
            if (fP.matTool == null) { Exceptions.matToolException(); }
            if (fP.lastP == null || tP.firstP == null) { Exceptions.nullPanic(); }

            List<Point3d> route = new List<Point3d> { fP.lastP.pt, tP.firstP.pt };

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