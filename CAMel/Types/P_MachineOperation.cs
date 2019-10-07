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

namespace CAMel.Types
{
    // List of toolpaths forming a general Operation of the machine,
    // from the complex to the simple
    // creating a surface, drilling a whole, cutting out an object...
    // When paths within an operation have a stepdown then all first
    // step downs with be completed, then the second and so on.
    public class MachineOperation : IList<ToolPath>, IToolPointContainer
    {
        [ItemNotNull, NotNull] private List<ToolPath> _tPs;

        public ToolPoint firstP => this.FirstOrDefault(a => a?.firstP != null)?.lastP;
        public ToolPoint lastP => this.LastOrDefault(a => a?.lastP != null)?.lastP;
        public void removeLastPoint()
        {
            this[this.Count - 1].removeLast();
        }

        // Default Constructor
        public MachineOperation()
        {
            this._tPs = new List<ToolPath>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // From list of toolpaths
        public MachineOperation([NotNull] List<ToolPath> tPs)
        {
            this._tPs = tPs;
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // From toolpath
        public MachineOperation([NotNull] ToolPath tP)
        {
            this._tPs = new List<ToolPath> {tP};
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Name and ToolPaths
        public MachineOperation([NotNull] string name, [NotNull] List<ToolPath> tPs)
        {
            this.name = name;
            this._tPs = tPs;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Copy Constructor
        private MachineOperation([NotNull] MachineOperation mO)
        {
            this.name = string.Copy(mO.name);
            this.preCode = string.Copy(mO.preCode);
            this.postCode = string.Copy(mO.postCode);
            this._tPs = new List<ToolPath>();
            foreach (ToolPath tP in mO) { Add(tP?.deepClone()); }
        }
        [NotNull] public MachineOperation deepClone() => new MachineOperation(this);

        // Return with new paths.
        [NotNull, PublicAPI]
        public MachineOperation deepCloneWithNewPaths([NotNull] List<ToolPath> procPaths)
        {
            MachineOperation outOp = new MachineOperation
            {
                preCode = this.preCode,
                postCode = this.postCode,
                name = this.name,
                _tPs = procPaths
            };

            return outOp;
        }

        public string TypeDescription => "Single operation of the machine, from the complex (creating a surface) to the simple (drilling a hole).";

        public string TypeName => "MachineOperation";

        public string name { get; set; }

        public string preCode { get; set; }
        public string postCode { get; set; }

        public override string ToString()
        {
            int totalTP = this.Sum(tP => tP.Count);
            return "Machine Operation: " + this.name + ", " + this.Count + " toolpaths, " + totalTP + " total tool points.";
        }

        // Process the toolpaths for additions and ensure ToolPaths are valid for writing.
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
            int levels = newPaths.Select(lTp => lTp.Count).Concat(new[] {0}).Max();
            // do the roughing layers
            for (int i = 0; i < levels; i++)
            {
                levelPaths = new List<ToolPath>();
                foreach (List<List<ToolPath>> lTp in newPaths.Where(lTp => i < lTp.Count && lTp[i] != null))
                { levelPaths.AddRange(lTp[i]); }

                // sort here (remember to only move chunks that are outside the material!)

                procPaths.AddRange(levelPaths);
            }
            // finishing cuts
            // find path with most levels
            levels = finishPaths.Select(lTp => lTp.Count).Concat(new[] {0}).Max();
            // add finishing paths

            for (int i = 0; i < levels; i++)
            {
                levelPaths = new List<ToolPath>();
                foreach (List<ToolPath> lTp in finishPaths.Where(lTp => i < lTp.Count).Where(lTp => lTp[i] != null))
                {
                    levelPaths.AddRange(m.insertRetract(lTp[i]));
                }

                procPaths.AddRange(levelPaths);
            }

            List<ToolPath> transPaths = new List<ToolPath>();
            ToolPath frP = new ToolPath();
            foreach (ToolPath tP in procPaths.Where(tP => tP.Count > 0))
            {
                // Check if transition is needed
                if (frP.Count > 0 && (tP.label == PathLabel.Insert || frP.label == PathLabel.Retract))
                {
                    // Calculate transition
                    ToolPath trP = m.transition(frP, tP);
                    // Remove last point of previous path
                    transPaths[transPaths.Count - 1]?.removeLast();
                    transPaths.Add(trP);
                }
                transPaths.Add(tP);
                frP = tP;
            }

            return deepCloneWithNewPaths(transPaths);
        }

        // Write GCode for this operation
        //[ContractAnnotation("s:null => false,result:null")]
        public void writeCode([NotNull] ref CodeInfo co, [NotNull] IMachine m, [NotNull] out ToolPath eP, [NotNull] ToolPath sP)
        {
            m.writeOpStart(ref co, this);

            ToolPath oldPath = sP;

            foreach (ToolPath tP in this.Where(tP => tP.Count > 0))
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
            }

            switch (scraps) {
                // Otherwise process mixed up any other sort of list by term.
                case MaterialTool mT:
                    oMOs.Add(new MachineOperation(new ToolPath(mT)));
                    break;
                case IEnumerable sc: {
                    bool tpPath = false;
                    ToolPath tempTP = new ToolPath();
                    foreach (object oB in sc)
                    {
                        switch (oB) {
                            case Point3d pt:
                                tpPath = true;
                                tempTP.Add(new ToolPoint(pt));
                                break;
                            case ToolPoint tPt:
                                tpPath = true;
                                tempTP.Add(tPt);
                                break;
                            default: {
                                if (tpPath)
                                {
                                    oMOs.Add(new MachineOperation(new List<ToolPath> {tempTP}));
                                    tpPath = false;
                                    tempTP = new ToolPath();
                                }
                                switch (oB) {
                                    case ToolPath tP:
                                        oMOs.Add(new MachineOperation(new List<ToolPath> {tP}));
                                        break;
                                    case MachineOperation mO:
                                        oMOs.Add(mO);
                                        break;
                                    case MachineInstruction mI:
                                        oMOs.AddRange(mI);
                                        break;
                                    case IMaterialForm uMF:
                                        oMOs.Add(new MachineOperation(new ToolPath(uMF)));
                                        break;
                                    case MaterialTool uMT:
                                        oMOs.Add(new MachineOperation(new ToolPath(uMT)));
                                        break;
                                    default:
                                        ignores++;
                                        break;
                                }
                                break;
                            }
                        }
                    }
                    if (tpPath)
                    {
                        oMOs.Add(new MachineOperation(new List<ToolPath> {tempTP}));
                    }
                    break;
                }
            }
            return oMOs;
        }

        #region Point extraction and previews

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
        [NotNull]
        public List<List<Point3d>> getPoints()
        {
            return this.Select(tP => tP.getPoints()).ToList();
        }
        // Get the list of tool directions
        [NotNull]
        public List<List<Vector3d>> getDirs()
        {
            return this.Select(tP => tP.getDirs()).ToList();
        }
        // Create a path with the points
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
            return this.Select(tP => tP.getLine()).ToList();
        }
        // Lines for each toolpoint
        [NotNull]
        public IEnumerable<Line> toolLines()
        {
            List<Line> lines = new List<Line>();
            foreach (ToolPath tP in this) { lines.AddRange(tP.toolLines()); }
            return lines;
        }

        #endregion

        #region List Functions

        public int Count => this._tPs.Count;
        public bool IsReadOnly => ((IList<ToolPath>) this._tPs).IsReadOnly;
        [NotNull] public ToolPath this[int index] { get => this._tPs[index]; set => this._tPs[index] = value; }
        public int IndexOf(ToolPath item) => this._tPs.IndexOf(item);
        public void Insert(int index, ToolPath item)
        {
            if (item != null) { this._tPs.Insert(index, item); }
        }
        public void RemoveAt(int index) => this._tPs.RemoveAt(index);
        public void Add(ToolPath item)
        {
            if (item != null) { this._tPs.Add(item); }
        }
        [PublicAPI] public void AddRange([NotNull] IEnumerable<ToolPath> items) => this._tPs.AddRange(items.Where(x => x != null));
        public void Clear() => this._tPs.Clear();
        public bool Contains(ToolPath item) => this._tPs.Contains(item);
        public void CopyTo(ToolPath[] array, int arrayIndex) => this._tPs.CopyTo(array, arrayIndex);
        public bool Remove(ToolPath item) => this._tPs.Remove(item);
        public IEnumerator<ToolPath> GetEnumerator() => this._tPs.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this._tPs.GetEnumerator();

        #endregion
    }

