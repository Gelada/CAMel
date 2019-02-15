using System;
using System.Collections.Generic;

using Rhino.Geometry;

using static CAMel.Exceptions;


namespace CAMel.Types.Machine
{
    public class TwoAxis : IGCodeMachine
    {
        public string name { get; }
        public double pathJump { get;}
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
        private readonly List<char> _terms;
        public List<MaterialTool> mTs { get; }

        public string insert { get; }
        public string retract { get; }

        public ToolPathAdditions defaultTPA { get => ToolPathAdditions.twoAxisDefault; }

        public TwoAxis(string name, List<MaterialTool>mTs, double pJ, string header, string footer, 
            string speed, string insert, string retract, string tool, string commentStart, string commentEnd, 
            string sectionBreak, string fileStart, string fileEnd)
        {
            this.name = name;
            this.header = header;
            this.footer = footer;
            this.pathJump = pJ;
            this.toolLengthCompensation = false;
            this.speedChangeCommand = speed;
            this.insert = insert;
            this.retract = retract;
            this.commentStart = commentStart;
            this.commentEnd = commentEnd;
            this.sectionBreak = sectionBreak;
            this.fileStart = fileStart;
            this.fileEnd = fileEnd;
            this.speedChangeCommand = speed;
            this.toolChangeCommand = tool;
            this.mTs = mTs;
            this._terms = new List<char> { 'X', 'Y', 'S', 'F' };
        }

        public string TypeDescription => @"Instructions for a 2-Axis machine";
        public string TypeName => @"CAMelTwoAxis";

        public override string ToString() => "2Axis: " + this.name;

        public string comment(string l) => GCode.comment(this, l);

        public ToolPath insertRetract(ToolPath tP) => Utility.leadInOut2D(tP, this.insert, this.retract);
        public List<List<ToolPath>> stepDown(ToolPath tP) => new List<List<ToolPath>>();
        public ToolPath threeAxisHeightOffset(ToolPath tP) => Utility.clearThreeAxisHeightOffset(tP);
        public List<ToolPath> finishPaths(ToolPath tP) => Utility.oneFinishPath(tP);

        public ToolPoint interpolate(ToolPoint fP, ToolPoint tP, MaterialTool mT, double par, bool lng)
        => Kinematics.interpolateLinear(fP, tP, par);
        public double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool mT, bool lng) => 0;

        public ToolPath readCode(string code) => GCode.gcRead(this,this.mTs,code, this._terms);
        public ToolPoint readTP(Dictionary<char, double> vals, MaterialTool mT) => new ToolPoint(new Point3d(vals['X'], vals['Y'],0), new Vector3d(0, 0, 0), vals['S'], vals['F']);

        public Vector3d toolDir(ToolPoint tP) => Vector3d.ZAxis;

        public void writeCode(ref CodeInfo co, ToolPath tP)
        {
            if (tP.matTool == null) { matToolException(); return; }
            // Double check tP does not have additions.
            if (tP.additions.any) { additionsException(); return; }

            if (tP.Count > 0) // Just ignore 0 length paths
            {
                GCode.gcPathStart(this, ref co, tP);

                bool fChange = false;
                bool sChange = false;

                double feed = co.machineState["F"];
                double speed = co.machineState["S"];
                if (feed < 0) { feed = tP.matTool.feedCut; fChange = true; }
                if (speed < 0) { speed = tP.matTool.speed; sChange = true; }

                string PtCode;

                foreach (ToolPoint pt in tP)
                {
                    if (pt.error != null)
                    {
                        foreach (string err in pt.error)
                        {
                            co.addError(err);
                            co.appendComment(err);
                        }
                    }
                    if (pt.warning != null)
                    {
                        foreach (string warn in pt.warning)
                        {
                            co.addWarning(warn);
                            co.appendComment(warn);
                        }
                    }

                    // Establish new feed value
                    if (Math.Abs(pt.feed - feed) > CAMel_Goo.tolerance)
                    {
                        if (pt.feed >= 0)
                        {
                            fChange = true;
                            feed = pt.feed;
                        }
                        else if (Math.Abs(feed - tP.matTool.feedCut) > CAMel_Goo.tolerance) // Default to the cut feed rate.
                        {
                            fChange = true;
                            feed = tP.matTool.feedCut;
                        }
                    }

                    // Establish new speed value
                    if (Math.Abs(pt.speed - speed) > CAMel_Goo.tolerance)
                    {
                        if (pt.speed > 0)
                        {
                            sChange = true;
                            speed = pt.speed;
                        }
                    }

                    // Add the position information
                    PtCode = GCode.gcTwoAxis(pt);

                    // Act if feed has changed
                    if (fChange && feed >= 0)
                    {
                        if (Math.Abs(feed) < CAMel_Goo.tolerance) { PtCode = "G00 " + PtCode; }
                        else { PtCode = "G01 " + PtCode + " F" + feed.ToString("0"); }
                    }
                    fChange = false;

                    // Act if speed has changed
                    if (sChange && speed >= 0)
                    { PtCode = this.speedChangeCommand + " S" + speed.ToString("0") + "\n" + PtCode; }
                    sChange = false;

                    if (pt.name != string.Empty) { PtCode = PtCode + " " + comment(pt.name); }

                    PtCode = pt.preCode + PtCode + pt.postCode;

                    co.append(PtCode);

                    // Adjust ranges

                    co.growRange("X", pt.pt.X);
                    co.growRange("Y", pt.pt.Y);
                }
                // Pass machine state information

                co.machineState.Clear();
                co.machineState.Add("X", tP.lastP.pt.X);
                co.machineState.Add("Y", tP.lastP.pt.Y);
                co.machineState.Add("F", feed);
                co.machineState.Add("S", speed);

                GCode.gcPathEnd(this, ref co, tP);
            }       
        }

        public void writeFileStart(ref CodeInfo co, MachineInstruction mI, ToolPath startPath)
        {
            // Set up Machine State  

            co.machineState.Clear();
            co.machineState.Add("X", startPath.firstP.pt.X);
            co.machineState.Add("Y", startPath.firstP.pt.Y);
            co.machineState.Add("F", -1);
            co.machineState.Add("S", -1);

            GCode.gcInstStart(this, ref co, mI, startPath);
        }
        public void writeFileEnd(ref CodeInfo co, MachineInstruction mI, ToolPath finalPath, ToolPath endPath) => GCode.gcInstEnd(this, ref co, mI, finalPath, endPath);
        public void writeOpEnd(ref CodeInfo co, MachineOperation mO) => GCode.gcOpEnd(this, ref co, mO);
        public void writeOpStart(ref CodeInfo co, MachineOperation mO) => GCode.gcOpStart(this, ref co, mO);

        public void writeTransition(ref CodeInfo co, ToolPath fP, ToolPath tP, bool first)
        {
            // check there is anything to transition from
            if ((fP.Count > 0) && tP.Count > 0)
            {
                List<Point3d> route = new List<Point3d> { fP.lastP.pt, tP.firstP.pt };

                ToolPath move = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                move.name = String.Empty;
                move.preCode = String.Empty;
                move.postCode = String.Empty;
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