using System;
using System.Collections.Generic;

using Rhino.Geometry;

using CAMel.Types.MaterialForm;
using static CAMel.Exceptions;

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
        private readonly List<char> _terms;
        public List<MaterialTool> mTs { get; }

        private double aMin { get; }
        private double aMax { get; }
        private double bMax { get; }
        public bool toolLengthCompensation { get; }

        public Vector3d pivot { get; } // Position of machine origin in design space.

        public ToolPathAdditions defaultTPA
        { get => ToolPathAdditions.basicDefault; }

        public PocketNC(string name, string header, string footer, Vector3d pivot, double aMin, double aMax, double bMax, bool tLc, double pathJump, List<MaterialTool> mTs)
        {
            this.name = name;
            this.toolLengthCompensation = tLc;
            this.header = header;
            this.footer = footer;
            this.fileStart = String.Empty;
            this.fileEnd = String.Empty;
            this.commentStart = GCode.defaultCommentStart;
            this.commentEnd = GCode.defaultCommentEnd;
            this.sectionBreak = GCode.defaultSectionBreak;
            this.speedChangeCommand = GCode.defaultSpeedChangeCommand;
            this.toolChangeCommand = GCode.defaultToolChangeCommand;
            this.pathJump = pathJump;
            this.mTs = mTs;
            this.pivot = pivot;
            this.aMin = aMin;
            this.aMax = aMax;
            this.bMax = bMax;
            this._terms = new List<char> { 'X', 'Y', 'Z', 'A', 'B', 'S', 'F' };
        }

        public string TypeDescription
        { get { return @"Instructions for a PocketNC machine"; } }
        public string TypeName
        { get { return @"CAMelPocketNC"; } }

        public override string ToString() => this.name;

        public string comment(string l) => GCode.comment(this, l);

        public ToolPath insertRetract(ToolPath tP)
        {
            if (tP.matForm != null) { return tP.matForm.insertRetract(tP); }

            matFormException();
            return null;
        }

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

        public ToolPath readCode(string code)
        {
            if(this.mTs.Count == 0) { noToolException(); }
            return GCode.gcRead(this, this.mTs, code, this._terms);
        }
        public ToolPoint readTP(Dictionary<char, double> vals, MaterialTool mT)
        {
            Point3d machPt = new Point3d(vals['X'], vals['Y'], vals['Z']);
            Vector3d ab = new Vector3d(vals['A']*Math.PI/180.0, vals['B'] * Math.PI/180.0, 0);

            ToolPoint tP = new ToolPoint
            {
                speed = vals['S'],
                feed = vals['F']
            };

            double toolLength = this.toolLengthCompensation ? 0 : mT.toolLength;

            return Kinematics.kFiveAxisABTable(tP, this.pivot, toolLength, machPt, ab);
        }

        public Vector3d toolDir(ToolPoint tP) => tP.dir;

        readonly double AngleAcc = 0.0001; // accuracy of angles to assume we lie on the cusp.

        public void writeCode(ref CodeInfo co, ToolPath tP)
        {
            // Double check tP does not have additions.
            if (tP.additions.any) { additionsException(); }

            if (tP.Count > 0) // Just ignore 0 length paths
            {
                if (tP.matTool == null) { matToolException(); return; }
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

                Vector3d ab = new Vector3d(co.machineState["A"], co.machineState["B"], 0), newAB;

                double bTo = 0;  // Allow for smooth adjustment through the cusp with A at 90.
                int bSteps = 0;  //

                Point3d machPt = new Point3d();

                double toolLength = this.toolLengthCompensation ? 0 : tP.matTool.toolLength;

                int i, j;
                ToolPoint tPt;
                for (i = 0; i < tP.Count; i++)
                {
                    tPt = tP[i];

                    if (tPt.error != null)
                    {
                        foreach (string err in tPt.error) { co.addError(err); }
                    }
                    if (tPt.warning != null)
                    {
                        foreach (string warn in tPt.warning) { co.addWarning(warn); }
                    }

                    // Establish new feed value
                    if (Math.Abs(tPt.feed - feed) > CAMel_Goo.tolerance)
                    {
                        if (tPt.feed >= 0)
                        {
                            fChange = true;
                            feed = tPt.feed;
                        }
                        else if (Math.Abs(feed - tP.matTool.feedCut) > CAMel_Goo.tolerance) // Default to the cut feed rate.
                        {
                            fChange = true;
                            feed = tP.matTool.feedCut;
                        }
                    }

                    // Establish new speed value
                    if (Math.Abs(tPt.speed - speed) > CAMel_Goo.tolerance)
                    {
                        if (tPt.speed > 0)
                        {
                            sChange = true;
                            speed = tPt.speed;
                        }
                    }

                    // Work on tool orientation

                    // get naive orientation and Machine XYZ position
                    newAB = Kinematics.ikFiveAxisABTable(tPt, this.pivot, toolLength, out machPt);

                    // adjust B to correct period
                    newAB.Y = newAB.Y + 2.0 * Math.PI * Math.Round((ab.Y - newAB.Y) / (2.0 * Math.PI));

                    // set A to 90 if it is close (to avoid a lot of messing with B for no real reason)

                    if (Math.Abs(newAB.X - Math.PI / 2.0) < this.AngleAcc) { newAB.X = Math.PI / 2.0; }

                    // adjust through cusp

                    if (Math.Abs(newAB.X - Math.PI / 2.0) < CAMel_Goo.tolerance) // already set if nearly there.
                    {
                        // detect that we are already moving
                        if (bSteps > 0)
                        {
                            newAB.Y = ab.Y + (bTo - ab.Y) / bSteps;
                            bSteps--;
                        }
                        else // head forward to next non-vertical point or the end.
                        {
                            j = i + 1;

                            while (j < (tP.Count - 1) &&
                                Math.Abs(
                                    Kinematics.ikFiveAxisABTable(tP[j], this.pivot, toolLength, out machPt).X
                                    - Math.PI / 2.0) < this.AngleAcc)
                            { j++; }

                            // If we are at the start of a path and vertical then we can just use the first non-vertical
                            // position for the whole run.
                            if (Math.Abs(ab.X - Math.PI / 2.0) < this.AngleAcc)
                            {
                                bTo = Kinematics.ikFiveAxisABTable(tP[j], this.pivot, toolLength, out machPt).Y;
                                bSteps = j - i;
                                newAB.Y = bTo;
                            }
                            // if we get to the end and it is still vertical we do not need to rotate.
                            else if (Math.Abs(Kinematics.ikFiveAxisABTable(tP[j], this.pivot, toolLength, out machPt).X) < this.AngleAcc)
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
                    // This will mean some cutable paths bcome impossible.
                    // This assumes only a double stance in positive position.

                    if (newAB.X > (Math.PI - this.aMax)) // check if double stance is possible
                    {
                        if ((newAB.Y - ab.Y) > Math.PI) // check for big rotation in B
                        {
                            newAB.X = Math.PI - newAB.X;
                            newAB.Y = newAB.Y - Math.PI;
                        }
                        else if ((newAB.Y - ab.Y) < -Math.PI) // check for big rotation in B
                        {
                            newAB.X = Math.PI - newAB.X;
                            newAB.Y = newAB.Y + Math.PI;
                        }
                    }

                    // (throw bounds error if B goes past +-Bmax degrees or A is not between Amin and Amax)


                    if (Math.Abs(newAB.Y) > this.bMax)
                    {
                        co.addError("Out of bounds on B");
                    }
                    if ((newAB.X > this.aMax) || (newAB.X < this.aMin))
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
                        if (Math.Abs(feed) < CAMel_Goo.tolerance) { ptCode = "G00 " + ptCode; }
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
        }

        public void writeFileStart(ref CodeInfo co, MachineInstruction mI, ToolPath startPath)
        {
            // Set up Machine State

            if (startPath.matTool == null) { matToolException(); return; }

            Point3d machPt;
            double toolLength = mI.mach.toolLengthCompensation ? 0 : startPath.matTool.toolLength;
            Vector3d ab = Kinematics.ikFiveAxisABTable(mI.startPath.firstP, this.pivot, toolLength, out machPt);

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
            if (fP.matForm == null) { matFormException(); return; }
            if (tP.matForm == null) { matFormException(); return; }

            // check there is anything to transition from or to
            if (fP.Count > 0 && tP.Count > 0)
            {
                // See if we lie in the material
                // Check end of this path and start of TP
                // For each see if it is safe in one Material Form
                // As we pull back to safe distance we allow a little wiggle.
                if ((
                    fP.matForm.intersect(fP.lastP, fP.matForm.safeDistance).thrDist > 0.0001
                    && tP.matForm.intersect(fP.lastP, tP.matForm.safeDistance).thrDist > 0.0001
                    ) || (
                    fP.matForm.intersect(tP.firstP, fP.matForm.safeDistance).thrDist > 0.0001
                    && tP.matForm.intersect(tP.firstP, tP.matForm.safeDistance).thrDist > 0.0001
                    ))
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
                    MFintersects inters;
                    MFintersects fromMid;

                    route.Add(fP.lastP.pt);
                    route.Add(tP.firstP.pt);

                    // loop through intersecting with safe bubble and adding points
                    for (i = 0; i < (route.Count - 1) && route.Count < 1000;)
                    {
                        if (tP.matForm.intersect(route[i], route[i + 1], tP.matForm.safeDistance, out inters))
                        {
                            fromMid = tP.matForm.intersect(inters.mid, inters.midOut, tP.matForm.safeDistance * 1.1);
                            route.Insert(i + 1, inters.mid + fromMid.thrDist * inters.midOut);
                        }
                        else
                        {
                            i++;
                        }
                    }

                    // add extra points if the angle change between steps is too large (pi/30)

                    Vector3d mixDir;
                    bool lng = false;
                    // work out how far angle needs to move
                    double angSpread = angDiff(fP.lastP, tP.firstP,fP.matTool, false);

                    int steps = (int)Math.Ceiling(30 * angSpread / (Math.PI * route.Count));
                    if (steps == 0) { steps = 1; } // Need to add at least one point even if angSpread is 0
                    int j;

                    // Try to build a path with angles.
                    // If a tool line hits the material
                    // switch to the longer rotate and try again

                    for (i = 0; i < (route.Count - 1); i++)
                    {
                        double shift;
                        // add new points at speed 0 to describe rapid move.
                        for (j = 0; j < steps; j++)
                        {
                            shift = (steps * i + j) / (double)(steps * (route.Count - 1));
                            mixDir = interpolate(fP.lastP, tP.firstP, fP.matTool, shift,lng).dir;

                            ToolPoint newTP = new ToolPoint((j * route[i + 1] + (steps - j) * route[i]) / steps, mixDir, -1, 0);
                            if ((fP.matForm.intersect(newTP, 0).thrDist > 0
                                || tP.matForm.intersect(newTP, 0).thrDist > 0))
                            {
                                if (lng)
                                {   // something has gone horribly wrong and
                                    // both angle change directions will hit the material

                                    throw new Exception("Safe Route failed to find a safe path from the end of one toolpath to the next.");
                                }
                                else
                                { // start again with the longer angle change

                                    lng = true;
                                    i = 0;
                                    j = 0;
                                    angSpread = angDiff(fP.lastP, tP.firstP, fP.matTool, true);
                                    steps = (int)Math.Ceiling(30 * angSpread / (Math.PI * route.Count));
                                    move = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                                }
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
}