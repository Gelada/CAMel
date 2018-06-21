using System;
using System.Collections.Generic;
using System.Text;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
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
        { get { return "Instructions for a 3-Axis machine"; } }
        public string TypeName
        { get { return "CAMelThreeAxis"; } }
        public string comment(string L)
        {
            throw new NotImplementedException();
        }

        public ICAMel_Base Duplicate()
        {
            return new ThreeAxis(this);
        }

        public ToolPath insertRetract(ToolPath tP)
        {
            return tP.MatForm.InsertRetract(tP);
        }
        public ToolPoint Interpolate(ToolPoint toolPoint1, ToolPoint toolPoint2, double par)
        {
            ToolPoint TPo = new ToolPoint(toolPoint1);
            TPo.Pt = toolPoint1.Pt * par + toolPoint2.Pt * (1 - par);
            return TPo;
        }

        public ToolPath ReadCode(string Code)
        {
            throw new NotImplementedException();
        }

        public Vector3d toolDir(ToolPoint TP)
        {
            return Vector3d.ZAxis;
        }

        public ToolPoint writeCode(ref CodeInfo Co, ToolPath tP, ToolPoint beforePoint)
        {
            throw new NotImplementedException();
        }

        public void writeFileEnd(ref CodeInfo Co, MachineInstruction MI)
        {
            GCode.GcInstEnd(this, ref Co, MI);
        }
        public void writeFileStart(ref CodeInfo Co, MachineInstruction MI)
        {
            GCode.GcInstStart(this, ref Co, MI);
        }
        public void writeOpEnd(ref CodeInfo Co, MachineOperation MO)
        {
            GCode.GcOpEnd(this, ref Co, MO);
        }
        public void writeOpStart(ref CodeInfo Co, MachineOperation MO)
        {
            GCode.GcOpStart(this, ref Co, MO);
        }

        public ToolPoint writeTransition(ref CodeInfo Co, ToolPath fP, ToolPath tP, bool first, ToolPoint beforePoint)
        {
            throw new NotImplementedException();
        }
    }
}