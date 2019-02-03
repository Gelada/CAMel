using System;
using System.Collections;
using System.Collections.Generic;

using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

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
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
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
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Copy Constructor
        private MachineInstruction(MachineInstruction Op)
        {
            this.name = string.Copy(Op.name);
            this.preCode = string.Copy(Op.preCode);
            this.postCode = string.Copy(Op.postCode);
            this.mach = Op.mach;
            this.startPath = Op.startPath.deepClone();
            this.endPath = Op.endPath.deepClone();
            this.MOs = new List<MachineOperation>();
            foreach(MachineOperation MO in Op.MOs) { this.MOs.Add(MO.deepClone()); }
        }

        public MachineInstruction deepClone() => new MachineInstruction(this);

        // Copy basic information but add new paths
        public MachineInstruction deepCloneWithNewPaths(List<MachineOperation> MOs)
        {
            MachineInstruction outInst = new MachineInstruction
            {
                name = string.Copy(this.name),
                preCode = string.Copy(this.preCode),
                postCode = string.Copy(this.postCode),
                mach = this.mach,
                startPath = this.startPath.deepClone(),
                endPath = this.endPath.deepClone()
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

        public override string ToString()
        {
            int total_TP = 0;
            foreach(MachineOperation MO in this) {  foreach (ToolPath TP in MO) { total_TP += TP.Count; } }
            return "Machine Instruction: " + this.name + ", " + this.Count + " operations, " + total_TP + " total points.";
        }

        // Main functions

        // Take the collection of paths and hints and validate it into 
        // a workable system, including adding MaterialTool and MaterialForm information
        // throughout
        public MachineInstruction processAdditions(IMachine M)
        {
            MachineInstruction valid = this.deepCloneWithNewPaths(new List<MachineOperation>());

            // Mix this.startPath and this.validStart as required to 
            // give a valid startPath.
            ToolPath validTP = this.validStart();
            valid.startPath = this.startPath ?? validTP;
            valid.startPath.validate(validTP, M);
            validTP = valid.startPath;

            // process and validate all Operations
            foreach (MachineOperation MO in this)
            { valid.Add(MO.processAdditions(this.mach, ref validTP)); }

            // If the startpath has no points add the first point of the processed points
            if (valid.startPath.Count == 0)
            {
                valid.startPath.Add(valid.firstP.deepClone());
                valid.startPath.firstP.feed = 0;
            }

            // validate endPath, validTP will have the most recent information
            if (valid.endPath == null) { valid.endPath = validTP.deepCloneWithNewPoints(new List<ToolPoint>()); }
            valid.endPath.validate(validTP, M);
            // if we need a point add the last point of the processed paths.
            if (valid.endPath.Count == 0)
            {
                valid.endPath.Add(valid.lastP.deepClone());
                valid.endPath.firstP.feed = 0;
            }

            return valid;
        }

        public void writeCode(ref CodeInfo Co)
        {
            ToolPath uStartPath = this.startPath;
            ToolPath endPath = new ToolPath();

            this.mach.writeFileStart(ref Co, this, uStartPath);

            foreach (MachineOperation MO in this)
            {
                MO.writeCode(ref Co, this.mach, out endPath, uStartPath);
                uStartPath = endPath;
            }

            this.mach.writeFileEnd(ref Co, this, uStartPath, this.endPath);
        }


        // Hunt through the ToolPaths until we find all we need
        private ToolPath validStart()
        {
            // we need a MaterialTool and a MaterialForm
            bool mTFound = false;
            bool mFFound = false;

            ToolPath valid = new ToolPath();
            // scan through the paths looking for info
            foreach (MachineOperation mO in this)
            {
                foreach (ToolPath tP in mO)
                {
                    if (!mTFound && tP.matTool != null) { mTFound = true; valid.matTool = tP.matTool; }
                    if (!mFFound && tP.matForm != null) { mFFound = true; valid.matForm = tP.matForm; }
                    if (mTFound && mFFound) { return valid; }
                }
            }
            // if the machine has one tool use that.
            if (this.mach.mTs.Count == 1 && mFFound) { valid.matTool = this.mach.mTs[0]; return valid; }
            // if we go through the whole thing without finding all the valid pieces
            throw new InvalidOperationException("Cannot validate Machine Instructions, there are either no ToolPaths with a MaterialTool or no ToolPaths with a MaterialForm.");
        }

        #region Point extraction and previews
        public ToolPath getSinglePath()
        {
            ToolPath oP = this[0].getSinglePath();
            for (int i = 1; i < this.Count; i++) { oP.AddRange(this[i].getSinglePath()); }
            return oP;
        }
        // Get the list of tooltip locations
        public List<List<List<Point3d>>> getPoints()
        {
            List<List<List<Point3d>>> Pts = new List<List<List<Point3d>>>();
            foreach (MachineOperation MO in this) { Pts.Add(MO.getPoints()); }
            return Pts;
        }
        // Get the list of tool directions
        public List<List<List<Vector3d>>> getDirs()
        {
            List<List<List<Vector3d>>> Dirs = new List<List<List<Vector3d>>>();
            foreach (MachineOperation MO in this) { Dirs.Add(MO.getDirs()); }
            return Dirs;
        }
        // Create a path with the points 
        public List<List<List<Point3d>>> getPointsandDirs(out List<List<List<Vector3d>>> Dirs)
        {
            Dirs = getDirs();
            return getPoints();
        }

        // Bounding Box for previews
        public BoundingBox getBoundingBox()
        {
            BoundingBox BB = BoundingBox.Unset;
            for (int i = 0; i < this.Count; i++)
            { BB.Union(this[i].getBoundingBox()); }
            return BB;
        }
        // Create single polyline
        public PolylineCurve getLine() => this.getSinglePath().getLine();
        // Create polylines
        public List<PolylineCurve> getLines()
        {
            List<PolylineCurve> lines = new List<PolylineCurve>();
            foreach (MachineOperation MO in this) { lines.AddRange(MO.getLines()); }
            return lines;
        }
        // Lines for each toolpoint
        public List<Line> toolLines()
        {
            List<Line> lines = new List<Line>();
            foreach (MachineOperation MO in this) { lines.AddRange(MO.toolLines()); }
            return lines;
        }
        #endregion

        #region List Functions 
        public int Count => ((IList<MachineOperation>)this.MOs).Count;
        public bool IsReadOnly => ((IList<MachineOperation>)this.MOs).IsReadOnly;
        public MachineOperation this[int index] { get => ((IList<MachineOperation>)this.MOs)[index]; set => ((IList<MachineOperation>)this.MOs)[index] = value; }
        public int IndexOf(MachineOperation item) { return ((IList<MachineOperation>)this.MOs).IndexOf(item); }
        public void Insert(int index, MachineOperation item) { ((IList<MachineOperation>)this.MOs).Insert(index, item); }
        public void RemoveAt(int index) { ((IList<MachineOperation>)this.MOs).RemoveAt(index); }
        public void Add(MachineOperation item) { ((IList<MachineOperation>)this.MOs).Add(item); }
        public void AddRange(IEnumerable<MachineOperation> items) { this.MOs.AddRange(items); }
        public void Clear() { ((IList<MachineOperation>)this.MOs).Clear(); }
        public bool Contains(MachineOperation item) { return ((IList<MachineOperation>)this.MOs).Contains(item); }
        public void CopyTo(MachineOperation[] array, int arrayIndex) { ((IList<MachineOperation>)this.MOs).CopyTo(array, arrayIndex); }
        public bool Remove(MachineOperation item) { return ((IList<MachineOperation>)this.MOs).Remove(item); }
        public IEnumerator<MachineOperation> GetEnumerator() { return ((IList<MachineOperation>)this.MOs).GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return ((IList<MachineOperation>)this.MOs).GetEnumerator(); }
        #endregion
    }

    // Grasshopper Type Wrapper
    public class GH_MachineInstruction : CAMel_Goo<MachineInstruction>, IGH_PreviewData
    {
        public BoundingBox ClippingBox => this.Value.getBoundingBox();

        // Default Constructor;
        public GH_MachineInstruction() { this.Value = new MachineInstruction(); }
        // Construct from value alone
        public GH_MachineInstruction(MachineInstruction MI) { this.Value = MI; }
        // Copy Constructor.
        public GH_MachineInstruction(GH_MachineInstruction Op) { this.Value = Op.Value.deepClone(); }
        // Duplicate
        public override IGH_Goo Duplicate() { return new GH_MachineInstruction(this); }
 
        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(MachineInstruction)))
            {
                object ptr = this.Value;
                target = (Q)ptr;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(ToolPath)))
            {
                object ptr = this.Value.getSinglePath();
                target = (Q)ptr;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_ToolPath)))
            {
                object ptr = new GH_ToolPath(this.Value.getSinglePath());
                target = (Q)ptr;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(Curve)))
            {
                target = (Q)(object)this.Value.getLine();
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_Curve)))
            {
                target = (Q)(object)new GH_Curve(this.Value.getLine());
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(IMachine)))
            {
                object ptr = this.Value.mach;
                target = (Q)ptr;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_Machine)))
            {
                object ptr = new GH_Machine(this.Value.mach);
                target = (Q)ptr;
                return true;
            }

            return false;
        }
        public override bool CastFrom(object source)
        {
            if (source == null) { return false; }
            // Cast from unwrapped MachineInstruction
            if (typeof(MachineInstruction).IsAssignableFrom(source.GetType()))
            {
                this.Value = (MachineInstruction)source;
                return true;
            }
            return false;
        }

        public void DrawViewportWires(GH_PreviewWireArgs args)
        {
            foreach (PolylineCurve L in this.Value.getLines())
            {
                args.Pipeline.DrawCurve(L, args.Color);
            }
            args.Pipeline.DrawArrows(this.Value.toolLines(), args.Color);
        }
        public void DrawViewportMeshes(GH_PreviewMeshArgs args) { }

    }

    // Grasshopper Parameter Wrapper
    public class GH_MachineInstructionPar : GH_Param<GH_MachineInstruction>, IGH_PreviewObject
    {
        public GH_MachineInstructionPar() :
            base("Instructions", "MachInst", "Contains a collection of Machine Instructions", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid
        {
            get { return new Guid("7ded80e7-6a29-4534-a848-f9d1b897098f"); }
        }

        public bool Hidden { get; set; }
        public bool IsPreviewCapable => true;
        public BoundingBox ClippingBox => base.Preview_ComputeClippingBox();
        public void DrawViewportWires(IGH_PreviewArgs args) => base.Preview_DrawWires(args);
        public void DrawViewportMeshes(IGH_PreviewArgs args) => base.Preview_DrawMeshes(args);

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