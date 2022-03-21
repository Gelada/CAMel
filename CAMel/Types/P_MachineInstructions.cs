namespace CAMel.Types
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using CAMel.Types.Machine;

    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Types;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    // List of toolpaths forming a complete set of instructions
    // for the machine
    /// <summary>TODO The machine instruction.</summary>
    public class MachineInstruction : IList<MachineOperation>, IToolPointContainer
    {
        /// <summary>TODO The m os.</summary>
        [ItemNotNull, NotNull] private List<MachineOperation> mOs;
        /// <summary>Gets or sets the start path.</summary>
        [PublicAPI, NotNull]
        public ToolPath startPath { get; set; }
        /// <summary>Gets or sets the end path.</summary>
        [PublicAPI, NotNull]
        public ToolPath endPath { get; set; }

        /// <inheritdoc />
        public string name { get; set; }
        /// <inheritdoc />
        public string preCode { get; set; }
        /// <inheritdoc />
        public string postCode { get; set; }

        /// <summary>Gets or sets the m.</summary>
        [PublicAPI, NotNull]
        public IMachine m { get; set; }

        /// <inheritdoc />
        public ToolPoint firstP => this.FirstOrDefault(a => a?.firstP != null)?.firstP;
        /// <inheritdoc />
        public ToolPoint lastP => this.LastOrDefault(a => a?.lastP != null)?.lastP;
        /// <summary>TODO The remove last point.</summary>
        [PublicAPI]
        public void removeLastPoint() { this[this.Count - 1].removeLastPoint(); }

        // Default Constructor
        /// <summary>Initializes a new instance of the <see cref="MachineInstruction"/> class.</summary>
        /// <param name="m">TODO The m.</param>
        public MachineInstruction([NotNull] IMachine m)
        {
            this.m = m;
            this.mOs = new List<MachineOperation>();
            this.startPath = new ToolPath();
            this.endPath = new ToolPath();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }

        // Everything but the pre and post codes
        /// <summary>Initializes a new instance of the <see cref="MachineInstruction"/> class.</summary>
        /// <param name="name">TODO The name.</param>
        /// <param name="mach">TODO The mach.</param>
        /// <param name="mOs">TODO The m os.</param>
        /// <param name="startPath">TODO The start path.</param>
        /// <param name="endPath">TODO The end path.</param>
        public MachineInstruction([NotNull] string name, [NotNull] IMachine mach, [CanBeNull] List<MachineOperation> mOs, [CanBeNull] ToolPath startPath = null, [CanBeNull] ToolPath endPath = null)
        {
            this.name = name;
            this.m = mach;
            this.mOs = mOs ?? new List<MachineOperation>();
            this.startPath = startPath ?? new ToolPath();
            this.endPath = endPath ?? new ToolPath();
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }

        // Copy Constructor
        /// <summary>Initializes a new instance of the <see cref="MachineInstruction"/> class.</summary>
        /// <param name="mI">TODO The m i.</param>
        private MachineInstruction([NotNull] MachineInstruction mI)
        {
            this.name = string.Copy(mI.name);
            this.preCode = string.Copy(mI.preCode);
            this.postCode = string.Copy(mI.postCode);
            this.m = mI.m;
            this.startPath = mI.startPath.deepClone();
            this.endPath = mI.endPath.deepClone();
            this.mOs = new List<MachineOperation>();
            foreach (MachineOperation mO in mI) { this.mOs.Add(mO?.deepClone()); }
        }

        /// <summary>TODO The deep clone.</summary>
        /// <returns>The <see cref="MachineInstruction"/>.</returns>
        [NotNull]
        public MachineInstruction deepClone() => new MachineInstruction(this);

        // Copy basic information but add new paths
        /// <summary>TODO The deep clone with new paths.</summary>
        /// <param name="newMOs">TODO The new m os.</param>
        /// <returns>The <see cref="MachineInstruction"/>.</returns>
        [NotNull, PublicAPI]
        public MachineInstruction deepCloneWithNewPaths([NotNull] List<MachineOperation> newMOs)
        {
            MachineInstruction outInst = new MachineInstruction(this.m)
                {
                    name = string.Copy(this.name),
                    preCode = string.Copy(this.preCode),
                    postCode = string.Copy(this.postCode),
                    m = this.m,
                    startPath = this.startPath.deepClone(),
                    endPath = this.endPath.deepClone(),
                    mOs = newMOs
                };
            return outInst;
        }

        /// <inheritdoc />
        public string TypeDescription => "Complete set of operations for a run of the machine.";

        /// <inheritdoc />
        public string TypeName => "MachineInstruction";

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="T:System.String" />.</returns>
        public override string ToString()
        {
            int totalTP = this.SelectMany(mO => mO).Sum(tP => tP?.Count ?? 0);
            return "Machine Instruction: " + this.name + ", " + this.Count + " operations, " + totalTP + " total points.";
        }

        // Main functions

        // Take the collection of paths and hints and validate it into
        // a workable system, including adding MaterialTool and MaterialForm information
        // throughout
        /// <summary>TODO The process additions.</summary>
        /// <returns>The <see cref="MachineInstruction"/>.</returns>
        [NotNull]
        public MachineInstruction processAdditions()
        {
            MachineInstruction valid = this.deepCloneWithNewPaths(new List<MachineOperation>());

            // Mix this.startPath and this.validStart as required to
            // give a valid startPath.
            ToolPath validTP = this.validStart();
            valid.startPath.validate(validTP, this.m);
            validTP = valid.startPath;

            // process and validate all Operations
            ToolPath fP = new ToolPath();

            // If there is a start path use it.
            MachineOperation pMo;
            if (valid.startPath.Count == 0) // If no explicit start path is given just use first point.
            {
                ToolPoint sPt = this.firstP.deepClone();
                sPt.feed = 0;
                valid.startPath.Add(sPt);
            }

            pMo = new MachineOperation { valid.startPath };
            pMo.name = "Start";
            valid.Add(pMo.processAdditions(this.m, ref validTP));
            fP = valid.startPath;

            foreach (MachineOperation mO in this)
            {
                // Process the machine Operation
                pMo = mO.processAdditions(this.m, ref validTP);
                // Add transition from previous operation
                // insert and retract are handled within the operation
                // as they can change toolpaths for example adding activation code
                if (fP.Count > 0)
                {
                    pMo.InsertRange(0, this.m.transition(fP, pMo[0],false, false)); 
                }
                // record last path
                fP = pMo[pMo.Count - 1]; 
                // add to the output Instructions.
                valid.Add(pMo);
            }

            // Transition to end path
            if (valid.endPath.Count > 0)
            {
                pMo = new MachineOperation { valid.endPath };
                pMo.name = "End";
                pMo = pMo.processAdditions(this.m, ref validTP);
                if (fP.Count > 0 && pMo.Count > 0)
                {
                    ToolPath pMo0 = pMo[0];
                    pMo.RemoveAt(0); // remove toolpath in case it is changed by the transition.
                    pMo.InsertRange(0, this.m.transition(fP, pMo0, false));
                }

                valid.Add(pMo);
            }

            return valid;
        }

        /// <summary>TODO The write code.</summary>
        /// <returns>The <see cref="CodeInfo"/>.</returns>
        [NotNull]
        public CodeInfo writeCode()
        {
            ToolPath fP = this[0][0];
            if (fP.matForm == null) { Exceptions.matFormException(); }
            if (fP.matTool == null) { Exceptions.matToolException(); }

            CodeInfo co = new CodeInfo(this.m, fP.matForm, fP.matTool);

            this.m.writeFileStart(ref co, this);

            foreach (MachineOperation mO in this)
            {
                mO.writeCode(ref co, this.m, out ToolPath eP, fP);
                fP = eP;
            }

            this.m.writeFileEnd(ref co, this);

            return co;
        }

        // Hunt through the ToolPaths until we find all we need
        /// <summary>TODO The valid start.</summary>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        [NotNull]
        private ToolPath validStart()
        {
            // we need a MaterialTool and a MaterialForm
            bool mTFound = false;
            bool mFFound = false;

            ToolPath valid = new ToolPath();

            // scan through the paths looking for info
            foreach (ToolPath tP in this.SelectMany(mO => mO))
            {
                if (!mTFound && tP.matTool != null)
                {
                    mTFound = true;
                    valid.matTool = tP.matTool;
                }

                if (!mFFound && tP.matForm != null)
                {
                    mFFound = true;
                    valid.matForm = tP.matForm;
                }

                if (mTFound && mFFound) { return valid; }
            }

            // if the machine has one tool use that.
            if (this.m.mTs.Count != 1 || !mFFound)
            {
                throw new InvalidOperationException(
                    "Cannot validate Machine Instructions, there are either no ToolPaths with a MaterialTool or no ToolPaths with a MaterialForm.");
            }

            valid.matTool = this.m.mTs[0];
            return valid; // if we go through the whole thing without finding all the valid pieces
        }

        #region Point extraction and previews

        /// <inheritdoc />
        public ToolPath getSinglePath()
        {
            ToolPath oP = this[0].getSinglePath();
            for (int i = 1; i < this.Count; i++) { oP.AddRange(this[i].getSinglePath()); }
            return oP;
        }

        // Get the list of tooltip locations
        /// <summary>TODO The get points.</summary>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull, PublicAPI]
        public List<List<List<Point3d>>> getPoints()
        {
            return this.Select(mO => mO?.getPoints()).ToList();
        }

        // Get the list of tool directions
        /// <summary>TODO The get dirs.</summary>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull, PublicAPI]
        public List<List<List<Vector3d>>> getDirs()
        {
            return this.Select(mO => mO?.getDirs()).ToList();
        }

        // Create a path with the points
        /// <summary>TODO The get points and dirs.</summary>
        /// <param name="dirs">TODO The dirs.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull, PublicAPI]
        public List<List<List<Point3d>>> getPointsAndDirs([NotNull] out List<List<List<Vector3d>>> dirs)
        {
            dirs = this.getDirs();
            return this.getPoints();
        }

        // Bounding Box for previews
        /// <inheritdoc />
        public BoundingBox getBoundingBox()
        {
            BoundingBox bb = BoundingBox.Unset;
            for (int i = 0; i < this.Count; i++)
            { bb.Union(this[i].getBoundingBox()); }
            return bb;
        }

        // Create single polyline
        /// <summary>TODO The get line.</summary>
        /// <returns>The <see cref="PolylineCurve"/>.</returns>
        [NotNull]
        public PolylineCurve getLine() => this.getSinglePath().getLine();

        // Create polylines
        /// <summary>TODO The get lines.</summary>
        /// <returns>The <see cref="IEnumerable"/>.</returns>
        [NotNull]
        public IEnumerable<PolylineCurve> getLines()
        {
            List<PolylineCurve> lines = new List<PolylineCurve>();
            foreach (MachineOperation mO in this) { lines.AddRange(mO.getLines()); }
            return lines;
        }

        // Lines for each toolpoint
        /// <summary>TODO The tool lines.</summary>
        /// <returns>The <see cref="IEnumerable"/>.</returns>
        [NotNull]
        public IEnumerable<Line> toolLines()
        {
            List<Line> lines = new List<Line>();
            foreach (MachineOperation mO in this) { lines.AddRange(mO.toolLines()); }
            return lines;
        }

        #endregion

        #region List Functions 

        /// <inheritdoc />
        public int Count => this.mOs.Count;
        /// <inheritdoc />
        public bool IsReadOnly => ((IList<MachineOperation>)this.mOs).IsReadOnly;

        /// <inheritdoc />
        [NotNull]
        public MachineOperation this[int index] { get => this.mOs[index]; set => this.mOs[index] = value; }

        /// <inheritdoc />
        public int IndexOf(MachineOperation item) => this.mOs.IndexOf(item);
        /// <inheritdoc />
        public void Insert(int index, MachineOperation item)
        {
            if (item != null) { this.mOs.Insert(index, item); }
        }
        /// <inheritdoc />
        public void InsertRange(int index, IEnumerable<MachineOperation> items)
        {
            this.mOs.InsertRange(index, items);
        }

        /// <inheritdoc />
        public void RemoveAt(int index) => this.mOs.RemoveAt(index);
        /// <inheritdoc />
        public void Add(MachineOperation item)
        {
            if (item != null) { this.mOs.Add(item); }
        }

        /// <summary>TODO The add range.</summary>
        /// <param name="items">TODO The items.</param>
        [PublicAPI]
        public void AddRange([NotNull] IEnumerable<MachineOperation> items) => this.mOs.AddRange(items.Where(x => x != null));
        /// <inheritdoc />
        public void Clear() => this.mOs.Clear();
        /// <inheritdoc />
        public bool Contains(MachineOperation item) => this.mOs.Contains(item);
        /// <inheritdoc />
        public void CopyTo(MachineOperation[] array, int arrayIndex) => this.mOs.CopyTo(array, arrayIndex);
        /// <inheritdoc />
        public bool Remove(MachineOperation item) => this.mOs.Remove(item);
        /// <inheritdoc />
        public IEnumerator<MachineOperation> GetEnumerator() => this.mOs.GetEnumerator();
        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => this.mOs.GetEnumerator();

        #endregion
    }

    // Grasshopper Type Wrapper
    /// <summary>TODO The g h_ machine instruction.</summary>
    public sealed class GH_MachineInstruction : CAMel_Goo<MachineInstruction>, IGH_PreviewData
    {
        // Default Constructor;
        /// <summary>Initializes a new instance of the <see cref="GH_MachineInstruction"/> class.</summary>
        [UsedImplicitly]
        public GH_MachineInstruction() => this.Value = null;

        // Construct from value alone
        /// <summary>Initializes a new instance of the <see cref="GH_MachineInstruction"/> class.</summary>
        /// <param name="mI">TODO The m i.</param>
        public GH_MachineInstruction([CanBeNull] MachineInstruction mI) => this.Value = mI;

        // Copy Constructor.
        /// <summary>Initializes a new instance of the <see cref="GH_MachineInstruction"/> class.</summary>
        /// <param name="mI">TODO The m i.</param>
        public GH_MachineInstruction([CanBeNull] GH_MachineInstruction mI) => this.Value = mI?.Value?.deepClone();

        // Duplicate
        /// <inheritdoc />
        [NotNull]
        public override IGH_Goo Duplicate() => new GH_MachineInstruction(this);

        /// <inheritdoc />
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
                object ptr = this.Value.m;
                target = (T)ptr;
                return true;
            }
            // ReSharper disable once InvertIf
            if (typeof(T).IsAssignableFrom(typeof(GH_Machine)))
            {
                object ptr = new GH_Machine(this.Value.m);
                target = (T)ptr;
                return true;
            }

            return false;
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public BoundingBox ClippingBox => this.Value?.getBoundingBox() ?? BoundingBox.Unset;

        /// <inheritdoc />
        public void DrawViewportWires([CanBeNull] GH_PreviewWireArgs args)
        {
            if (this.Value == null || args?.Pipeline == null) { return; }
            foreach (PolylineCurve l in this.Value.getLines())
            {
                args.Pipeline.DrawCurve(l, args.Color);
            }

            args.Pipeline.DrawArrows(this.Value.toolLines(), args.Color);
        }

        /// <inheritdoc />
        public void DrawViewportMeshes([CanBeNull] GH_PreviewMeshArgs args) { }
    }

    // Grasshopper Parameter Wrapper
    /// <summary>TODO The g h_ machine instruction par.</summary>
    public class GH_MachineInstructionPar : GH_Param<GH_MachineInstruction>, IGH_PreviewObject
    {
        /// <summary>Initializes a new instance of the <see cref="GH_MachineInstructionPar"/> class.</summary>
        public GH_MachineInstructionPar()
            : base("Instructions", "MachInst", "Contains a collection of Machine Instructions", "CAMel", "  Params", GH_ParamAccess.item) { }
        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("7ded80e7-6a29-4534-a848-f9d1b897098f");

        /// <inheritdoc />
        public bool Hidden { get; set; }
        /// <inheritdoc />
        public bool IsPreviewCapable => true;
        /// <inheritdoc />
        public BoundingBox ClippingBox => this.Preview_ComputeClippingBox();
        /// <inheritdoc />
        public void DrawViewportWires([CanBeNull] IGH_PreviewArgs args) => this.Preview_DrawWires(args);
        /// <inheritdoc />
        public void DrawViewportMeshes([CanBeNull] IGH_PreviewArgs args) => this.Preview_DrawMeshes(args);

        /// <inheritdoc />
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.machineinstructions;
    }
}