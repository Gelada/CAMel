namespace CAMel.Types.Machine
{
    using System;
    using System.Collections.Generic;

    using CAMel.Types.MaterialForm;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    /// <inheritdoc />
    /// <summary>TODO The pocket nc.</summary>
    public class PocketNC : IGCodeMachine
    {
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
        /// <summary>Gets the footer.</summary>
        public string footer { get; }
        /// <summary>Gets the name.</summary>
        public string name { get; }
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

        /// <summary>Gets the a min.</summary>
        private double aMin { get; }
        /// <summary>Gets the a max.</summary>
        private double aMax { get; }
        /// <summary>Gets the b max.</summary>
        private double bMax { get; }
        /// <summary>Gets a value indicating whether tool length compensation.</summary>
        public bool toolLengthCompensation { get; }

        /// <summary>Gets the pivot.</summary>
        private Vector3d pivot { get; } // Position of machine origin in design space.

        /// <summary>TODO The default tpa.</summary>
        public ToolPathAdditions defaultTPA => ToolPathAdditions.basicDefault;

        /// <summary>TODO The extension.</summary>
        public string extension => "ngc";

        /// <summary>Initializes a new instance of the <see cref="PocketNC"/> class.</summary>
        /// <param name="name">TODO The name.</param>
        /// <param name="header">TODO The header.</param>
        /// <param name="footer">TODO The footer.</param>
        /// <param name="pivot">TODO The pivot.</param>
        /// <param name="aMin">TODO The a min.</param>
        /// <param name="aMax">TODO The a max.</param>
        /// <param name="bMax">TODO The b max.</param>
        /// <param name="tLc">TODO The t lc.</param>
        /// <param name="mTs">TODO The m ts.</param>
        public PocketNC(
            [NotNull] string name, [NotNull] string header,
            [NotNull] string footer, Vector3d pivot, double aMin,
            double aMax, double bMax, bool tLc,
            [NotNull] List<MaterialTool> mTs)
        {
            this.name = name;
            this.toolLengthCompensation = tLc;
            this.header = header;
            this.footer = footer;
            this.fileStart = string.Empty;
            this.fileEnd = string.Empty;
            this.commentStart = GCode.DefaultCommentStart;
            this.commentEnd = GCode.DefaultCommentEnd;
            this.sectionBreak = GCode.DefaultSectionBreak;
            this.speedChangeCommand = GCode.DefaultSpeedChangeCommand;
            this.toolChangeCommand = GCode.DefaultToolChangeCommand;
            this.mTs = mTs;
            this.pivot = pivot;
            this.aMin = aMin;
            this.aMax = aMax;
            this.bMax = bMax;
            this.terms = new List<char> { 'X', 'Y', 'Z', 'A', 'B', 'S', 'F' };
        }

        /// <inheritdoc />
        /// <summary>TODO The type description.</summary>
        public string TypeDescription => "Instructions for a PocketNC machine";

        /// <inheritdoc />
        /// <summary>TODO The type name.</summary>
        public string TypeName => "CAMelPocketNC";

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="T:System.String" />.</returns>
        public override string ToString() => this.name;

        /// <summary>TODO The comment.</summary>
        /// <param name="l">TODO The l.</param>
        /// <returns>The <see cref="string"/>.</returns>
        public string comment(string l) => GCode.comment(this, l);
        /// <summary>TODO The line number.</summary>
        /// <param name="l">TODO The l.</param>
        /// <param name="line">TODO The line.</param>
        /// <returns>The <see cref="string"/>.</returns>
        public string lineNumber(string l, int line) => GCode.gcLineNumber(l, line);

        /// <summary>TODO The refine angle.</summary>
        private const double RefineAngle = 4.0 * Math.PI / 180.0;
        /// <summary>TODO The refine.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        public ToolPath refine(ToolPath tP)
        {
            if (tP.matForm == null) { Exceptions.matFormException(); }
            ToolPath refined = Kinematics.angleRefine(this, tP, RefineAngle);
            return tP.matForm.refine(refined, this);
        }

        /// <summary>TODO The off set.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        public List<ToolPath> offSet(ToolPath tP) =>
            tP.planarOffset(out Vector3d dir)
                ? Utility.planeOffset(tP, dir)
                : Utility.localOffset(tP);
        /// <summary>TODO The insert retract.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        public List<ToolPath> insertRetract(ToolPath tP) => Utility.insertRetract(tP);
        /// <summary>TODO The step down.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        public List<List<ToolPath>> stepDown(ToolPath tP) => Utility.stepDown(tP, this);
        /// <summary>TODO The three axis height offset.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        public ToolPath threeAxisHeightOffset(ToolPath tP) => Utility.threeAxisHeightOffset(tP, this);
        /// <summary>TODO The finish paths.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        public List<ToolPath> finishPaths(ToolPath tP) => Utility.finishPaths(tP, this);

        /// <summary>TODO The interpolate.</summary>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="par">TODO The par.</param>
        /// <param name="lng">TODO The lng.</param>
        /// <returns>The <see cref="ToolPoint"/>.</returns>
        public ToolPoint interpolate(ToolPoint fP, ToolPoint tP, MaterialTool mT, double par, bool lng)
        {
            double toolLength = this.toolLengthCompensation ? 0 : mT.toolLength;
            return Kinematics.interpolateFiveAxisABTable(this.pivot, toolLength, fP, tP, par, lng);
        }

        /// <summary>TODO The ang diff.</summary>
        /// <param name="tP1">TODO The t p 1.</param>
        /// <param name="tP2">TODO The t p 2.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="lng">TODO The lng.</param>
        /// <returns>The <see cref="double"/>.</returns>
        public double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool mT, bool lng)
        {
            double toolLength = this.toolLengthCompensation ? 0 : mT.toolLength;
            return Kinematics.angDiffFiveAxisABTable(this.pivot, toolLength, tP1, tP2, lng);
        }

        /// <summary>TODO The read code.</summary>
        /// <param name="code">TODO The code.</param>
        /// <returns>The <see cref="MachineInstruction"/>.</returns>
        public MachineInstruction readCode(string code)
        {
            if (this.mTs.Count == 0) { Exceptions.noToolException(); }
            return GCode.gcRead(this, this.mTs, code, this.terms);
        }

        /// <summary>TODO The read tp.</summary>
        /// <param name="values">TODO The values.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <returns>The <see cref="ToolPoint"/>.</returns>
        public ToolPoint readTP(Dictionary<char, double> values, MaterialTool mT)
        {
            Point3d machPt = new Point3d(values['X'], values['Y'], values['Z']);
            Vector3d ab = new Vector3d(values['A'] * Math.PI / 180.0, values['B'] * Math.PI / 180.0, 0);

            ToolPoint tP = new ToolPoint
                {
                    speed = values['S'],
                    feed = values['F']
                };

            double toolLength = this.toolLengthCompensation ? 0 : mT.toolLength;

            return Kinematics.kFiveAxisABTable(tP, this.pivot, toolLength, machPt, ab);
        }

        /// <summary>TODO The tool dir.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="Vector3d"/>.</returns>
        public Vector3d toolDir(ToolPoint tP) => tP.dir;

        /// <summary>TODO The angle acc.</summary>
        private const double AngleAcc = 0.0001; // accuracy of angles to assume we lie on the cusp.

        /// <summary>TODO The write code.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="tP">TODO The t p.</param>
        public void writeCode(ref CodeInfo co, ToolPath tP)
        {
            // Double check tP does not have additions.
            if (tP.additions.any) { Exceptions.additionsException(); }

            if (tP.Count <= 0) { return; }
            if (tP.matTool == null) { Exceptions.matToolException(); }

            GCode.gcPathStart(this, ref co, tP);

            // We will watch for speed and feed changes.
            // We will adjust A and B as best as possible and otherwise throw errors.
            // Manual unwinding Grrrr!

            // work out initial values of feed.
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

            Vector3d ab = new Vector3d(co.machineState["A"], co.machineState["B"], 0);

            double bTo = 0; // Allow for smooth adjustment through the cusp with A at 90.
            int bSteps = 0;

            Point3d machPt = new Point3d();

            double toolLength = this.toolLengthCompensation ? 0 : tP.matTool.toolLength;

            for (int i = 0; i < tP.Count; i++)
            {
                ToolPoint tPt = tP[i];

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

                // Work on tool orientation

                // get naive orientation and Machine XYZ position
                Vector3d newAB = Kinematics.ikFiveAxisABTable(tPt, this.pivot, toolLength, out machPt);

                // adjust B to correct period
                newAB.Y += 2.0 * Math.PI * Math.Round((ab.Y - newAB.Y) / (2.0 * Math.PI));

                // set A to 90 if it is close (to avoid a lot of messing with B for no real reason)
                if (Math.Abs(newAB.X - Math.PI / 2.0) < AngleAcc) { newAB.X = Math.PI / 2.0; }

                // adjust through cusp
                if (Math.Abs(newAB.X - Math.PI / 2.0) < CAMel_Goo.Tolerance) // already set if nearly there.
                {
                    // detect that we are already moving
                    if (bSteps > 0)
                    {
                        newAB.Y = ab.Y + (bTo - ab.Y) / bSteps;
                        bSteps--;
                    }
                    else // head forward to next non-vertical point or the end.
                    {
                        int j = i + 1;

                        while (j < tP.Count - 1 &&
                               Math.Abs(
                                   Kinematics.ikFiveAxisABTable(tP[j], this.pivot, toolLength, out machPt).X
                                   - Math.PI / 2.0) < AngleAcc) { j++; }

                        // If we are at the start of a path and vertical then we can just use the first non-vertical
                        // position for the whole run.
                        if (Math.Abs(ab.X - Math.PI / 2.0) < AngleAcc)
                        {
                            bTo = Kinematics.ikFiveAxisABTable(tP[j], this.pivot, toolLength, out machPt).Y;
                            bSteps = j - i;
                            newAB.Y = bTo;
                        }

                        // if we get to the end and it is still vertical we do not need to rotate.
                        else if (Math.Abs(Kinematics.ikFiveAxisABTable(tP[j], this.pivot, toolLength, out machPt).X) <
                                 AngleAcc)
                        {
                            bTo = ab.X;
                            bSteps = j - i;
                            newAB.Y = bTo;
                        }
                        else
                        {
                            bTo = Kinematics.ikFiveAxisABTable(tP[j], this.pivot, toolLength, out machPt).Y;
                            bSteps = j - i;
                            newAB.Y = ab.Y;
                        }
                    }
                }

                // take advantage of the double stance for A,
                // up until now, A is between -90 and 90, all paths start in
                // that region, but can rotate out of it if necessary.
                // This will mean some cutable paths become impossible.
                // This assumes only a double stance in positive position.
                if (newAB.X > Math.PI - this.aMax) // check if double stance is possible
                {
                    if (newAB.Y - ab.Y > Math.PI) // check for big rotation in B
                    {
                        newAB.X = Math.PI - newAB.X;
                        newAB.Y -= Math.PI;
                    }
                    else if (newAB.Y - ab.Y < -Math.PI) // check for big rotation in B
                    {
                        newAB.X = Math.PI - newAB.X;
                        newAB.Y += Math.PI;
                    }
                }

                // (throw bounds error if B goes past +-bMax degrees or A is not between aMin and aMax)
                if (Math.Abs(newAB.Y) > this.bMax) { co.addError("Out of bounds on B"); }
                if (newAB.X > this.aMax || newAB.X < this.aMin) { co.addError("Out of bounds on A"); }

                // update AB value
                ab = newAB;

                // Add the position information
                string ptCode = GCode.gcFiveAxisAB(machPt, ab);

                // Act if feed has changed
                if (fChange && feed >= 0)
                {
                    if (Math.Abs(feed) < CAMel_Goo.Tolerance) { ptCode = "G00 " + ptCode; }
                    else { ptCode = "G01 " + ptCode + " F" + feed.ToString("0.00"); }
                }

                fChange = false;

                // Act if speed has changed
                if (sChange && speed >= 0)
                {
                    ptCode = this.speedChangeCommand + " S" + speed.ToString("0") + "\n" + ptCode;
                }

                sChange = false;

                if (tPt.name != string.Empty) { ptCode = ptCode + " " + this.comment(tPt.name); }

                ptCode = tPt.preCode + ptCode + tPt.postCode;

                co.append(ptCode);

                // Adjust ranges
                co.growRange("X", machPt.X);
                co.growRange("Y", machPt.Y);
                co.growRange("Z", machPt.Z);
                co.growRange("A", ab.X * 180.0 / Math.PI);
                co.growRange("B", ab.Y * 180.0 / Math.PI);
            }

            // Pass machine state information
            co.machineState.Clear();
            co.machineState.Add("X", machPt.X);
            co.machineState.Add("Y", machPt.Y);
            co.machineState.Add("Z", machPt.Z);
            co.machineState.Add("A", ab.X);
            co.machineState.Add("B", ab.Y);
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
            if (mI.firstP == null) { Exceptions.emptyPathException(); }
            if (mI[0][0].matTool == null) { Exceptions.matToolException(); }

            double toolLength = mI.m.toolLengthCompensation ? 0 : mI[0][0].matTool.toolLength;

            Vector3d ab = Kinematics.ikFiveAxisABTable(mI.firstP, this.pivot, toolLength, out Point3d machPt);

            co.machineState.Clear();
            co.machineState.Add("X", machPt.X);
            co.machineState.Add("Y", machPt.Y);
            co.machineState.Add("Z", machPt.Z);
            co.machineState.Add("A", ab.X);
            co.machineState.Add("B", ab.Y);
            co.machineState.Add("F", -1);
            co.machineState.Add("S", -1);

            GCode.gcInstStart(this, ref co, mI);
        }

        /// <summary>TODO The write file end.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mI">TODO The m i.</param>
        public void writeFileEnd(ref CodeInfo co, MachineInstruction mI) => GCode.gcInstEnd(this, ref co, mI);
        /// <summary>TODO The write op start.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mO">TODO The m o.</param>
        public void writeOpStart(ref CodeInfo co, MachineOperation mO) => GCode.gcOpStart(this, ref co, mO);
        /// <summary>TODO The write op end.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mO">TODO The m o.</param>
        public void writeOpEnd(ref CodeInfo co, MachineOperation mO) => GCode.gcOpEnd(this, ref co, mO);
        /// <summary>TODO The tool change.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="toolNumber">TODO The tool number.</param>
        public void toolChange(ref CodeInfo co, int toolNumber) => GCode.toolChange(this, ref co, toolNumber);
        /// <summary>TODO The jump check.</summary>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="double"/>.</returns>
        public double jumpCheck(ToolPath fP, ToolPath tP) => Utility.jumpCheck(fP, tP);
        /// <summary>TODO The jump check.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        public void jumpCheck(ref CodeInfo co, ToolPath fP, ToolPath tP) => Utility.jumpCheck(ref co, this, fP, tP);

        // This should call a utility with standard options
        // a good time to move it is when a second 5-axis is added
        // hopefully at that point there is a better understanding of safe moves!

        /// <summary>TODO The transition.</summary>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        /// <exception cref="Exception"></exception>
        public ToolPath transition(ToolPath fP, ToolPath tP)
        {
            if (fP.matForm == null || tP.matForm == null) { Exceptions.matFormException(); }
            if (fP.matTool == null) { Exceptions.matToolException(); }
            if (fP.lastP == null || tP.firstP == null) { Exceptions.emptyPathException(); }

            if (this.jumpCheck(fP, tP) > 0) { Exceptions.transitionException(); }

            // Safely move from one safe point to another.
            ToolPath move = tP.deepCloneWithNewPoints(new List<ToolPoint>());
            move.name = "Transition";
            move.label = PathLabel.Transition;

            List<Point3d> route = new List<Point3d>();
            int i;

            route.Add(fP.lastP.pt);
            route.Add(tP.firstP.pt);

            // loop through intersecting with safe bubble and adding points
            for (i = 0; i < route.Count - 1 && route.Count < 1000;)
            {
                if (tP.matForm.intersect(route[i], route[i + 1], tP.matForm.safeDistance, out MFintersects inters))
                {
                    MFintersects fromMid =
                        tP.matForm.intersect(inters.mid, inters.midOut, tP.matForm.safeDistance * 1.1);
                    route.Insert(i + 1, inters.mid + fromMid.thrDist * inters.midOut);
                }
                else { i++; }
            }

            // add extra points if the angle change between steps is too large (pi/30)
            bool lng = false;

            // work out how far angle needs to move
            double angSpread = this.angDiff(fP.lastP, tP.firstP, fP.matTool, false);

            int steps = (int)Math.Ceiling(30 * angSpread / (Math.PI * route.Count));
            if (steps == 0) { steps = 1; } // Need to add at least one point even if angSpread is 0

            // Try to build a path with angles.
            // If a tool line hits the material
            // switch to the longer rotate and try again
            for (i = 0; i < route.Count - 1; i++)
            {
                // add new points at speed 0 to describe rapid move.
                for (int j = 0; j < steps; j++)
                {
                    double shift = (steps * i + j) / (double)(steps * (route.Count - 1));
                    Vector3d mixDir = this.interpolate(fP.lastP, tP.firstP, fP.matTool, shift, lng).dir;

                    ToolPoint newTP = new ToolPoint((j * route[i + 1] + (steps - j) * route[i]) / steps, mixDir, -1, 0);
                    if (fP.matForm.intersect(newTP, 0).thrDist > 0
                        || tP.matForm.intersect(newTP, 0).thrDist > 0)
                    {
                        if (lng)
                        {
                            // something has gone horribly wrong and
                            // both angle change directions will hit the material
                            // break;
                            throw new Exception(
                                "Safe Route failed to find a safe path from the end of one toolpath to the next.");
                        }

                        // start again with the longer angle change
                        lng = true;
                        i = 0;
                        j = 0;
                        angSpread = this.angDiff(fP.lastP, tP.firstP, fP.matTool, true);
                        steps = (int)Math.Ceiling(30 * angSpread / (Math.PI * route.Count));
                        move = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                        move.name = "Transition";
                        move.label = PathLabel.Transition;
                    }
                    else { move.Add(newTP); }
                }
            }

            return move;
        }
    }
}
