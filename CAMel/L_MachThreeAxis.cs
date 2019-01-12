using System;
using System.Collections.Generic;

using Rhino.Geometry;

using CAMel.Types.MaterialForm;
using static CAMel.Exceptions;

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
        private readonly List<char> terms;
        public List<MaterialTool> MTs { get; }
        public ToolPathAdditions machineImplements { get; }

        internal static ToolPathAdditions _defaultImplents => new ToolPathAdditions()
        {
            insert = true,
            retract = true,
            stepDown = true,
            sdDropStart = true,
            sdDropMiddle = 1,
            sdDropEnd = true,
            threeAxisHeightOffset = true,
            tabbing = true,
            leadLength = 0
        };

        public ToolPathAdditions defaultTPA
        { get => ToolPathAdditions.basicDefault; }

        public ThreeAxis(string name, ToolPathAdditions tPA, List<MaterialTool> MTs, double pJ, string head, string foot, string speed, string tool, string commentStart, string commentEnd, string sectionBreak, string fileStart, string fileEnd)
        {
            this.name = name;
            this.machineImplements = tPA;
            this.pathJump = pJ;
            this.header = head;
            this.footer = foot;
            this.speedChangeCommand = speed;
            this.toolChangeCommand = tool;
            this.commentStart = commentStart;
            this.commentEnd = commentEnd;
            this.sectionBreak = sectionBreak;
            this.fileStart = fileStart;
            this.fileEnd = fileEnd;
            this.MTs = MTs;
            this.terms = new List<char> { 'X', 'Y', 'Z', 'S', 'F' };
        }

        public string TypeDescription
        { get { return @"Instructions for a 3-Axis machine"; } }
        public string TypeName
        { get { return @"CAMelThreeAxis"; } }
        public string comment(string L)
        {
            if (L == "" || L == " ") { return " "; }
            else { return this.commentStart + " " + L + " " + this.commentEnd; }
        }

        public ToolPath insertRetract(ToolPath tP) => tP.matForm.insertRetract(tP);

        public ToolPoint interpolate(ToolPoint fP, ToolPoint tP, MaterialTool MT, double par, bool lng)
        => Kinematics.interpolateLinear(fP, tP, par);

        public double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool MT, bool lng) => 0;

        public ToolPath readCode(string Code) => GCode.gcRead(this, this.MTs, Code, this.terms);
        public ToolPoint readTP(Dictionary<char, double> vals, MaterialTool MT) => new ToolPoint(new Point3d(vals['X'], vals['Y'], vals['Z']), new Vector3d(0, 0, 0), vals['S'], vals['F']);

        public Vector3d toolDir(ToolPoint TP) => Vector3d.ZAxis;

        public void writeCode(ref CodeInfo Co, ToolPath tP)
        {
            if (tP.matTool == null) { matToolException(); }
            // Double check tP does not have additions.
            if (tP.Additions.any) { additionsException(); }

            if (tP.Count > 0) // Just ignore 0 length paths
            {
                GCode.gcPathStart(this, ref Co, tP);

                bool FChange = false;
                bool SChange = false;

                double feed = Co.machineState["F"];
                double speed = Co.machineState["S"];
                if (feed < 0) { feed = tP.matTool.feedCut; FChange = true; }
                if (speed < 0) { speed = tP.matTool.speed; SChange = true; }

                string PtCode;

                foreach (ToolPoint Pt in tP)
                {
                    if (Pt.error != null)
                    {
                        foreach (string err in Pt.error)
                        {
                            Co.addError(err);
                            Co.appendComment(err);
                        }
                    }
                    if (Pt.warning != null)
                    {
                        foreach (string warn in Pt.warning)
                        {
                            Co.addWarning(warn);
                            Co.appendComment(warn);
                        }
                    }

                    // Establish new feed value
                    if (Pt.feed != feed)
                    {
                        if (Pt.feed >= 0)
                        {
                            FChange = true;
                            feed = Pt.feed;
                        }
                        else if (feed != tP.matTool.feedCut) // Default to the cut feed rate.
                        {
                            FChange = true;
                            feed = tP.matTool.feedCut;
                        }
                    }

                    // Establish new speed value
                    if (Pt.speed != speed)
                    {
                        if (Pt.speed > 0)
                        {
                            SChange = true;
                            speed = Pt.speed;
                        }
                    }

                    // Add the position information
                    PtCode = GCode.gcThreeAxis(Pt);

                    // Act if feed has changed
                    if (FChange)
                    {
                        if (feed == 0) { PtCode = "G00 " + PtCode; }
                        else { PtCode = "G01 " + PtCode + " F" + feed.ToString("0"); }
                    }
                    FChange = false;

                    // Act if speed has changed
                    if (SChange)
                    {
                        PtCode = this.speedChangeCommand + " S" + speed.ToString("0") + "\n" + PtCode;
                    }
                    SChange = false;

                    PtCode = Pt.preCode + PtCode + Pt.postCode;

                    if (Pt.name != string.Empty)
                    {
                        PtCode = PtCode + " " + this.comment(Pt.name);
                    }
                    Co.append(PtCode);

                    // Adjust ranges

                    Co.growRange("X", Pt.pt.X);
                    Co.growRange("Y", Pt.pt.Y);
                    Co.growRange("Z", Pt.pt.Z);
                }
                // Pass machine state information

                Co.machineState.Clear();
                Co.machineState.Add("X", tP.lastP.pt.X);
                Co.machineState.Add("Y", tP.lastP.pt.Y);
                Co.machineState.Add("Z", tP.lastP.pt.Z);
                Co.machineState.Add("F", feed);
                Co.machineState.Add("S", speed);
            }
        }
        public void writeFileStart(ref CodeInfo Co, MachineInstruction MI, ToolPath startPath)
        {
            // Set up Machine State  

            Co.machineState.Clear();
            Co.machineState.Add("X", startPath.firstP.pt.X);
            Co.machineState.Add("Y", startPath.firstP.pt.Y);
            Co.machineState.Add("Z", startPath.firstP.pt.Z);
            Co.machineState.Add("F", -1);
            Co.machineState.Add("S", -1);

            GCode.gcInstStart(this, ref Co, MI, startPath);
        }
        public void writeFileEnd(ref CodeInfo Co, MachineInstruction MI, ToolPath finalPath, ToolPath endPath) => GCode.gcInstEnd(this, ref Co, MI, finalPath, endPath);
        public void writeOpStart(ref CodeInfo Co, MachineOperation MO) => GCode.gcOpStart(this, ref Co, MO);
        public void writeOpEnd(ref CodeInfo Co, MachineOperation MO) => GCode.gcOpEnd(this, ref Co, MO);
        
        public void writeTransition(ref CodeInfo Co, ToolPath fP, ToolPath tP, bool first)
        {
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
                    double Length = fP.lastP.pt.DistanceTo(tP.firstP.pt);
                    if (first) { Co.addError("Transition between operations might be in material."); }
                    else if (Length > this.pathJump) // changing between paths in material
                    {
                        Co.addError("Long Transition between paths in material. \n"
                            + "To remove this error, don't use ignore, instead change PathJump for the machine from: "
                            + this.pathJump.ToString() + " to at least: " + Length.ToString());
                    }
                }
                else // Safely move from one safe point to another.
                {
                    // Start with a straight line, see how close it 
                    // comes to danger. If its too close add a new
                    // point and try again.

                    List<Point3d> route = new List<Point3d> { fP.lastP.pt, tP.firstP.pt };

                    int i;
                    MFintersects inters;
                    MFintersects fromMid;

                    // loop through intersecting with safe bubble and adding points
                    for (i = 0; i < (route.Count - 1) && i < 100;)
                    {
                        if (tP.matForm.intersect(route[i], route[i + 1], tP.matForm.safeDistance, out inters))
                        {
                            fromMid = tP.matForm.intersect(inters.mid, inters.midOut, tP.matForm.safeDistance * 1.1);
                            route.Insert(i + 1, inters.mid + fromMid.thrDist * inters.midOut);
                        }
                        else {  i++; }
                    }

                    // get rid of start and end points that are already in the paths
                    route.RemoveAt(0);
                    route.RemoveAt(route.Count - 1);

                    ToolPath Move = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                    Move.name = String.Empty;
                    Move.preCode = String.Empty;
                    Move.postCode = String.Empty;

                    foreach (Point3d Pt in route)
                    {
                        // add new point at speed 0 to describe rapid move.
                        Move.Add(new ToolPoint(Pt, new Vector3d(), -1, 0));
                    }

                    this.writeCode(ref Co, Move);
                }
            }
        }
    }
}