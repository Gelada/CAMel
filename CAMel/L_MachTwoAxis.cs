using System;
using System.Collections.Generic;
using System.Text;

using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using CAMel.Types.MaterialForm;
using System.Text.RegularExpressions;

namespace CAMel.Types.Machine
{
    public class TwoAxis : IGCodeMachine
    {
        public string name { get; set; }
        public double pathJump { get; set; }
        public bool toolLengthCompensation { get; set; } // Tool Length Compensation
        public string sectionBreak { get; set; }
        public string speedChangeCommand { get; set; }
        public string toolChangeCommand { get; set; }
        public string fileStart { get; set; }
        public string fileEnd { get; set; }
        public string header { get; set; }
        public string footer { get; set; }
        public string commentStart { get; set; }
        public string commentEnd { get; set; }
        List<char> terms;

        public double leads { get; set; }

        public bool IsValid => throw new NotImplementedException();

        public TwoAxis()
        {
            this.name = "Unamed 2-Axis Machine";
            this.toolLengthCompensation = false;
            this.header = String.Empty;
            this.footer = String.Empty;
            this.fileStart = String.Empty;
            this.fileEnd = String.Empty;
            this.commentStart = "(";
            this.commentEnd = ")";
            this.sectionBreak = "------------------------------------------";
            this.speedChangeCommand = "M03";
            this.toolChangeCommand = "G43H";
            this.pathJump = 1;
            this.leads = 1;
            setTerms();
        }
        public TwoAxis(string name, string header, string footer)
        {
            this.name = name;
            this.toolLengthCompensation = false;
            this.header = header;
            this.footer = footer;
            this.fileStart = String.Empty;
            this.fileEnd = String.Empty;
            this.commentStart = "(";
            this.commentEnd = ")";
            this.sectionBreak = "------------------------------------------";
            this.speedChangeCommand = "M03";
            this.toolChangeCommand = "G43H";
            this.pathJump = 1;
            this.leads = 1;
            setTerms();
        }
        public TwoAxis(TwoAxis TA)
        {
            this.name = TA.name;
            this.toolLengthCompensation = TA.toolLengthCompensation;
            this.header = TA.header;
            this.footer = TA.footer;
            this.fileStart = TA.fileStart;
            this.fileEnd = TA.fileEnd;
            this.commentStart = TA.commentStart;
            this.commentEnd = TA.commentEnd;
            this.sectionBreak = TA.sectionBreak;
            this.speedChangeCommand = TA.speedChangeCommand;
            this.toolChangeCommand = TA.toolChangeCommand;
            this.pathJump = TA.pathJump;
            this.leads = TA.leads;
            this.terms = new List<char>();
            this.terms.AddRange(TA.terms);

        }

        private void setTerms()
        {
            this.terms = new List<char>{'X','Y','S','F'};
        }

        public string TypeDescription => @"Instructions for a 2-Axis machine";
        public string TypeName => @"CAMelTwoAxis";

        public string comment(string L)
        {
            if (L == "" || L == " ") { return " "; }
            else { return this.commentStart + " " + L + " " + this.commentEnd; }
        }

        public ICAMel_Base Duplicate() => new TwoAxis(this);

        public ToolPath insertRetract(ToolPath tP) => Utility.leadInOut2d(tP, this.leads);
        
        public ToolPoint interpolate(ToolPoint fP, ToolPoint tP, MaterialTool MT, double par, bool lng)
        => Kinematics.interpolateLinear(fP, tP, par);
        public double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool MT, bool lng) => 0;

        public ToolPath readCode(List<MaterialTool> MTs, string Code)
        {
            return GCode.gcRead(this,MTs,Code, this.terms);
        }
        public ToolPoint readTP(Dictionary<char, double> vals, MaterialTool MT) => new ToolPoint(new Point3d(vals['X'], vals['Y'],0), new Vector3d(0, 0, 0), vals['S'], vals['F']);

        public Vector3d toolDir(ToolPoint TP) => Vector3d.ZAxis;

        public ToolPoint writeCode(ref CodeInfo Co, ToolPath tP, ToolPoint beforePoint)
        {
            GCode.gcPathStart(this, ref Co, tP);

            bool FChange = false;
            bool SChange = false;

            double feed;
            double speed;

            if (beforePoint == null) // There were no previous points
            {
                if (tP.Count > 0)
                {
                    if (tP[0].feed >= 0) { feed = tP[0].feed; }
                    else { feed = tP.matTool.feedCut; }
                    if (tP[0].speed >= 0) { speed = tP[0].speed; }
                    else { speed = tP.matTool.speed; }

                    // Only call Feed/speed if non-negative 
                    // so Material Tool can have -1 for speed/feed and ignore them
                    if (feed >= 0) { FChange = true; }
                    if (speed >= 0) { SChange = true; }
                }
                else
                {
                    feed = tP.matTool.feedCut;
                    speed = tP.matTool.speed;
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
                PtCode = GCode.gcTwoAxis(Pt);
                
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

                if (Pt.name != "")
                {
                    PtCode = PtCode + " " + this.comment(Pt.name);
                }
                Co.Append(PtCode);

                // Adjust ranges

                Co.GrowRange("X", Pt.pt.X);
                Co.GrowRange("Y", Pt.pt.Y);
                Co.GrowRange("Z", Pt.pt.Z);

            }

            // return the last point or the beforePoint if the path had no elements
            ToolPoint PtOut;

            if (tP.Count > 0) { PtOut = new ToolPoint(tP[tP.Count - 1]) { feed = feed, speed = speed }; }
            else { PtOut = beforePoint; }

            return PtOut;
        }

        public void writeFileEnd(ref CodeInfo Co, MachineInstruction MI) => GCode.gcInstEnd(this, ref Co, MI);
        public void writeFileStart(ref CodeInfo Co, MachineInstruction MI) => GCode.gcInstStart(this, ref Co, MI);
        public void writeOpEnd(ref CodeInfo Co, MachineOperation MO) => GCode.gcOpEnd(this, ref Co, MO);
        public void writeOpStart(ref CodeInfo Co, MachineOperation MO) => GCode.gcOpStart(this, ref Co, MO);

        public ToolPoint writeTransition(ref CodeInfo Co, ToolPath fP, ToolPath tP, bool first, ToolPoint beforePoint)
        {
            ToolPoint outPoint = beforePoint;
            // check there is anything to transition from
            if ((fP == null || fP.Count == 0) && tP!= null && tP.Count > 0)
            {
                ToolPath startPath = tP.copyWithNewPoints(new List<ToolPoint>());
                startPath.Add(tP[0]);
                startPath[0].feed = 0;
                startPath.name = String.Empty;
                startPath.preCode = String.Empty;
                startPath.postCode = String.Empty;
                outPoint = startPath.writeCode(ref Co, this, beforePoint);
            }
            else if (tP!=null && tP.Count > 0)
            {
                List<Point3d> route = new List<Point3d> { fP[fP.Count - 1].pt, tP[0].pt };

                ToolPath Move = tP.copyWithNewPoints(new List<ToolPoint>());
                Move.name = String.Empty;
                Move.preCode = String.Empty;
                Move.postCode = String.Empty;
                foreach (Point3d Pt in route)
                {
                    // add new point at speed 0 to describe rapid move.
                    Move.Add(new ToolPoint(Pt, new Vector3d(), -1, 0));
                }
                outPoint = Move.writeCode(ref Co, this, beforePoint);
            }
            return outPoint;
        }
    }
}