    // Grasshopper Type Wrapper
    public sealed class GH_MachineOperation : CAMel_Goo<MachineOperation>, IGH_PreviewData
    {
        // Default Constructor
        [UsedImplicitly] public GH_MachineOperation() { this.Value = new MachineOperation(); }
        // Construct from value alone
        public GH_MachineOperation([CanBeNull] MachineOperation mO) { this.Value = mO; }
        // Copy Constructor.
        public GH_MachineOperation([CanBeNull] GH_MachineOperation mO) { this.Value = mO?.Value?.deepClone(); }
        // Duplicate
        [NotNull] public override IGH_Goo Duplicate() => new GH_MachineOperation(this);

        public override bool CastTo<T>(ref T target)
        {
            if (this.Value == null) { return false; }
            if (typeof(T).IsAssignableFrom(typeof(MachineOperation)))
            {
                object ptr = this.Value;
                target = (T) ptr;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(ToolPath)))
            {
                object ptr = this.Value.getSinglePath();
                target = (T) ptr;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_ToolPath)))
            {
                object ptr = new GH_ToolPath(this.Value.getSinglePath());
                target = (T) ptr;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(Curve)))
            {
                target = (T) (object) this.Value.getLine();
                return true;
            }
            // ReSharper disable once InvertIf
            if (typeof(T).IsAssignableFrom(typeof(GH_Curve)))
            {
                target = (T) (object) new GH_Curve(this.Value.getLine());
                return true;
            }

            return false;
        }
        public override bool CastFrom([CanBeNull] object source)
        {
            switch (source) {
                case null: return false;
                //Cast from unwrapped MO
                case MachineOperation value:
                    this.Value = value;
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
    public class GH_MachineOperationPar : GH_Param<GH_MachineOperation>, IGH_PreviewObject
    {
        public GH_MachineOperationPar() :
            base("Operation", "MachOp", "Contains a collection of Machine Operations", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid => new Guid("e0dfd958-f0fb-46b7-b743-04e071ea25fd");

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
        protected override System.Drawing.Bitmap Icon => Properties.Resources.machineoperations;
    }
}