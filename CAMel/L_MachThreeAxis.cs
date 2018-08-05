using System;
using System.Collections.Generic;
using System.Text;

using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using CAMel.Types.MaterialForm;
using System.Text.RegularExpressions;

namespace CAMel.Types.Machine
{
    public class ThreeAxis : IGCodeMachine
    {
        public double pathJump { get; set; }
        public string sectionBreak { get; set; }
        public string speedChangeCommand { get; set; }
        public string fileStart { get; set; }
        public string fileEnd { get; set; }
        public string header { get; set; }
        public string footer { get; set; }
        public string name { get; set; }
        public string commentStart { get; set; }
        public string commentEnd { get; set; }

        public bool IsValid => throw new NotImplementedException();

        public ThreeAxis()
        {
            this.name = "Unamed Machine";
            this.header = String.Empty;
            this.footer = String.Empty;
            this.fileStart = String.Empty;
            this.fileEnd = String.Empty;
            this.commentStart = "(";
            this.commentEnd = ")";
            this.sectionBreak = "------------------------------------------";
            this.speedChangeCommand = "M03";
            this.pathJump = 1;
        }
        public ThreeAxis(string name, string header, string footer)
        {
            this.name = name;
            this.header = header;
            this.footer = footer;
            this.fileStart = String.Empty;
            this.fileEnd = String.Empty;
            this.commentStart = "(";
            this.commentEnd = ")";
            this.sectionBreak = "------------------------------------------";
            this.speedChangeCommand = "M03";
            this.pathJump = 1;
        }
        public ThreeAxis(ThreeAxis TA)
        {
            this.name = TA.name;
            this.header = TA.header;
            this.footer = TA.footer;
            this.fileStart = TA.fileStart;
            this.fileEnd = TA.fileEnd;
            this.commentStart = TA.commentStart;
            this.commentEnd = TA.commentEnd;
            this.sectionBreak = TA.sectionBreak;
            this.speedChangeCommand = TA.speedChangeCommand;
            this.pathJump = TA.pathJump;
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

        public ICAMel_Base Duplicate() => new ThreeAxis(this);

        public ToolPath insertRetract(ToolPath tP) => tP.MatForm.InsertRetract(tP);

        public ToolPoint Interpolate(ToolPoint toolPoint1, ToolPoint toolPoint2, double par)
        {
            ToolPoint TPo = new ToolPoint(toolPoint1);
            TPo.Pt = toolPoint1.Pt * par + toolPoint2.Pt * (1 - par);
            return TPo;
        }

        public ToolPath ReadCode(string Code)
        {
            List<char> terms = new List<char>();
            terms.Add('X');
            terms.Add('Y');
            terms.Add('Z');
            terms.Add('S');
            terms.Add('F');
            return GCode.Read(this,Code,terms);
        }
        public ToolPoint readTP(Dictionary<char, double> vals) => new ToolPoint(new Point3d(vals['X'], vals['Y'], vals['Z']), new Vector3d(0, 0, 0), vals['S'], vals['F']);

        public Vector3d toolDir(ToolPoint TP) => -Vector3d.ZAxis;

        public ToolPoint writeCode(ref CodeInfo Co, ToolPath tP, ToolPoint beforePoint)
        {
            GCode.GcPathStart(this, ref Co, tP);

            bool FChange = false;
            bool SChange = false;

            double feed;
            double speed;

            if (beforePoint == null) // There were no previous points
            {
                if (tP.Count > 0)
                {
                    if (tP[0].feed >= 0) { feed = tP[0].feed; }
                    else { feed = tP.MatTool.feedCut; }
                    if (tP[0].speed >= 0) { speed = tP[0].speed; }
                    else { speed = tP.MatTool.speed; }

                    // Only call Feed/speed if non-negative 
                    // so Material Tool can have -1 for speed/feed and ignore them
                    if (feed >= 0) { FChange = true; }
                    if (speed >= 0) { SChange = true; }
                }
                else
                {
                    feed = tP.MatTool.feedCut;
                    speed = tP.MatTool.speed;
                }
            }
            else
            {
                feed = beforePoint.feed;
                speed = beforePoint.speed;
            }

            string PtCode;

            foreach (ToolPoint Pt in tP)
            {
                if (Pt.error != null)
                {
                    foreach (string err in Pt.error)
                    {
                        Co.AddError(err);
                        Co.AppendComment(err);
                    }
                }
                if (Pt.warning != null)
                {
                    foreach (string warn in Pt.warning)
                    {
                        Co.AddWarning(warn);
                        Co.AppendComment(warn);
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
                    else if (feed != tP.MatTool.feedCut) // Default to the cut feed rate.
                    {
                        FChange = true;
                        feed = tP.MatTool.feedCut;
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
                PtCode = Kinematics.IK_ThreeAxis(Pt, tP.MatTool);
                
                // Act if feed has changed
                if (FChange)
                {
                    if (feed == 0)
                        PtCode = "G00 " + PtCode;
                    else
                        PtCode = "G01 " + PtCode + " F" + feed.ToString("0");
                }
                FChange = false;

                // Act if speed has changed
                if (SChange)
                {
                    PtCode = this.speedChangeCommand + " S" + speed.ToString("0") + "\n" + PtCode;
                }
                SChange = false;

                PtCode = Pt.preCode + PtCode + Pt.postCode;

                if (Pt.name != "")
                {
                    PtCode = PtCode + " " + this.comment(Pt.name);
                }
                Co.Append(PtCode);

                // Adjust ranges

                Co.GrowRange("X", Pt.Pt.X);
                Co.GrowRange("Y", Pt.Pt.Y);
                Co.GrowRange("Z", Pt.Pt.Z);

            }

            // return the last point or the beforePoint if the path had no elements
            ToolPoint PtOut;

            if (tP.Count > 0)
            {
                PtOut = new ToolPoint(tP[tP.Count - 1]);
                PtOut.feed = feed;
                PtOut.speed = speed;
            }
            else PtOut = beforePoint;

            return PtOut;
        }

        public void writeFileEnd(ref CodeInfo Co, MachineInstruction MI) => GCode.GcInstEnd(this, ref Co, MI);
        public void writeFileStart(ref CodeInfo Co, MachineInstruction MI) => GCode.GcInstStart(this, ref Co, MI);
        public void writeOpEnd(ref CodeInfo Co, MachineOperation MO) => GCode.GcOpEnd(this, ref Co, MO);
        public void writeOpStart(ref CodeInfo Co, MachineOperation MO) => GCode.GcOpStart(this, ref Co, MO);

        public ToolPoint writeTransition(ref CodeInfo Co, ToolPath fP, ToolPath tP, bool first, ToolPoint beforePoint)
        {
            ToolPoint outPoint = beforePoint;
            // check there is anything to transition from or to
            if (fP != null && fP.Count > 0 && tP != null && tP.Count > 0)
            {
                // See if we lie in the material
                // Check end of this path and start of TP
                // For each see if it is safe in one Material Form
                // As we pull back to safe distance we allow a little wiggle.
                if ((
                    fP.MatForm.intersect(fP[fP.Count - 1], fP.MatForm.safeDistance).thrDist > 0.0001
                    && tP.MatForm.intersect(fP[fP.Count - 1], tP.MatForm.safeDistance).thrDist > 0.0001
                    ) || (
                    fP.MatForm.intersect(tP[0], fP.MatForm.safeDistance).thrDist > 0.0001
                    && tP.MatForm.intersect(tP[0], tP.MatForm.safeDistance).thrDist > 0.0001
                    ))
                {
                    // If in material we probably need to throw an error
                    // first path in an operation
                    double Length = fP[fP.Count - 1].Pt.DistanceTo(tP[0].Pt);
                    if (first) { Co.AddError("Transition between operations might be in material."); }
                    else if (Length > this.pathJump) // changing between paths in material
                    {
                        Co.AddError("Long Transition between paths in material. \n"
                            + "To remove this error, don't use ignore, instead change PathJump for the machine from: "
                            + this.pathJump.ToString() + " to at least: " + Length.ToString());
                    }
                }
                else // Safely move from one safe point to another.
                {
                    // Start with a straight line, see how close it 
                    // comes to danger. If its too close add a new
                    // point and try again.

                    List<Point3d> route = new List<Point3d>();
                    route.Add(fP[fP.Count - 1].Pt);
                    route.Add(tP[0].Pt);

                    int i;
                    MFintersects inters;
                    MFintersects fromMid;

                    // loop through intersecting with safe bubble and adding points
                    for (i = 0; i < (route.Count - 1) && i < 100;)
                    {
                        if (tP.MatForm.intersect(route[i], route[i + 1], tP.MatForm.safeDistance, out inters))
                        {
                            fromMid = tP.MatForm.intersect(inters.mid, inters.midOut, tP.MatForm.safeDistance * 1.1);
                            route.Insert(i + 1, inters.mid + fromMid.thrDist * inters.midOut);
                        }
                        else {  i++; }
                    }

                    // get rid of start and end points that are already in the paths
                    route.RemoveAt(0);
                    route.RemoveAt(route.Count - 1);

                    ToolPath Move = tP.copyWithNewPoints(new List<ToolPoint>());
                    Move.name = String.Empty;
                    Move.preCode = String.Empty;
                    Move.postCode = String.Empty;

                    foreach (Point3d Pt in route)
                    {
                        // add new point at speed 0 to describe rapid move.
                        Move.Add(new ToolPoint(Pt, new Vector3d(), -1, 0));
                    }

                    outPoint = Move.WriteCode(ref Co, this, beforePoint);
                }
            }
            return outPoint;
        }
    }
}