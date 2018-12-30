using System;
using System.Collections;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types.Machine;

namespace CAMel.Types
{
    // List of toolpaths forming a complete set of instructions 
    // for the machine
    public class MachineInstruction : IList<MachineOperation>,IToolPointContainer
    {
        private List<MachineOperation> MOs;

        public IMachine mach { get; set; }

        // Default Constructor
        public MachineInstruction()
        {
            this.MOs = new List<MachineOperation>();
            this.name = "";
            this.preCode = "";
            this.postCode = "";
        }
        // Just name
        public MachineInstruction(string name)
        {
            this.name = name;
            this.MOs = new List<MachineOperation>();
            this.preCode = "";
            this.postCode = "";
        }
        // Name and Machine
        public MachineInstruction(string name, IMachine mach)
        {
            this.name = name;
            this.mach = mach;
            this.MOs = new List<MachineOperation>();
            this.preCode = "";
            this.postCode = "";
        }
        // Name, Machine and Operations
        public MachineInstruction(string name, IMachine mach, List<MachineOperation> MOs)
        {
            this.name = name;
            this.mach = mach;
            this.MOs = MOs;
            this.preCode = "";
            this.postCode = "";
        }
        // Copy Constructor
        public MachineInstruction(MachineInstruction Op)
        {
            this.name = Op.name;
            this.preCode = Op.preCode;
            this.postCode = Op.postCode;
            this.mach = Op.mach;
            this.MOs = new List<MachineOperation>();
            foreach(MachineOperation MO in Op.MOs)
            {
                this.MOs.Add(new MachineOperation(MO));
            }
        }



        // Copy basic information but add new paths
        public MachineInstruction copyWithNewPaths(List<MachineOperation> MOs)
        {
            MachineInstruction outInst = new MachineInstruction
            {
                preCode = this.preCode,
                postCode = this.postCode,
                name = this.name,
                mach = this.mach,
            };
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

        public string preCode { get; set; }
        public string postCode { get; set; }

        public bool IsValid
        {
            get
            {
                throw new NotImplementedException("IsValid not implemented for Machine Instructions.");
            }
        }

        public int Count => ((IList<MachineOperation>)this.MOs).Count;

        public bool IsReadOnly => ((IList<MachineOperation>)this.MOs).IsReadOnly;

        public MachineOperation this[int index] { get => ((IList<MachineOperation>)this.MOs)[index]; set => ((IList<MachineOperation>)this.MOs)[index] = value; }

        public override string ToString()
        {
            int total_TP = 0;
            foreach(MachineOperation MO in this)
            {
                foreach (ToolPath TP in MO)
                {
                    total_TP = total_TP + TP.Count;
                }
            }
            return "Machine Instruction: " + this.name + ", " + this.Count + " operations, " + total_TP + " total Instructions.";
        }

        // Main functions

        public MachineInstruction processAdditions()
        {
            List<MachineOperation> procOps = new List<MachineOperation>();

            foreach(MachineOperation MO in this)
            {
                procOps.Add(MO.processAdditions(this.mach));
            }

            return this.copyWithNewPaths(procOps);
        }

        public void writeCode(ref CodeInfo Co)
        {
            this.mach.writeFileStart(ref Co, this);

            // Let the Code writer know the Material Tool and Form so can report changes
            // for consistency this might also do speed and feed, 
            // at the moment that is handled by passing a toolPoint around.

            Co.currentMT = this[0][0].matTool;
            Co.currentMF = this[0][0].matForm;

            ToolPath startPath = null;
            ToolPath endPath;

            foreach(MachineOperation MO in this)
            {
                MO.writeCode(ref Co, this.mach, out endPath, startPath);
                startPath = endPath;
            }

            this.mach.writeFileEnd(ref Co, this);
        }

        ICAMel_Base ICAMel_Base.Duplicate()
        {
            return new MachineInstruction(this);
        }

        public int IndexOf(MachineOperation item)
        {
            return ((IList<MachineOperation>)this.MOs).IndexOf(item);
        }

        public void Insert(int index, MachineOperation item)
        {
            ((IList<MachineOperation>)this.MOs).Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            ((IList<MachineOperation>)this.MOs).RemoveAt(index);
        }

        public void Add(MachineOperation item)
        {
            ((IList<MachineOperation>)this.MOs).Add(item);
        }

        public void Clear()
        {
            ((IList<MachineOperation>)this.MOs).Clear();
        }

        public bool Contains(MachineOperation item)
        {
            return ((IList<MachineOperation>)this.MOs).Contains(item);
        }

        public void CopyTo(MachineOperation[] array, int arrayIndex)
        {
            ((IList<MachineOperation>)this.MOs).CopyTo(array, arrayIndex);
        }

        public bool Remove(MachineOperation item)
        {
            return ((IList<MachineOperation>)this.MOs).Remove(item);
        }

        public IEnumerator<MachineOperation> GetEnumerator()
        {
            return ((IList<MachineOperation>)this.MOs).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IList<MachineOperation>)this.MOs).GetEnumerator();
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
        public GH_MachineInstruction(string name, IMachine Ma)
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