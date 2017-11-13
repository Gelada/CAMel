using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace CAMel.Types
{
    // List of toolpaths forming a complete set of instructions 
    // for the machine
    public class MachineInstruction : IToolPointContainer
    {
        public List<MachineOperation> MOs;
        public Machine Mach { get; set; }

        // Default Constructor
        public MachineInstruction()
        {
            this.MOs = new List<MachineOperation>();
            this.Mach = new Machine();
            this.name = "";
            this.localCode = "";
        }
        // Just name
        public MachineInstruction(string name)
        {
            this.name = name;
            this.MOs = new List<MachineOperation>();
            this.Mach = new Machine();
            this.localCode = "";
        }
        // Name and Machine
        public MachineInstruction(string name, Machine Ma)
        {
            this.name = name;
            this.Mach = Ma;
            this.MOs = new List<MachineOperation>();
            this.localCode = "";
        }
        // Copy Constructor
        public MachineInstruction(MachineInstruction Op)
        {
            this.name = Op.name;
            this.localCode = Op.localCode;
            this.Mach = Op.Mach;
            this.MOs = new List<MachineOperation>();
            foreach(MachineOperation MO in Op.MOs)
            {
                this.MOs.Add(new MachineOperation(MO));
            }
        }

        // Copy basic information but add new paths
        public MachineInstruction copyWithNewPaths(List<MachineOperation> MOs)
        {
            MachineInstruction outInst = new MachineInstruction();
            outInst.localCode = this.localCode;
            outInst.name = this.name;
            outInst.Mach = this.Mach;
            outInst.MOs = MOs;

            return outInst;

        }

        public string TypeDescription
        {
            get { return "Complete set of operations for a run of the machine."; }
        }

        public string TypeName
        {
            get { return "MachineInstruction"; }
        }

        public string name { get; set; }

        public string localCode { get; set; }

        public bool IsValid
        {
            get
            {
                throw new NotImplementedException("IsValid not implemented for Machine Instructions.");
            }
        }

        public override string ToString()
        {
            int total_TP = 0;
            foreach(MachineOperation MO in this.MOs)
            {
                foreach (ToolPath TP in MO.TPs)
                {
                    total_TP = total_TP + TP.Pts.Count;
                }
            }
            return "Machine Instruction: " + this.name + ", " + this.MOs.Count + " operations, " + total_TP + " total Instructions.";
        }

        // Main functions

        public MachineInstruction ProcessAdditions()
        {
            List<MachineOperation> procOps = new List<MachineOperation>();

            foreach(MachineOperation MO in this.MOs)
            {
                procOps.Add(MO.ProcessAdditions(this.Mach));
            }

            return this.copyWithNewPaths(procOps);
        }

        public void WriteCode(ref CodeInfo Co)
        {
            DateTime thisDay = DateTime.Now;
            Co.AppendLineNoNum(this.Mach.filestart);
            Co.AppendLine(this.Mach.SectionBreak);
            if (this.name != "") Co.AppendLine(this.Mach.CommentChar + " " + this.name + this.Mach.endCommentChar);
            Co.AppendComment("");
            Co.AppendComment(" Machine Instructions Created " + thisDay.ToString("f"));
            Co.AppendComment("  by " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " "
                + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            if (this.Mach.name != "") Co.AppendComment("  for " + this.Mach.name);
            Co.AppendComment(" Starting with: ");
            Co.AppendComment("  Tool: "+this.MOs[0].TPs[0].MatTool.Tool_name);
            Co.AppendComment("  in " + this.MOs[0].TPs[0].MatTool.Mat_name+ " with shape " + this.MOs[0].TPs[0].MatForm.ToString());
            Co.AppendComment("");
            Co.AppendLine(this.Mach.SectionBreak);
            Co.Append(this.Mach.header);
            Co.Append(this.localCode);

            // Let the Code writer know the Material Tool and Form so can report changes
            Co.currentMT = this.MOs[0].TPs[0].MatTool;
            Co.currentMF = this.MOs[0].TPs[0].MatForm;

            ToolPath startPath = null;
            ToolPath endPath;

            foreach(MachineOperation MO in this.MOs)
            {
                MO.WriteCode(ref Co, Mach, out endPath, startPath);
                startPath = endPath;
            }


            Co.AppendLine(this.Mach.SectionBreak);
            Co.AppendComment(" End of ToolPaths");
            Co.AppendLine(this.Mach.SectionBreak);

            Co.Append(this.Mach.footer);
            Co.AppendLineNoNum(this.Mach.fileend);
        }

        ICAMel_Base ICAMel_Base.Duplicate()
        {
            return new MachineInstruction(this);
        }
    }

    // Grasshopper Type Wrapper
    public class GH_MachineInstruction : CAMel_Goo<MachineInstruction>
    {
        // Default Constructor with XY plane with safe distance 1;
        public GH_MachineInstruction()
        {
            this.Value = new MachineInstruction();
        }
       
        // Just name
        public GH_MachineInstruction(string name)
        {
            this.Value = new MachineInstruction(name);
        }
        // Name and Machine
        public GH_MachineInstruction(string name, Machine Ma)
        {
            this.Value = new MachineInstruction(name, Ma);
        }
        // Copy Constructor.
        public GH_MachineInstruction(GH_MachineInstruction Op)
        {
            this.Value = new MachineInstruction(Op.Value);
        }
        // Duplicate
        public override IGH_Goo Duplicate()
        {
            return new GH_MachineInstruction(this);
        }
 
        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(MachineInstruction)))
            {
                object ptr = this.Value;
                target = (Q)ptr;
                return true;
            }

            return false;
        }

        public override bool CastFrom(object source)
        {
            if (source == null)
            {
                return false;
            }
            if (source is MachineInstruction)
            {
                this.Value = new MachineInstruction((MachineInstruction)source);
                return true;
            }
            return false;
        }
    }

    // Grasshopper Parameter Wrapper
    public class GH_MachineInstructionPar : GH_Param<GH_MachineInstruction>
    {
        public GH_MachineInstructionPar() :
            base("Instructions", "MachInst", "Contains a collection of Machine Instructions", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid
        {
            get { return new Guid("7ded80e7-6a29-4534-a848-f9d1b897098f"); }
        }
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Properties.Resources.machineinstructions;
            }
        }
    }

}