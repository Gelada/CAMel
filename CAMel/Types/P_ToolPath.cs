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
    // One action of the machine, such as cutting a line
    public class ToolPath : IList<ToolPoint>, IToolPointContainer
    {
        [ItemNotNull, NotNull] private List<ToolPoint> _pts; // Positions of the machine
        public MaterialTool matTool { get; set; } // Material and tool to cut it with
        public IMaterialForm matForm { get; set; } // Shape of the material
        [NotNull] public ToolPathAdditions additions; // Features we might add to the path

        public ToolPoint firstP => this.Count > 0 ? this[0] : null;

        public ToolPoint lastP => this.Count > 0 ? this[this.Count - 1] : null;

        // Default Constructor, set everything to empty
        public ToolPath()
        {
            this.name = string.Empty;
            this._pts = new List<ToolPoint>();
            this.matTool = null;
            this.matForm = null;
            this.additions = ToolPathAdditions.replaceable;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Just a MaterialTool
        public ToolPath([NotNull] MaterialTool mT)
        {
            this.name = string.Empty;
            this._pts = new List<ToolPoint>();
            this.matTool = mT;
            this.matForm = null;
            this.additions = ToolPathAdditions.replaceable;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Just a MaterialForm
        public ToolPath([NotNull] IMaterialForm mf)
        {
            this.name = string.Empty;
            this._pts = new List<ToolPoint>();
            this.matTool = null;
            this.matForm = mf;
            this.additions = ToolPathAdditions.replaceable;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // MaterialTool and Form
        public ToolPath([NotNull] string name, [CanBeNull] MaterialTool mT, [CanBeNull] IMaterialForm mF)
        {
            this.name = name;
            this._pts = new List<ToolPoint>();
            this.matTool = mT;
            this.matForm = mF;
            this.additions = ToolPathAdditions.replaceable;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // MaterialTool, Form and features
        public ToolPath([NotNull] string name, [CanBeNull] MaterialTool mT, [CanBeNull] IMaterialForm mF, [NotNull] ToolPathAdditions tpa)
        {
            this.name = name;
            this._pts = new List<ToolPoint>();
            this.matTool = mT;
            this.matForm = mF;
            this.additions = tpa;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Copy Constructor
        private ToolPath([NotNull] ToolPath tP)
        {
            this.name = string.Copy(tP.name);
            this._pts = new List<ToolPoint>();
            foreach (ToolPoint pt in tP) { Add(pt?.deepClone()); }
            this.matTool = tP.matTool;
            this.matForm = tP.matForm;
            this.preCode = string.Copy(tP.preCode);
            this.postCode = string.Copy(tP.postCode);
            this.additions = tP.additions.deepClone();
        }

        [NotNull, Pure] public ToolPath deepClone() => new ToolPath(this);
        // create a lifted path
        [NotNull, Pure]
        public ToolPath deepClone(double h, [NotNull] IMachine m)
        {
            if (Math.Abs(h) < CAMel_Goo.Tolerance) { return deepClone(); }
            ToolPath tP = deepCloneWithNewPoints(new List<ToolPoint>());
            foreach (ToolPoint tPt in this)
            {
                ToolPoint newTPt = tPt.deepClone();
                newTPt.pt = newTPt.pt + h * m.toolDir(tPt);
                tP.Add(newTPt);
            }
            return tP;
        }

        [NotNull, Pure]
        public ToolPath deepCloneWithNewPoints([CanBeNull] List<ToolPoint> pts)
        {
            ToolPath newTP = new ToolPath
            {
                name = string.Copy(this.name),
                matTool = this.matTool,
                matForm = this.matForm,
                preCode = string.Copy(this.preCode),
                postCode = string.Copy(this.postCode),
                additions = this.additions.deepClone(),
                _pts = pts ?? new List<ToolPoint>()
            };
            return newTP;
        }
        // Copy in features from the valid ToolPath if this does not yet have its own.
        // TODO create an additions.Unset;
        public void validate([NotNull] ToolPath valid, [NotNull] IMachine m)
        {
            this.matTool = this.matTool ?? valid.matTool;
            this.matForm = this.matForm ?? valid.matForm;
            this.additions.replace(m.defaultTPA);
        }

        public string TypeDescription => "An action of the machine, for example cutting a single line";
        public string TypeName => "ToolPath";

        public string name { get; set; }

        public string preCode { get; set; }
        public string postCode { get; set; }

        public override string ToString() => "ToolPath: " + this.name + " with " + this.Count + " points.";

        // Main functions

        // Process any additions to the path and return
        // list of list of toolpaths (for stepdown)
        [NotNull, Pure]
        public List<List<ToolPath>> processAdditions([NotNull] IMachine m, [CanBeNull] out List<ToolPath> fP)
        {
            if (this.matTool == null) { Exceptions.matToolException(); }
            if (this.matForm == null) { Exceptions.matFormException(); }

            // offset Curve
            List<ToolPath> useTP = m.offSet(this);

            // adjust path for three axis (or index three axis)
            for (int i = 0; i < useTP.Count; i++)
            {
                if (useTP[i] == null) { Exceptions.nullPanic(); }
                useTP[i] = this.additions.threeAxisHeightOffset ? m.threeAxisHeightOffset(useTP[i]) : useTP[i];
            }

            // add steps into material
            List<List<ToolPath>> roughPaths = new List<List<ToolPath>>();
            if (this.additions.stepDown)
            {
                foreach (ToolPath tP in useTP)
                {
                    List<List<ToolPath>> rPs = m.stepDown(tP);
                    for (int i = 0; i < rPs.Count; i++)
                    {
                        if (roughPaths.Count < i) { roughPaths.Add(new List<ToolPath>()); }
                        if (roughPaths[i] != null && rPs[i] != null) { roughPaths[i].AddRange(rPs[i]); }
                    }
                }
            }

            // add finishing paths, processing onion
            fP = new List<ToolPath>();
            foreach (ToolPath tP in useTP) { fP.AddRange(m.finishPaths(tP)); }

            // add insert and retract moves

            foreach (List<ToolPath> tP in roughPaths)
            {
                if (tP == null) { continue; }
                for (int i = 0; i < tP.Count; i++)
                {
                    if (tP[i] == null) { continue; }
                    tP[i] = m.insertRetract(tP[i]);
                }
            }
            for (int i = 0; i < fP.Count; i++)
            {
                if (fP[i] == null) { continue; }
                fP[i] = m.insertRetract(fP[i]);
            }

            return roughPaths;
        }

        // Use a curve and direction vector to create a path of toolpoints
        public bool convertCurve([CanBeNull] Curve c, Vector3d d)
        {
            if (c?.IsValid != true) { return false; }
            if (this.matTool == null) { Exceptions.matToolException(); }

            Curve c2 = c.ToPolyline(0, 0, Math.PI, 0, 0, this.matTool.tolerance, this.matTool.minStep,
                20.0 * this.matTool.toolWidth, true);
            if (c2 == null) { return false; }
            c2.TryGetPolyline(out Polyline pL);

            this._pts = new List<ToolPoint>();

            // Add the points to the Path

            foreach (Point3d pt in pL)
            {
                ToolPoint tPt = new ToolPoint(pt, d);
                Add(tPt);
            }
            return true;
        }
        private const double _AccTol = 0.0001;
        [NotNull, PublicAPI]
        public static PolylineCurve convertAccurate([NotNull] Curve c)
        {
            // Check if already a polyline, otherwise make one
            PolylineCurve plC = c.TryGetPolyline(out Polyline p)
                ? new PolylineCurve(p)
                : c.ToPolyline(0, 0, Math.PI, 0, 0, _AccTol * 5.0, 0, 0, true);

            return plC ?? new PolylineCurve();
        }

        [NotNull]
        internal static ToolPath toPath([CanBeNull] object scraps)
        {
            ToolPath oP = new ToolPath();
            switch (scraps) {
                case IToolPointContainer container:
                    oP.AddRange(container.getSinglePath());
                    break;
                case Point3d pt:
                    oP.Add(pt);
                    break;
                case IEnumerable li:
                {
                    foreach (object oB in li)
                    {
                        switch (oB) {
                            case IToolPointContainer tPc:
                                oP.AddRange(tPc.getSinglePath());
                                break;
                            case Point3d pti:
                                oP.Add(pti);
                                break;
                        }
                    }
                    break;
                }
            }
            return oP;
        }

        #region Point extraction and previews

        public ToolPath getSinglePath() => deepClone();
        // Get the list of tooltip locations
        [NotNull, Pure]
        public List<Point3d> getPoints()
        {
            List<Point3d> points = new List<Point3d>();

            foreach (ToolPoint tP in this) { points.Add(tP.pt); }

            return points;
        }
        // Get the list of tool directions
        [NotNull, Pure]
        public List<Vector3d> getDirs()
        {
            List<Vector3d> dirs = new List<Vector3d>();

            foreach (ToolPoint tP in this) { dirs.Add(tP.dir); }
            return dirs;
        }
        // Create a path with the points
        [NotNull, Pure]
        public List<Point3d> getPointsAndDirs([CanBeNull] out List<Vector3d> dirs)
        {
            List<Point3d> ptsOut = new List<Point3d>();
            dirs = new List<Vector3d>();
            foreach (ToolPoint tPt in this)
            {
                ptsOut.Add(tPt.pt);
                dirs.Add(tPt.dir);
            }
            return ptsOut;
        }
        // Get the list of speeds and feeds (a vector with speed in X and feed in Y)
        [NotNull, Pure]
        public IEnumerable<Vector3d> getSpeedFeed()
        {
            List<Vector3d> sF = new List<Vector3d>();

            foreach (ToolPoint tP in this) { sF.Add(new Vector3d(tP.speed, tP.feed, 0)); }
            return sF;
        }

        // Bounding Box for previews
        [Pure]
        public BoundingBox getBoundingBox()
        {
            BoundingBox bb = BoundingBox.Unset;
            for (int i = 0; i < this.Count; i++)
            { bb.Union(this[i].getBoundingBox()); }
            return bb;
        }
        // Create a polyline
        [NotNull, Pure] public PolylineCurve getLine() => new PolylineCurve(getPoints());

        // Lines for each toolpoint
        [NotNull, Pure]
        public IEnumerable<Line> toolLines()
        {
            List<Line> lines = new List<Line>();
            foreach (ToolPoint tP in this) { lines.Add(tP.toolLine()); }
            return lines;
        }

        [Pure]
        public bool planarOffset(out Vector3d dir)
        {
            dir = this[0].dir;
            // Check that there is a unique directions
            foreach (ToolPoint tPt in this)
            { if ((dir - tPt.dir).SquareLength > CAMel_Goo.Tolerance) { return false; } }
            // Check for planarity
            return getLine().IsPlanar();
        }
        [Pure]
        public bool isClosed()
        {
            if (this.firstP == null || this.lastP == null) { return false; }
            return this.firstP.pt.DistanceTo(this.lastP.pt) < CAMel_Goo.Tolerance;
        }

        #endregion

        #region List Functions

        public int Count => this._pts.Count;
        public bool IsReadOnly => ((IList<ToolPoint>) this._pts).IsReadOnly;
        [NotNull] public ToolPoint this[int index] { get => this._pts[index]; set => this._pts[index] = value; }
        public int IndexOf(ToolPoint item) => this._pts.IndexOf(item);
        public void Insert(int index, ToolPoint item)
        {
            if (item != null) { this._pts.Insert(index, item); }
        }
        public void InsertRange(int index, [NotNull] IEnumerable<ToolPoint> items) => this._pts.InsertRange(index, items.Where(x => x != null));
        public void RemoveAt(int index) => this._pts.RemoveAt(index);
        public void Add(ToolPoint item)
        {
            if (item != null) { this._pts.Add(item); }
        }
        [PublicAPI] public void Add(Point3d item) => this._pts.Add(new ToolPoint(item));
        public void AddRange([NotNull] IEnumerable<ToolPoint> items) => this._pts.AddRange(items.Where(x => x != null));
        public void AddRange([NotNull] IEnumerable<Point3d> items)
        {
            foreach (Point3d pt in items) { Add(pt); }
        }
        public void Clear() => this._pts.Clear();
        public bool Contains(ToolPoint item) => this._pts.Contains(item);
        public void CopyTo(ToolPoint[] array, int arrayIndex) => this._pts.CopyTo(array, arrayIndex);
        public bool Remove(ToolPoint item) => this._pts.Remove(item);
        public IEnumerator<ToolPoint> GetEnumerator() => this._pts.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this._pts.GetEnumerator();

        #endregion
    }

    // Grasshopper Type Wrapper
    public sealed class GH_ToolPath : CAMel_Goo<ToolPath>, IGH_PreviewData
    {
        // Default Constructor
        [UsedImplicitly] public GH_ToolPath() { this.Value = new ToolPath(); }
        // Create from unwrapped version
        public GH_ToolPath([CanBeNull] ToolPath tP) { this.Value = tP; }
        // Copy Constructor
        public GH_ToolPath([CanBeNull] GH_ToolPath tP) { this.Value = tP?.Value?.deepClone(); }
        // Duplicate
        [NotNull] public override IGH_Goo Duplicate() => new GH_ToolPath(this);

        public override bool CastTo<T>(ref T target)
        {
            if (this.Value == null) { return false; }
            // Cast from unwrapped ToolPath
            if (typeof(T).IsAssignableFrom(typeof(ToolPath)))
            {
                target = (T) (object) this.Value;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(MachineOperation)))
            {
                target = (T) (object) new MachineOperation(this.Value);
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_MachineOperation)))
            {
                target = (T) (object) new GH_MachineOperation(new MachineOperation(this.Value));
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(Curve)))
            {
                target = (T) (object) this.Value.getLine();
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_Curve)))
            {
                target = (T) (object) new GH_Curve(this.Value.getLine());
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(IMaterialForm)))
            {
                target = (T) this.Value.matForm;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_MaterialForm)))
            {
                target = (T) (object) new GH_MaterialForm(this.Value.matForm);
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(MaterialTool)))
            {
                target = (T) (object) this.Value.matTool;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_MaterialTool)))
            {
                target = (T) (object) new GH_MaterialTool(this.Value.matTool);
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(ToolPathAdditions)))
            {
                target = (T) (object) this.Value.additions;
                return true;
            }
            // ReSharper disable once InvertIf
            if (typeof(T).IsAssignableFrom(typeof(GH_ToolPathAdditions)))
            {
                target = (T) (object) new GH_ToolPathAdditions(this.Value.additions);
                return true;
            }
            return false;
        }

        public override bool CastFrom([CanBeNull] object source)
        {
            switch (source) {
                case null: return false;
                case ToolPath sTP:
                    this.Value = sTP;
                    return true;
                case Curve curve: {
                    if (!curve.TryGetPolyline(out Polyline pl)) { return false; }
                    ToolPath tP = new ToolPath();
                    tP.AddRange(pl);
                    this.Value = tP;
                    return true;
                }
                case GH_Curve ghCurve: {
                    if (ghCurve.Value == null || !ghCurve.Value.TryGetPolyline(out Polyline pl)) { return false; }
                    ToolPath tP = new ToolPath();
                    tP.AddRange(pl);
                    this.Value = tP;
                    return true;
                }
                default: return false;
            }
        }

        public BoundingBox ClippingBox => this.Value?.getBoundingBox() ?? BoundingBox.Unset;

        public void DrawViewportWires([CanBeNull] GH_PreviewWireArgs args)
        {
            if (this.Value == null || args?.Pipeline == null) { return; }
            args.Pipeline.DrawCurve(this.Value.getLine(), args.Color);
            args.Pipeline.DrawArrows(this.Value.toolLines(), args.Color);
        }
        public void DrawViewportMeshes([CanBeNull] GH_PreviewMeshArgs args) { }
    }

    // Grasshopper Parameter Wrapper
    public class GH_ToolPathPar : GH_Param<GH_ToolPath>, IGH_PreviewObject
    {
        public GH_ToolPathPar() :
            base("ToolPath", "ToolPath", "Contains a collection of Tool Paths", "CAMel", "  Params", GH_ParamAccess.item) { }

        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("4ea6da38-c19f-43e7-85d4-ada4716c06ac");

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
        protected override System.Drawing.Bitmap Icon => Properties.Resources.toolpath;
    }
}