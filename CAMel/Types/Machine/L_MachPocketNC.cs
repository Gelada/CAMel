using System;
using System.Collections.Generic;
using CAMel.Types.MaterialForm;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.Types.Machine
{
    public class PocketNC : IGCodeMachine
    {
        public double pathJump { get; }
        public string sectionBreak { get; }
        public string speedChangeCommand { get; }
        public string toolChangeCommand { get; }
        public string fileStart { get; }
        public string fileEnd { get; }
        public string header { get; }
        public string footer { get; }
        public string name { get; }
        public string commentStart { get; }
        public string commentEnd { get; }
        [NotNull] private readonly List<char> _terms;
        public List<MaterialTool> mTs { get; }

        private double aMin { get; }
        private double aMax { get; }
        private double bMax { get; }
        public bool toolLengthCompensation { get; }

        private Vector3d pivot { get; } // Position of machine origin in design space.

        public ToolPathAdditions defaultTPA => ToolPathAdditions.basicDefault;

        public string extension => "ngc";

        public PocketNC([NotNull] string name, [NotNull] string header, [NotNull] string footer, Vector3d pivot, double aMin, double aMax, double bMax, bool tLc, double pathJump, [NotNull] List<MaterialTool> mTs)
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
            this.pathJump = pathJump;
            this.mTs = mTs;
            this.pivot = pivot;
            this.aMin = aMin;
            this.aMax = aMax;
            this.bMax = bMax;
            this._terms = new List<char> { 'X', 'Y', 'Z', 'A', 'B', 'S', 'F' };
        }

        public string TypeDescription => "Instructions for a PocketNC machine";

        public string TypeName => "CAMelPocketNC";

        public override string ToString() => this.name;

        public string comment(string l) => GCode.comment(this, l);
        public string lineNumber(string l, int line) => GCode.gcLineNumber(l, line);

        public ToolPath insertRetract(ToolPath tP) => Utility.insertRetract(tP);
        public List<List<ToolPath>> stepDown(ToolPath tP) => Utility.stepDown(tP, this);
        public ToolPath threeAxisHeightOffset(ToolPath tP) => Utility.threeAxisHeightOffset(tP, this);
        public List<ToolPath> finishPaths(ToolPath tP) => Utility.finishPaths(tP, this);

        public ToolPoint interpolate(ToolPoint fP, ToolPoint tP, MaterialTool mT, double par, bool lng)
        {
            double toolLength = this.toolLengthCompensation ? 0 : mT.toolLength;
            return Kinematics.interpolateFiveAxisABTable(this.pivot, toolLength, fP, tP, par, lng);
        }
        public double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool mT, bool lng)
        {
            double toolLength = this.toolLengthCompensation ? 0 : mT.toolLength;
            return Kinematics.angDiffFiveAxisABTable(this.pivot, toolLength, tP1, tP2, lng);
        }

        public MachineInstruction readCode(string code)
        {
            if(this.mTs.Count == 0) { Exceptions.noToolException(); }
            return GCode.gcRead(this, this.mTs, code, this._terms);
        }
        public ToolPoint readTP(Dictionary<char, double> values, MaterialTool mT)
        {
            Point3d machPt = new Point3d(values['X'], values['Y'], values['Z']);
            Vector3d ab = new Vector3d(values['A']*Math.PI/180.0, values['B'] * Math.PI/180.0, 0);

            ToolPoint tP = new ToolPoint
            {
                speed = values['S'],
                feed = values['F']
            };

            double toolLength = this.toolLengthCompensation ? 0 : mT.toolLength;

            return Kinematics.kFiveAxisABTable(tP, this.pivot, toolLength, machPt, ab);
        }

        public Vector3d toolDir(ToolPoint tP) => tP.dir;

        private const double _AngleAcc = 0.0001; // accuracy of angles to assume we lie on the cusp.

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
            if (feed < 0) { feed = tP.matTool.feedCut; fChange = true; }
            if (speed < 0) { speed = tP.matTool.speed; sChange = true; }

            Vector3d ab = new Vector3d(co.machineState["A"], co.machineState["B"], 0);

            double bTo = 0;  // Allow for smooth adjustment through the cusp with A at 90.
            int bSteps = 0;  //

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
                newAB.Y = newAB.Y + 2.0 * Math.PI * Math.Round((ab.Y - newAB.Y) / (2.0 * Math.PI));

                // set A to 90 if it is close (to avoid a lot of messing with B for no real reason)

                if (Math.Abs(newAB.X - Math.PI / 2.0) < _AngleAcc) { newAB.X = Math.PI / 2.0; }

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
                                   - Math.PI / 2.0) < _AngleAcc)
                        { j++; }

                        // If we are at the start of a path and vertical then we can just use the first non-vertical
                        // position for the whole run.
                        if (Math.Abs(ab.X - Math.PI / 2.0) < _AngleAcc)
                        {
                            bTo = Kinematics.ikFiveAxisABTable(tP[j], this.pivot, toolLength, out machPt).Y;
                            bSteps = j - i;
                            newAB.Y = bTo;
                        }
                        // if we get to the end and it is still vertical we do not need to rotate.
                        else if (Math.Abs(Kinematics.ikFiveAxisABTable(tP[j], this.pivot, toolLength, out machPt).X) < _AngleAcc)
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
                        newAB.Y = newAB.Y - Math.PI;
                    }
                    else if (newAB.Y - ab.Y < -Math.PI) // check for big rotation in B
                    {
                        newAB.X = Math.PI - newAB.X;
                        newAB.Y = newAB.Y + Math.PI;
                    }
                }

                // (throw bounds error if B goes past +-bMax degrees or A is not between aMin and aMax)


                if (Math.Abs(newAB.Y) > this.bMax)
                {
                    co.addError("Out of bounds on B");
                }
                if (newAB.X > this.aMax || newAB.X < this.aMin)
                {
                    co.addError("Out of bounds on A");
                }

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

                if (tPt.name != string.Empty) { ptCode = ptCode + " " + comment(tPt.name); }

                ptCode = tPt.preCode + ptCode + tPt.postCode;

                co.append(ptCode);
                // Adjust ranges

                co.growRange("X", machPt.X);
                co.growRange("Y", machPt.Y);
                co.growRange("Z", machPt.Z);
                co.growRange("A", ab.X);
                co.growRange("B", ab.Y);
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

        public void writeFileStart(ref CodeInfo co, MachineInstruction mI, ToolPath startPath)
        {
            // Set up Machine State

            if (startPath.matTool == null) { Exceptions.matToolException(); }

            double toolLength = mI.mach.toolLengthCompensation ? 0 : startPath.matTool.toolLength;

            if (mI.startPath.firstP == null) { Exceptions.nullPanic(); }
            Vector3d ab = Kinematics.ikFiveAxisABTable(mI.startPath.firstP, this.pivot, toolLength, out Point3d machPt);

            co.machineState.Clear();
            co.machineState.Add("X", machPt.X);
            co.machineState.Add("Y", machPt.Y);
            co.machineState.Add("Z", machPt.Z);
            co.machineState.Add("A", ab.X);
            co.machineState.Add("B", ab.Y);
            co.machineState.Add("F", -1);
            co.machineState.Add("S", -1);

            GCode.gcInstStart(this, ref co, mI, startPath);
        }
        public void writeFileEnd(ref CodeInfo co, MachineInstruction mI, ToolPath finalPath, ToolPath endPath) => GCode.gcInstEnd(this, ref co, mI, finalPath, endPath);
        public void writeOpStart(ref CodeInfo co, MachineOperation mO) => GCode.gcOpStart(this, ref co, mO);
        public void writeOpEnd(ref CodeInfo co, MachineOperation mO) => GCode.gcOpEnd(this, ref co, mO);

        // This should call a utility with standard options
        // a good time to move it is when a second 5-axis is added
        // hopefully at that point there is a better understanding of safe moves!

        public void writeTransition(ref CodeInfo co, ToolPath fP, ToolPath tP, bool first)
        {
            if (fP.matForm == null || tP.matForm == null) { Exceptions.matFormException(); }
            if (fP.matTool == null) { Exceptions.matToolException(); }

            // check there is anything to transition from or to
            if (fP.Count <= 0 || tP.Count <= 0) { return; }
            // See if we lie in the material
            // Check end of this path and start of TP
            // For each see if it is safe in one Material Form
            // As we pull back to safe distance we allow a little wiggle.
            if(fP.lastP == null || tP.firstP == null) { Exceptions.nullPanic(); }
            if (fP.matForm.intersect(fP.lastP, fP.matForm.safeDistance).thrDist > 0.0001
                && tP.matForm.intersect(fP.lastP, tP.matForm.safeDistance).thrDist > 0.0001 || fP.matForm.intersect(tP.firstP, fP.matForm.safeDistance).thrDist > 0.0001
                && tP.matForm.intersect(tP.firstP, tP.matForm.safeDistance).thrDist > 0.0001)
            {
                // If in material we probably need to throw an error
                // first path in an operation
                double length = fP.lastP.pt.DistanceTo(tP.firstP.pt);
                if (first) { co.addError("Transition between operations might be in material."); }
                else if (length > this.pathJump) // changing between paths in material
                {
                    co.addError("Long Transition between paths in material. \n"
                                + "To remove this error, don't use ignore, instead change PathJump for the machine from: "
                                + this.pathJump + " to at least: " + length);
                }
            }
            else // Safely move from one safe point to another.
            {
                ToolPath move = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                move.name = string.Empty;

                List<Point3d> route = new List<Point3d>();
                int i;

                route.Add(fP.lastP.pt);
                route.Add(tP.firstP.pt);

                // loop through intersecting with safe bubble and adding points
                for (i = 0; i < route.Count - 1 && route.Count < 1000;)
                {
                    if (tP.matForm.intersect(route[i], route[i + 1], tP.matForm.safeDistance, out MFintersects inters))
                    {
                        MFintersects fromMid = tP.matForm.intersect(inters.mid, inters.midOut, tP.matForm.safeDistance * 1.1);
                        route.Insert(i + 1, inters.mid + fromMid.thrDist * inters.midOut);
                    }
                    else
                    {
                        i++;
                    }
                }

                // add extra points if the angle change between steps is too large (pi/30)

                bool lng = false;
                // work out how far angle needs to move
                double angSpread = angDiff(fP.lastP, tP.firstP, fP.matTool, false);

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
                        Vector3d mixDir = interpolate(fP.lastP, tP.firstP, fP.matTool, shift,lng).dir;

                        ToolPoint newTP = new ToolPoint((j * route[i + 1] + (steps - j) * route[i]) / steps, mixDir, -1, 0);
                        if (fP.matForm.intersect(newTP, 0).thrDist > 0
                            || tP.matForm.intersect(newTP, 0).thrDist > 0)
                        {
                            if (lng)
                            {   // something has gone horribly wrong and
                                // both angle change directions will hit the material

                                throw new Exception("Safe Route failed to find a safe path from the end of one toolpath to the next.");
                            }
                            // start again with the longer angle change

                            lng = true;
                            i = 0;
                            j = 0;
                            angSpread = angDiff(fP.lastP, tP.firstP, fP.matTool, true);
                            steps = (int)Math.Ceiling(30 * angSpread / (Math.PI * route.Count));
                            move = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                        }
                        else
                        {
                            move.Add(newTP);
                        }
                    }
                }
                // get rid of start point that was already in the paths
                move.RemoveAt(0);
                writeCode(ref co, move);
            }
        }
    }
}