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
    public class MachineInstruction : IList<MachineOperation>, IToolPointContainer
    {
        private List<MachineOperation> MOs;
        public ToolPath startPath { get; set; }
        public ToolPath endPath { get; set; }

        public string name { get; set; }
        public string preCode { get; set; }
        public string postCode { get; set; }

        public IMachine mach { get; set; }

        public ToolPoint firstP
        {
            get
            {
                ToolPoint oP = null;
                // Cycle through to find a path of length greater than 1.
                for (int i = 0; i < this.Count; i++)
                {
                    oP = this[i].firstP;
                    if(oP != null) { break; }
                }
                return oP;
            }
        }
        public ToolPoint lastP
        {
            get
            {
                ToolPoint oP = null;
                // Cycle through to find a path of length greater than 1.
                for (int i = this.Count - 1; i >= 0; i--)
                {
                    oP = this[i].lastP;
                    if (oP != null) { break; }
                }
                return oP;
            }
        }

        // Default Constructor
        public MachineInstruction()
        {
            this.MOs = new List<MachineOperation>();
            this.startPath = new ToolPath();
            this.endPath = new ToolPath();
            this.name = "";
            this.preCode = "";
            this.postCode = "";
        }

        // Everything but the pre and post codes
        public MachineInstruction(string name, IMachine mach, List<MachineOperation> MOs, ToolPath startPath = null, ToolPath endPath = null)
        {
            this.name = name;
            this.mach = mach;
            this.MOs = MOs;
            if (startPath == null) { this.startPath = new ToolPath(); }
            else { this.startPath = startPath; }
            if (endPath == null) { this.endPath = new ToolPath(); }
            else { this.endPath = endPath; }
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
            this.startPath = new ToolPath(Op.startPath);
            this.endPath = new ToolPath(Op.endPath);
            this.MOs = new List<MachineOperation>();
            foreach(MachineOperation MO in Op.MOs)
            {
                this.MOs.Add(new MachineOperation(MO));
            }
        }

        MachineInstruction Duplicate() => new MachineInstruction(this);

        // Copy basic information but add new paths
        public MachineInstruction copyWithNewPaths(List<MachineOperation> MOs)
        {
            MachineInstruction outInst = new MachineInstruction
            {
                preCode = this.preCode,
                postCode = this.postCode,
                name = this.name,
                mach = this.mach,
                startPath = this.startPath,
                endPath = this.endPath
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
                foreach (ToolPath TP in MO) { total_TP = total_TP + TP.Count; }
            }
            return "Machine Instruction: " + this.name + ", " + this.Count + " operations, " + total_TP + " total Instructions.";
        }

        // Main functions

        public MachineInstruction processAdditions()
        {
            List<MachineOperation> procOps = new List<MachineOperation>();

            foreach(MachineOperation MO in this) { procOps.Add(MO.processAdditions(this.mach)); }

            return this.copyWithNewPaths(procOps);
        }

        public void writeCode(ref CodeInfo Co)
        {
            // Use startPath or first point to create a preliminary position, to transition from
            ToolPath uStartPath;
            if(this.startPath == null)
            {
                uStartPath = this[0][0].copyWithNewPoints(new List<ToolPoint> { this.firstP });
                uStartPath.firstP.feed = 0;
            }
            else if (this.startPath.Count == 0)
            {
                uStartPath = this.startPath.copyWithNewPoints(new List<ToolPoint> { this.firstP });
                uStartPath.firstP.feed = 0;
            }
            else { uStartPath = this.startPath; }

            if (uStartPath.matForm == null) { uStartPath.matForm = this[0][0].matForm; }
            if (uStartPath.matTool == null) { uStartPath.matTool = this[0][0].matTool; }

            ToolPath endPath = new ToolPath();

            this.mach.writeFileStart(ref Co, this, uStartPath);

            foreach (MachineOperation MO in this)
            {
                MO.writeCode(ref Co, this.mach, out endPath, uStartPath);
                uStartPath = endPath;
            }

            // Use endP or last point to create an end position, to transition to

            if (this.endPath == null)
            {
                endPath = endPath.copyWithNewPoints(new List<ToolPoint> { this.lastP });
            }
            else { endPath = this.endPath; }

            this.mach.writeFileEnd(ref Co, this, uStartPath, endPath);
        }

        public ToolPath getSinglePath()
        {
            ToolPath oP = this[0].getSinglePath();
            for (int i = 1; i < this.Count; i++) { oP.AddRange(this[i].getSinglePath()); }
            return oP;
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
        // Default Constructor;
        public GH_MachineInstruction()
        {
            this.Value = new MachineInstruction();
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