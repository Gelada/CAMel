using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CAMel.Types.Machine;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.Types
{
    // List of toolpaths forming a complete set of instructions
    // for the machine
    public class MachineInstruction : IList<MachineOperation>, IToolPointContainer
    {
        [ItemNotNull] [NotNull] private List<MachineOperation> _mOs;
        [PublicAPI] [NotNull] public ToolPath startPath { get; set; }
        [PublicAPI] [NotNull] public ToolPath endPath { get; set; }

        public string name { get; set; }
        public string preCode { get; set; }
        public string postCode { get; set; }

        [PublicAPI] [NotNull] public IMachine mach { get; set; }

        public ToolPoint firstP => this.First(a => a?.firstP != null)?.firstP;
        public ToolPoint lastP => this.Last(a => a?.lastP != null)?.lastP;

        // Default Constructor
        public MachineInstruction([NotNull] IMachine m)
        {
            this.mach = m;
            this._mOs = new List<MachineOperation>();
            this.startPath = new ToolPath();
            this.endPath = new ToolPath();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }

        // Everything but the pre and post codes
        public MachineInstruction([NotNull] string name, [NotNull] IMachine mach, [CanBeNull] List<MachineOperation> mOs, [CanBeNull] ToolPath startPath = null, [CanBeNull] ToolPath endPath = null)
        {
            this.name = name;
            this.mach = mach;
            this._mOs = mOs ?? new List<MachineOperation>();
            this.startPath = startPath ?? new ToolPath();
            this.endPath = endPath ?? new ToolPath();
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Copy Constructor
        private MachineInstruction([NotNull] MachineInstruction mI)
        {
            this.name = string.Copy(mI.name);
            this.preCode = string.Copy(mI.preCode);
            this.postCode = string.Copy(mI.postCode);
            this.mach = mI.mach;
            this.startPath = mI.startPath.deepClone();
            this.endPath = mI.endPath.deepClone();
            this._mOs = new List<MachineOperation>();
            foreach(MachineOperation mO in mI) { this._mOs.Add(mO?.deepClone()); }
        }

        [NotNull] public MachineInstruction deepClone() => new MachineInstruction(this);

        // Copy basic information but add new paths
        [NotNull]
        [PublicAPI]
        public MachineInstruction deepCloneWithNewPaths([NotNull] List<MachineOperation> mOs)
        {
            MachineInstruction outInst = new MachineInstruction(this.mach)
            {
                name = string.Copy(this.name),
                preCode = string.Copy(this.preCode),
                postCode = string.Copy(this.postCode),
                mach = this.mach,
                startPath = this.startPath.deepClone(),
                endPath = this.endPath.deepClone(),
                _mOs = mOs
            };
            return outInst;
        }

        public string TypeDescription => "Complete set of operations for a run of the machine.";

        public string TypeName => "MachineInstruction";

        public override string ToString()
        {
            int totalTP = 0;
            foreach(MachineOperation mO in this) {  foreach (ToolPath tP in mO) { totalTP += tP.Count; } }
            return "Machine Instruction: " + this.name + ", " + this.Count + " operations, " + totalTP + " total points.";
        }

        // Main functions

        // Take the collection of paths and hints and validate it into
        // a workable system, including adding MaterialTool and MaterialForm information
        // throughout
        [NotNull]
        public MachineInstruction processAdditions()
        {
            MachineInstruction valid = deepCloneWithNewPaths(new List<MachineOperation>());

            // Mix this.startPath and this.validStart as required to
            // give a valid startPath.
            ToolPath validTP = validStart();
            valid.startPath.validate(validTP, this.mach);
            validTP = valid.startPath;

            // process and validate all Operations
            foreach (MachineOperation mO in this) { valid.Add(mO.processAdditions(this.mach, ref validTP)); }

            valid.startPath.additions = new ToolPathAdditions();

            // If the start path has no points add the first point of the processed points
            if (valid.startPath.Count == 0)
            {
                valid.startPath.Add(valid.firstP?.deepClone());
                if(valid.startPath.firstP != null) { valid.startPath.firstP.feed = 0; }
            }

            // validate endPath, validTP will have the most recent information
             valid.endPath.validate(validTP, this.mach);
             valid.endPath.additions = new ToolPathAdditions();
            // if we need a point add the last point of the processed paths.
            if (valid.endPath.Count != 0) { return valid; }

            valid.endPath.Add(valid.lastP?.deepClone());
            if(valid.endPath.firstP != null) { valid.endPath.firstP.feed = 0; }

            return valid;
        }

        [NotNull]
        public CodeInfo writeCode()
        {
            ToolPath uStartPath = this.startPath;
            if (uStartPath.matForm == null) { Exceptions.matFormException(); }
            if (uStartPath.matTool == null) { Exceptions.matToolException(); }

            CodeInfo co = new CodeInfo(this.mach,uStartPath.matForm,uStartPath.matTool);

            this.mach.writeFileStart(ref co, this, uStartPath);

            foreach (MachineOperation mO in this)
            {
                mO.writeCode(ref co, this.mach, out ToolPath uEndPath, uStartPath);
                uStartPath = uEndPath;
            }

            this.mach.writeFileEnd(ref co, this, uStartPath, this.endPath);

            return co;
        }


        // Hunt through the ToolPaths until we find all we need
        [NotNull]
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
            if (this.mach.mTs.Count != 1 || !mFFound)
            {
                throw new InvalidOperationException(
                    "Cannot validate Machine Instructions, there are either no ToolPaths with a MaterialTool or no ToolPaths with a MaterialForm.");
            }
            valid.matTool = this.mach.mTs[0]; return valid;
            // if we go through the whole thing without finding all the valid pieces
        }

        #region Point extraction and previews
        public ToolPath getSinglePath()
        {
            ToolPath oP = this[0].getSinglePath();
            for (int i = 1; i < this.Count; i++) { oP.AddRange(this[i].getSinglePath()); }
            return oP;
        }
        // Get the list of tooltip locations
        [NotNull, PublicAPI]
        public List<List<List<Point3d>>> getPoints()
        {
            List<List<List<Point3d>>> pts = new List<List<List<Point3d>>>();
            foreach (MachineOperation mO in this) { pts.Add(mO.getPoints()); }
            return pts;
        }
        // Get the list of tool directions
        [NotNull, PublicAPI]
        public List<List<List<Vector3d>>> getDirs()
        {
            List<List<List<Vector3d>>> dirs = new List<List<List<Vector3d>>>();
            foreach (MachineOperation mO in this) { dirs.Add(mO.getDirs()); }
            return dirs;
        }
        // Create a path with the points
        [NotNull]
        [PublicAPI]
        public List<List<List<Point3d>>> getPointsAndDirs([NotNull] out List<List<List<Vector3d>>> dirs)
        {
            dirs = getDirs();
            return getPoints();
        }

        // Bounding Box for previews
        public BoundingBox getBoundingBox()
        {
            BoundingBox bb = BoundingBox.Unset;
            for (int i = 0; i < this.Count; i++)
            { bb.Union(this[i].getBoundingBox()); }
            return bb;
        }
        // Create single polyline
        [NotNull] public PolylineCurve getLine() => getSinglePath().getLine();
        // Create polylines
        [NotNull]
        public IEnumerable<PolylineCurve> getLines()
        {
            List<PolylineCurve> lines = new List<PolylineCurve>();
            foreach (MachineOperation mO in this) { lines.AddRange(mO.getLines()); }
            return lines;
        }
        // Lines for each toolpoint
        [NotNull]
        public IEnumerable<Line> toolLines()
        {
            List<Line> lines = new List<Line>();
            foreach (MachineOperation mO in this) { lines.AddRange(mO.toolLines()); }
            return lines;
        }
        #endregion

        #region List Functions 
        public int Count => this._mOs.Count;
        public bool IsReadOnly => ((IList<MachineOperation>)this._mOs).IsReadOnly;
        [NotNull] public MachineOperation this[int index] { get => this._mOs[index]; set => this._mOs[index] = value; }
        public int IndexOf(MachineOperation item) => this._mOs.IndexOf(item);
        public void Insert(int index, MachineOperation item) { if (item != null) { this._mOs.Insert(index, item); } }
        public void RemoveAt(int index) => this._mOs.RemoveAt(index);
        public void Add(MachineOperation item) { if (item != null) { this._mOs.Add(item); } }
        [PublicAPI] public void AddRange([NotNull] IEnumerable<MachineOperation> items) => this._mOs.AddRange(items.Where(x => x != null));
        public void Clear() => this._mOs.Clear();
        public bool Contains(MachineOperation item) => this._mOs.Contains(item);
        public void CopyTo(MachineOperation[] array, int arrayIndex) => this._mOs.CopyTo(array, arrayIndex);
        public bool Remove(MachineOperation item) => this._mOs.Remove(item);
        public IEnumerator<MachineOperation> GetEnumerator() => this._mOs.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this._mOs.GetEnumerator();

        #endregion
    }

    // Grasshopper Type Wrapper
    public sealed class GH_MachineInstruction : CAMel_Goo<MachineInstruction>, IGH_PreviewData
    {
        // Default Constructor;
        [UsedImplicitly] public GH_MachineInstruction() { this.Value = null; }
        // Construct from value alone
        public GH_MachineInstruction([CanBeNull] MachineInstruction mI) { this.Value = mI; }
        // Copy Constructor.
        public GH_MachineInstruction([CanBeNull] GH_MachineInstruction mI) { this.Value = mI?.Value?.deepClone(); }
        // Duplicate
        [NotNull] public override IGH_Goo Duplicate() => new GH_MachineInstruction(this);

        public override bool CastTo<T>(ref T target)
        {
            if (this.Value == null) { return false; }
            if (typeof(T).IsAssignableFrom(typeof(MachineInstruction)))
            {
                object ptr = this.Value;
                target = (T)ptr;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(ToolPath)))
            {
                object ptr = this.Value.getSinglePath();
                target = (T)ptr;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_ToolPath)))
            {
                object ptr = new GH_ToolPath(this.Value.getSinglePath());
                target = (T)ptr;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(Curve)))
            {
                target = (T)(object)this.Value.getLine();
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_Curve)))
            {
                target = (T)(object)new GH_Curve(this.Value.getLine());
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(IMachine)))
            {
                object ptr = this.Value.mach;
                target = (T)ptr;
                return true;
            }
            // ReSharper disable once InvertIf
            if (typeof(T).IsAssignableFrom(typeof(GH_Machine)))
            {
                object ptr = new GH_Machine(this.Value.mach);
                target = (T)ptr;
                return true;
            }

            return false;
        }
        public override bool CastFrom([CanBeNull] object source)
        {
            switch (source) {
                case null: return false;
                // Cast from unwrapped MachineInstruction
                case MachineInstruction mI:
                    this.Value = mI;
                    return true;
                default: return false;
            }
        }

        public BoundingBox ClippingBox => this.Value?.getBoundingBox() ?? BoundingBox.Unset;

        public void DrawViewportWires([CanBeNull] GH_PreviewWireArgs args)
        {
            if (this.Value == null || args?.Pipeline == null) { return; }
            foreach (PolylineCurve l in this.Value.getLines())
            {
                args.Pipeline.DrawCurve(l, args.Color);
            }
            args.Pipeline.DrawArrows(this.Value.toolLines(), args.Color);
        }
        public void DrawViewportMeshes([CanBeNull] GH_PreviewMeshArgs args) { }

    }

    // Grasshopper Parameter Wrapper
    public class GH_MachineInstructionPar : GH_Param<GH_MachineInstruction>, IGH_PreviewObject
    {
        public GH_MachineInstructionPar() :
            base("Instructions", "MachInst", "Contains a collection of Machine Instructions", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid => new Guid("7ded80e7-6a29-4534-a848-f9d1b897098f");

        public bool Hidden { get; set; }
        public bool IsPreviewCapable => true;
        public BoundingBox ClippingBox => Preview_ComputeClippingBox();
        public void DrawViewportWires([CanBeNull] IGH_PreviewArgs args) => Preview_DrawWires(args);
        public void DrawViewportMeshes([CanBeNull] IGH_PreviewArgs args) => Preview_DrawMeshes(args);

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.machineinstructions;
    }

}