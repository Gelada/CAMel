namespace CAMel.Types
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using CAMel.Types.Machine;
    using CAMel.Types.MaterialForm;

    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Types;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    // List of toolpaths forming a general Operation of the machine,
    // from the complex to the simple
    // creating a surface, drilling a whole, cutting out an object...
    // When paths within an operation have a stepdown then all first
    // step downs with be completed, then the second and so on.
    /// <summary>TODO The machine operation.</summary>
    public class MachineOperation : IList<ToolPath>, IToolPointContainer
    {
        /// <summary>TODO The t ps.</summary>
        [ItemNotNull, NotNull] private List<ToolPath> tPs;

        /// <inheritdoc />
        public ToolPoint firstP => this.FirstOrDefault(a => a?.firstP != null)?.firstP;
        /// <inheritdoc />
        public ToolPoint lastP => this.LastOrDefault(a => a?.lastP != null)?.lastP;
        /// <summary>TODO The remove last point.</summary>
        public void removeLastPoint()
        {
            this[this.Count - 1].removeLast();
        }

        // Default Constructor
        /// <summary>Initializes a new instance of the <see cref="MachineOperation"/> class.</summary>
        public MachineOperation()
        {
            this.tPs = new List<ToolPath>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }

        // From list of toolpaths
        /// <summary>Initializes a new instance of the <see cref="MachineOperation"/> class.</summary>
        /// <param name="tPs">TODO The t ps.</param>
        public MachineOperation([NotNull] List<ToolPath> tPs)
        {
            this.tPs = tPs;
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }

        // From toolpath
        /// <summary>Initializes a new instance of the <see cref="MachineOperation"/> class.</summary>
        /// <param name="tP">TODO The t p.</param>
        public MachineOperation([NotNull] ToolPath tP)
        {
            this.tPs = new List<ToolPath> { tP };
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }

        // Name and ToolPaths
        /// <summary>Initializes a new instance of the <see cref="MachineOperation"/> class.</summary>
        /// <param name="name">TODO The name.</param>
        /// <param name="tPs">TODO The t ps.</param>
        public MachineOperation([NotNull] string name, [NotNull] List<ToolPath> tPs)
        {
            this.name = name;
            this.tPs = tPs;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }

        // Copy Constructor
        /// <summary>Initializes a new instance of the <see cref="MachineOperation"/> class.</summary>
        /// <param name="mO">TODO The m o.</param>
        private MachineOperation([NotNull] MachineOperation mO)
        {
            this.name = string.Copy(mO.name);
            this.preCode = string.Copy(mO.preCode);
            this.postCode = string.Copy(mO.postCode);
            this.tPs = new List<ToolPath>();
            foreach (ToolPath tP in mO) { this.Add(tP?.deepClone()); }
        }

        /// <summary>TODO The deep clone.</summary>
        /// <returns>The <see cref="MachineOperation"/>.</returns>
        [NotNull]
        public MachineOperation deepClone() => new MachineOperation(this);

        // Return with new paths.
        /// <summary>TODO The deep clone with new paths.</summary>
        /// <param name="procPaths">TODO The proc paths.</param>
        /// <returns>The <see cref="MachineOperation"/>.</returns>
        [NotNull, PublicAPI]
        public MachineOperation deepCloneWithNewPaths([NotNull] List<ToolPath> procPaths)
        {
            MachineOperation outOp = new MachineOperation
                {
                    preCode = this.preCode,
                    postCode = this.postCode,
                    name = this.name,
                    tPs = procPaths
                };

            return outOp;
        }

        /// <inheritdoc />
        public string TypeDescription => "Single operation of the machine, from the complex (creating a surface) to the simple (drilling a hole).";

        /// <inheritdoc />
        /// <summary>TODO The type name.</summary>
        public string TypeName => "MachineOperation";

        /// <inheritdoc />
        public string name { get; set; }

        /// <inheritdoc />
        public string preCode { get; set; }
        /// <inheritdoc />
        public string postCode { get; set; }

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        public override string ToString()
        {
            int totalTP = this.Sum(tP => tP?.Count ?? 0);
            return "Machine Operation: " + this.name + ", " + this.Count + " toolpaths, " + totalTP + " total tool points.";
        }

        // Process the toolpaths for additions and ensure ToolPaths are valid for writing.
        /// <summary>TODO The process additions.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="validTP">TODO The valid tp.</param>
        /// <returns>The <see cref="MachineOperation"/>.</returns>
        [NotNull]
        public MachineOperation processAdditions([NotNull] IMachine m, [NotNull] ref ToolPath validTP)
        {
            // Wow a 3d block of ToolPaths
            // Each of the stepdown paths can have several pieces (1st level)
            // Each ToolPath has several stepdown paths (2nd level)
            // We started with a list of toolpaths (1st level)
            // We create this block and then order it so we do
            // all preparation a level at a time and then do a final pass of all paths
            List<List<List<ToolPath>>> newPaths = new List<List<List<ToolPath>>>();

            // Store finishing paths separately
            List<List<ToolPath>> finishPaths = new List<List<ToolPath>>();

            foreach (ToolPath tP in this)
            {
                tP.validate(validTP, m);
                validTP = tP;
                newPaths.Add(tP.processAdditions(m, out List<ToolPath> fP));
                finishPaths.Add(fP);
            }

            // Create the list for the output
            List<ToolPath> procPaths = new List<ToolPath>();

            List<ToolPath> levelPaths; // all paths on one level

            // Find path with most levels
            int levels = newPaths.Select(lTp => lTp?.Count ?? 0).Concat(new[] { 0 }).Max();

            // do the roughing layers
            for (int i = 0; i < levels; i++)
            {
                levelPaths = new List<ToolPath>();
                foreach (List<List<ToolPath>> lTp in newPaths.Where(lTp => i < lTp?.Count))
                { levelPaths.AddRange(lTp[i] ?? new List<ToolPath>()); }

                // sort here (remember to only move chunks that are outside the material!)
                procPaths.AddRange(levelPaths);
            }

            // finishing cuts
            // find path with most levels
            levels = finishPaths.Select(lTp => lTp?.Count ?? 0).Concat(new[] { 0 }).Max();

            // add finishing paths
            for (int i = 0; i < levels; i++)
            {
                levelPaths = new List<ToolPath>();

                // ReSharper disable once LoopCanBePartlyConvertedToQuery
                foreach (List<ToolPath> lTp in finishPaths.Where(lTp => i < lTp?.Count))
                {
                    if (lTp?[i] == null) { continue; }
                    // TODO: Cleanup without insert/retract
                    //levelPaths.AddRange(m.insertRetract(lTp[i]));
                    levelPaths.Add(lTp[i]);
                }

                procPaths.AddRange(levelPaths);
            }

            List<ToolPath> transPaths = new List<ToolPath>();
            ToolPath frP = new ToolPath();
            bool first = true;
            foreach (ToolPath tP in procPaths.Where(tP => tP?.Count > 0))
            {
                if (first)
                {
                    List<ToolPath> trP = m.insert(tP);
                    // separate the last path (tP with possibly alterations by insert, such as activation)
                    frP = trP[trP.Count - 1];
                    trP.RemoveAt(trP.Count - 1);
                    transPaths.AddRange(trP);
                    first = false;
                }
                 else {
                    if (frP.Count > 0)
                    {
                        // Calculate transition
                        List<ToolPath> trP = m.transition(frP, tP);

                        // separate the last path (tP with possibly alterations by transition, such as activation)
                        frP = trP[trP.Count - 1];
                        trP.RemoveAt(trP.Count - 1);

                        transPaths.AddRange(trP);
                    }
                }
            }
            // Add last path and retract

            transPaths.AddRange(m.retract(frP));

            return this.deepCloneWithNewPaths(transPaths);
        }

        // Write GCode for this operation
        // [ContractAnnotation("s:null => false,result:null")]
        /// <summary>TODO The write code.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="m">TODO The m.</param>
        /// <param name="eP">TODO The e p.</param>
        /// <param name="sP">TODO The s p.</param>
        public void writeCode([NotNull] ref CodeInfo co, [NotNull] IMachine m, [NotNull] out ToolPath eP, [NotNull] ToolPath sP)
        {
            m.writeOpStart(ref co, this);

            ToolPath oldPath = sP;

            foreach (ToolPath tP in this.Where(tP => tP?.Count > 0))
            {
                // Check for jump between paths
                if (oldPath.Count > 0) { m.jumpCheck(ref co, oldPath, tP); }

                // Add Path to Code
                m.writeCode(ref co, tP);
                oldPath = tP;
            }

            m.writeOpEnd(ref co, this);

            eP = oldPath;
        }

        // Process a collage of bits and pieces into a list of Operations
        /// <summary>TODO The to operations.</summary>
        /// <param name="scraps">TODO The scraps.</param>
        /// <param name="ignores">TODO The ignores.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        internal static List<MachineOperation> toOperations([CanBeNull] object scraps, out int ignores)
        {
            List<MachineOperation> oMOs = new List<MachineOperation>();
            ignores = 0;

            switch (scraps) {
                case null: return oMOs;
                case MachineOperation item:
                    oMOs.Add(item);
                    break;
                case List<ToolPath> ps:
                    oMOs.Add(new MachineOperation(ps));
                    break;
                case IMaterialForm mF:
                    oMOs.Add(new MachineOperation(new ToolPath(mF)));
                    break;

                // Otherwise process mixed up any other sort of list by term.
                case MaterialTool mT:
                    oMOs.Add(new MachineOperation(new ToolPath(mT)));
                    break;
                case IEnumerable sc:
                    {
                        ToolPath tempTP = new ToolPath();
                        MachineOperation tempMO = new MachineOperation();
                        foreach (object oB in sc)
                        {
                            switch (oB)
                            {
                                case Point3d pt:
                                    tempTP.Add(new ToolPoint(pt));
                                    break;
                                case ToolPoint tPt:
                                    tempTP.Add(tPt);
                                    break;
                                default:
                                    {
                                        if (tempTP.Count > 0)
                                        {
                                            tempMO.Add(tempTP);
                                            tempTP = new ToolPath();
                                        }

                                        switch (oB)
                                        {
                                            case ToolPath tP:
                                                tempMO.Add(tP);
                                                break;
                                            case MachineOperation mO:
                                                if (tempMO.Count > 0)
                                                {
                                                    oMOs.Add(tempMO);
                                                    tempMO = new MachineOperation();
                                                }

                                                oMOs.Add(mO);
                                                break;
                                            case MachineInstruction mI:
                                                if (tempMO.Count > 0)
                                                {
                                                    oMOs.Add(tempMO);
                                                    tempMO = new MachineOperation();
                                                }

                                                oMOs.AddRange(mI);
                                                break;
                                            case IMaterialForm uMF:
                                                tempMO.Add(new ToolPath(uMF));
                                                break;
                                            case MaterialTool uMT:
                                                tempMO.Add(new ToolPath(uMT));
                                                break;
                                            default:
                                                ignores++;
                                                break;
                                        }

                                        break;
                                    }
                            }
                        }

                        if (tempTP.Count > 0) { tempMO.Add(tempTP); }
                        if (tempMO.Count > 0) { oMOs.Add(tempMO); }
                        break;
                    }
            }

            return oMOs;
        }

        /// <summary>Transform the MachineOperation in place WARNING: for non rigid transforms will lose information beyond tool position and direction.</summary>
        /// <param name="transform">Transform to apply</param>
        public void transform(Transform transform) { foreach (ToolPath tP in this) { tP.transform(transform); } }

        #region Point extraction and previews

        /// <inheritdoc />
        public ToolPath getSinglePath()
        {
            if (!this.Any()) { return new ToolPath(); }

            ToolPath oP = this[0].getSinglePath();
            for (int i = 1; i < this.Count; i++)
            {
                oP.AddRange(this[i].getSinglePath());
            }

            return oP;
        }

        // Get the list of tooltip locations
        /// <summary>TODO The get points.</summary>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        public List<List<Point3d>> getPoints()
        {
            return this.Select(tP => tP?.getPoints()).ToList();
        }

        // Get the list of tool directions
        /// <summary>TODO The get dirs.</summary>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        public List<List<Vector3d>> getDirs()
        {
            return this.Select(tP => tP?.getDirs()).ToList();
        }

        // Create a path with the points
        /// <summary>TODO The get points and dirs.</summary>
        /// <param name="dirs">TODO The dirs.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull, PublicAPI]
        public List<List<Point3d>> getPointsAndDirs([CanBeNull] out List<List<Vector3d>> dirs)
        {
            List<List<Point3d>> ptsOut = new List<List<Point3d>>();
            dirs = new List<List<Vector3d>>();
            foreach (ToolPath tP in this)
            {
                ptsOut.Add(tP.getPointsAndDirs(out List<Vector3d> tPDirs));
                dirs.Add(tPDirs);
            }

            return ptsOut;
        }

        // Bounding Box for previews
        /// <inheritdoc />
        /// <summary>TODO The get bounding box.</summary>
        /// <returns>The <see cref="T:Rhino.Geometry.BoundingBox" />.</returns>
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
            return this.Select(tP => tP?.getLine()).ToList();
        }

        // Lines for each toolpoint
        /// <summary>TODO The tool lines.</summary>
        /// <returns>The <see cref="IEnumerable"/>.</returns>
        [NotNull]
        public IEnumerable<Line> toolLines()
        {
            List<Line> lines = new List<Line>();
            foreach (ToolPath tP in this) { lines.AddRange(tP.toolLines()); }
            return lines;
        }

        #endregion

        #region List Functions

        /// <inheritdoc />
        public int Count => this.tPs.Count;
        /// <inheritdoc />
        public bool IsReadOnly => ((IList<ToolPath>)this.tPs).IsReadOnly;

        /// <inheritdoc />
        [NotNull]
        public ToolPath this[int index] { get => this.tPs[index]; set => this.tPs[index] = value; }

        /// <inheritdoc />
        public int IndexOf(ToolPath item) => this.tPs.IndexOf(item);

        /// <inheritdoc />
        public void Insert(int index, ToolPath item)
        {
            if (item != null) { this.tPs.Insert(index, item); }
        }

        /// <inheritdoc />
        public void InsertRange(int index, IEnumerable<ToolPath> items)
        {
            this.tPs.InsertRange(index, items); 
        }

        /// <inheritdoc />
        public void RemoveAt(int index) => this.tPs.RemoveAt(index);

        /// <inheritdoc />
        public void Add(ToolPath item)
        {
            if (item != null) { this.tPs.Add(item); }
        }

        /// <summary>TODO The add range.</summary>
        /// <param name="items">TODO The items.</param>
        [PublicAPI]
        public void AddRange([NotNull] IEnumerable<ToolPath> items) => this.tPs.AddRange(items.Where(x => x != null));

        /// <inheritdoc />
        public void Clear() => this.tPs.Clear();

        /// <inheritdoc />
        public bool Contains(ToolPath item) => this.tPs.Contains(item);

        /// <inheritdoc />
        public void CopyTo(ToolPath[] array, int arrayIndex) => this.tPs.CopyTo(array, arrayIndex);

        /// <inheritdoc />
        public bool Remove(ToolPath item) => this.tPs.Remove(item);

        /// <inheritdoc />
        public IEnumerator<ToolPath> GetEnumerator() => this.tPs.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => this.tPs.GetEnumerator();

        #endregion
    }

    // Grasshopper Type Wrapper
    /// <summary>TODO The g h_ machine operation.</summary>
    public sealed class GH_MachineOperation : CAMel_Goo<MachineOperation>, IGH_PreviewData
    {
        // Default Constructor
        /// <summary>Initializes a new instance of the <see cref="GH_MachineOperation"/> class.</summary>
        [UsedImplicitly]
        public GH_MachineOperation() => this.Value = new MachineOperation();

        // Construct from value alone
        /// <summary>Initializes a new instance of the <see cref="GH_MachineOperation"/> class.</summary>
        /// <param name="mO">TODO The m o.</param>
        public GH_MachineOperation([CanBeNull] MachineOperation mO) => this.Value = mO;

        // Copy Constructor.
        /// <summary>Initializes a new instance of the <see cref="GH_MachineOperation"/> class.</summary>
        /// <param name="mO">TODO The m o.</param>
        public GH_MachineOperation([CanBeNull] GH_MachineOperation mO) => this.Value = mO?.Value?.deepClone();

        /// <inheritdoc />
        [NotNull]
        public override IGH_Goo Duplicate() => new GH_MachineOperation(this);

        /// <inheritdoc />
        public override bool CastTo<T>(ref T target)
        {
            if (this.Value == null) { return false; }
            if (typeof(T).IsAssignableFrom(typeof(MachineOperation)))
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
            // ReSharper disable once InvertIf
            if (typeof(T).IsAssignableFrom(typeof(GH_Curve)))
            {
                target = (T)(object)new GH_Curve(this.Value.getLine());
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public override bool CastFrom([CanBeNull] object source)
        {
            switch (source) {
                case null: return false;

                // Cast from unwrapped MO
                case MachineOperation value:
                    this.Value = value;
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
    /// <summary>TODO The g h_ machine operation par.</summary>
    public class GH_MachineOperationPar : GH_Param<GH_MachineOperation>, IGH_PreviewObject
    {
        /// <summary>Initializes a new instance of the <see cref="GH_MachineOperationPar"/> class.</summary>
        public GH_MachineOperationPar()
            : base(
                "Operation", "MachOp",
                "Contains a collection of Machine Operations",
                "CAMel", "  Params", GH_ParamAccess.item) { }

        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("e0dfd958-f0fb-46b7-b743-04e071ea25fd");

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
        protected override System.Drawing.Bitmap Icon => Properties.Resources.machineoperations;
    }
}