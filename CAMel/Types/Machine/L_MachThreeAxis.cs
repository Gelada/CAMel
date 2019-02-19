using System;
using System.Collections.Generic;
using CAMel.Types.MaterialForm;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.Types.Machine
{
    public class ThreeAxis : IGCodeMachine
    {
        public string name { get; }
        public double pathJump { get; }
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
        [NotNull] private readonly List<char> _terms;
        public List<MaterialTool> mTs { get; }

        [NotNull] public ToolPathAdditions defaultTPA => ToolPathAdditions.basicDefault;

        public ThreeAxis([NotNull] string name,
            [NotNull] List<MaterialTool> mTs,
            double pJ,
            [NotNull] string head,
            [NotNull] string foot,
            [NotNull] string speed,
            [NotNull] string tool,
            [NotNull] string commentStart,
            [NotNull] string commentEnd,
            [NotNull] string sectionBreak,
            [NotNull] string fileStart,
            [NotNull] string fileEnd)
        {
            this.name = name;
            this.pathJump = pJ;
            this.toolLengthCompensation = false;
            this.header = head;
            this.footer = foot;
            this.speedChangeCommand = speed;
            this.toolChangeCommand = tool;
            this.commentStart = commentStart;
            this.commentEnd = commentEnd;
            this.sectionBreak = sectionBreak;
            this.fileStart = fileStart;
            this.fileEnd = fileEnd;
            this.mTs = mTs;
            this._terms = new List<char> { 'X', 'Y', 'Z', 'S', 'F' };
        }

        public string TypeDescription => @"Instructions for a 3-Axis machine";

        public string TypeName => @"CAMelThreeAxis";

        public override string ToString() => "2Axis: " + this.name;

        public string comment(string l) => GCode.comment(this, l);

        public ToolPath insertRetract(ToolPath tP)
        {
            if(tP.matForm == null) { Exceptions.matFormException(); }
            return tP.matForm.insertRetract(tP);
        }
        public List<List<ToolPath>> stepDown(ToolPath tP) => Utility.stepDown(tP, this);
        public ToolPath threeAxisHeightOffset(ToolPath tP) => Utility.threeAxisHeightOffset(tP, this);
        public List<ToolPath> finishPaths(ToolPath tP) => Utility.finishPaths(tP, this);

        public ToolPoint interpolate(ToolPoint fP, ToolPoint tP, MaterialTool mT, double par, bool lng)
        => Kinematics.interpolateLinear(fP, tP, par);

        public double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool mT, bool lng) => 0;

        public ToolPath readCode(string code) => GCode.gcRead(this, this.mTs, code, this._terms);
        public ToolPoint readTP(Dictionary<char, double> values, MaterialTool mT) => new ToolPoint(new Point3d(values['X'], values['Y'], values['Z']), new Vector3d(0, 0, 0), values['S'], values['F']);

        public Vector3d toolDir(ToolPoint tP) => Vector3d.ZAxis;

        public void writeCode(ref CodeInfo co, [ItemNotNull] ToolPath tP)
        {
            if (tP.matTool == null) { Exceptions.matToolException(); }
            // Double check tP does not have additions.
            if (tP.additions != null && tP.additions.any) { Exceptions.additionsException(); }

            if (tP.Count <= 0) { return; }

            GCode.gcPathStart(this, ref co, tP);

            bool fChange = false;
            bool sChange = false;

            double feed = co.machineState["F"];
            double speed = co.machineState["S"];
            if (feed < 0) { feed = tP.matTool.feedCut; fChange = true; }
            if (speed < 0) { speed = tP.matTool.speed; sChange = true; }

            foreach (ToolPoint tPt in tP)
            {
                foreach (string err in tPt.error)
                {
                    co.addError(err);
                    co.appendComment(err);
                }
                foreach (string warn in tPt.warning)
                {
                    co.addWarning(warn);
                    co.appendComment(warn);
                }


                // Establish new feed value
                if (Math.Abs(tPt.feed - feed) > CAMel_Goo.Tolerance)
                {
                    if (tPt.feed >= 0)
                    {
                        fChange = true;
                        feed = tPt.feed;
                    } else if (Math.Abs(feed - tP.matTool.feedCut) > CAMel_Goo.Tolerance
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
                    if (Math.Abs(feed) < CAMel_Goo.Tolerance) { ptCode = "G00 " + ptCode; } else
                    {
                        ptCode = "G01 " + ptCode + " F" + feed.ToString("0");
                    }
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

            co.machineState.Clear();
            co.machineState.Add("X", tP.lastP.pt.X);
            co.machineState.Add("Y", tP.lastP.pt.Y);
            co.machineState.Add("Z", tP.lastP.pt.Z);
            co.machineState.Add("F", feed);
            co.machineState.Add("S", speed);

            GCode.gcPathEnd(this, ref co, tP);
        }
        public void writeFileStart(ref CodeInfo co, MachineInstruction mI, ToolPath startPath)
        {
            // Set up Machine State

            if(startPath.firstP == null) { Exceptions.noToolPathException(); }

            co.machineState.Clear();
            co.machineState.Add("X", startPath.firstP.pt.X);
            co.machineState.Add("Y", startPath.firstP.pt.Y);
            co.machineState.Add("Z", startPath.firstP.pt.Z);
            co.machineState.Add("F", -1);
            co.machineState.Add("S", -1);

            GCode.gcInstStart(this, ref co, mI, startPath);
        }
        public void writeFileEnd(ref CodeInfo co, MachineInstruction mI, ToolPath finalPath, ToolPath endPath) => GCode.gcInstEnd(this, ref co, mI, finalPath, endPath);
        public void writeOpStart(ref CodeInfo co, MachineOperation mO) => GCode.gcOpStart(this, ref co, mO);
        public void writeOpEnd(ref CodeInfo co, MachineOperation mO) => GCode.gcOpEnd(this, ref co, mO);

        public void writeTransition(ref CodeInfo co, ToolPath fP, ToolPath tP, bool first)
        {
            if(fP.matForm == null || tP.matForm == null) { Exceptions.matFormException(); }
            // check there is anything to transition from or to
            if (fP.Count <= 0 || tP.Count <= 0) { return; }
            // See if we lie in the material
            // Check end of this path and start of TP
            // For each see if it is safe in one Material Form
            // As we pull back to safe distance we allow a little wiggle.
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
                // Start with a straight line, see how close it
                // comes to danger. If its too close add a new
                // point and try again.

                List<Point3d> route = new List<Point3d> { fP.lastP.pt, tP.firstP.pt };

                int i;

                // loop through intersecting with safe bubble and adding points
                for (i = 0; i < route.Count - 1 && i < 100;)
                {
                    if (tP.matForm.intersect(route[i], route[i + 1], tP.matForm.safeDistance, out MFintersects inters))
                    {
                        MFintersects fromMid = tP.matForm.intersect(inters.mid, inters.midOut, tP.matForm.safeDistance * 1.1);
                        route.Insert(i + 1, inters.mid + fromMid.thrDist * inters.midOut);
                    }
                    else {  i++; }
                }

                // get rid of start and end points that are already in the paths
                route.RemoveAt(0);
                route.RemoveAt(route.Count - 1);

                ToolPath move = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                move.name = string.Empty;
                move.preCode = string.Empty;
                move.postCode = string.Empty;

                foreach (Point3d pt in route)
                {
                    // add new point at speed 0 to describe rapid move.
                    move.Add(new ToolPoint(pt, new Vector3d(), -1, 0));
                }

                writeCode(ref co, move);
            }
        }
    }
}