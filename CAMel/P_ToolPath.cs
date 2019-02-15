using System;
using System.Collections;
using System.Collections.Generic;

using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using CAMel.Types.MaterialForm;
using CAMel.Types.Machine;
using static CAMel.Exceptions;

namespace CAMel.Types
{

    // One action of the machine, such as cutting a line
    public class ToolPath : IList<ToolPoint> ,IToolPointContainer
    {
        private List<ToolPoint> _pts;     // Positions of the machine
        public MaterialTool matTool { get; set; }   // Material and tool to cut it with
        public IMaterialForm matForm { get; set; }    // Shape of the material
        public ToolPathAdditions additions;       // Features we might add to the path

        public ToolPoint firstP
        {
            get
            {
                if (this.Count > 0) { return this[0]; }
                else { return null; }
            }
        }
        public ToolPoint lastP
        {
            get
            {
                if (this.Count > 0) { return this[this.Count-1]; }
                else { return null; }
            }
        }
        // Default Constructor, set everything to empty
        public ToolPath()
        {
            this.name = string.Empty;
            this._pts = new List<ToolPoint>();
            this.matTool = null;
            this.matForm = null;
            this.additions = new ToolPathAdditions();
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Just a MaterialTool
        public ToolPath(MaterialTool mT)
        {
            this.name = string.Empty;
            this._pts = new List<ToolPoint>();
            this.matTool = mT;
            this.matForm = null;
            this.additions = null;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Just a MaterialForm
        public ToolPath(IMaterialForm mf)
        {
            this.name = string.Empty;
            this._pts = new List<ToolPoint>();
            this.matTool = null;
            this.matForm = mf;
            this.additions = null;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // MaterialTool and Form
        public ToolPath(string name, MaterialTool mT, IMaterialForm mF)
        {
            this.name = name;
            this._pts = new List<ToolPoint>();
            this.matTool = mT;
            this.matForm = mF;
            this.additions = new ToolPathAdditions();
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // MaterialTool, Form and features
        public ToolPath(string name, MaterialTool mT, IMaterialForm mF, ToolPathAdditions tpa)
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
        private ToolPath(ToolPath tP)
        {
            this.name = string.Copy(tP.name);
            this._pts = new List<ToolPoint>();
            foreach (ToolPoint pt in tP) { Add(pt.deepClone()); }
            this.matTool = tP.matTool;
            this.matForm = tP.matForm;
            this.preCode = string.Copy(tP.preCode);
            this.postCode = string.Copy(tP.postCode);
            this.additions = tP.additions.deepClone();
        }

        public ToolPath deepClone() => new ToolPath(this);
        // create a lifted path
        public ToolPath deepClone(double h, IMachine m)
        {
            if(Math.Abs(h) < CAMel_Goo.tolerance) { return deepClone(); }
            ToolPath tP = deepCloneWithNewPoints(new List<ToolPoint>());
            foreach (ToolPoint tPt in this)
            {
                ToolPoint newTPt = tPt.deepClone();
                newTPt.pt = newTPt.pt + h * m.toolDir(tPt);
                tP.Add(newTPt);
            }
            return tP;
        }

        public ToolPath deepCloneWithNewPoints(List<ToolPoint> pts)
        {
            ToolPath newTP = new ToolPath()
            {
                name = string.Copy(this.name),
                matTool = this.matTool,
                matForm = this.matForm,
                preCode = string.Copy(this.preCode),
                postCode = string.Copy(this.postCode),
                additions = this.additions.deepClone(),
                _pts = pts
            };
            return newTP;
        }
        // Copy in features from the valid ToolPath if this does not yet have its own.
        public void validate(ToolPath valid, IMachine m)
        {
            this.matTool = this.matTool ?? valid.matTool;
            this.matForm = this.matForm ?? valid.matForm;
            this.additions = this.additions ?? m.defaultTPA;
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
        public List<List<ToolPath>> processAdditions(IMachine m, out List<ToolPath> fP)
        {
            if (this.matTool == null) { matToolException(); fP = null; return null; }
            if (this.matForm == null) { matFormException(); fP = null; return null; }

            // adjust path for three axis (or index three axis)
            ToolPath useTP;
            if (this.additions.threeAxisHeightOffset) { useTP = m.threeAxisHeightOffset(this); }
            else { useTP = this; }

            // add steps into material
            var roughPaths = new List<List<ToolPath>>();
            if(useTP.additions.stepDown) { roughPaths = m.stepDown(useTP); }

            // add finishing paths, processing onion

            fP = m.finishPaths(useTP);

            // add insert and retract moves

            for (int i = 0; i < roughPaths.Count; i++)
                {
                    for (int j = 0; j < roughPaths[i].Count; j++)
                    { roughPaths[i][j] = m.insertRetract(roughPaths[i][j]); }
                }
            for (int i = 0; i < fP.Count; i++) { fP[i] = m.insertRetract(fP[i]); }

            return roughPaths;
        }

        // Use a curve and direction vector to create a path of toolpoints
        public bool convertCurve(Curve c, Vector3d d)
        {
            if (this.matTool == null) { matToolException(); return false; }

            // Create polyline approximation
            Polyline pL;

            // Check we are dealing with a valid curve.

            if (c != null && c.IsValid)
            {
                Curve c2 = c.ToPolyline(0, 0, Math.PI, 0, 0, this.matTool.tolerance, this.matTool.minStep, 20.0 * this.matTool.toolWidth, true);
                c2.TryGetPolyline(out pL);
            }
            else { return false; }


            this._pts = new List<ToolPoint>();

            // Add the points to the Path

            foreach (Point3d pt in pL)
            {
                ToolPoint tPt = new ToolPoint(pt, d);
                Add(tPt);
            }
            return true;
        }
        private const double _accTol = 0.0001;
        public static PolylineCurve convertAccurate(Curve c)
        {
            // Check if already a polyline, otherwise make one
            PolylineCurve plC = c.TryGetPolyline(out Polyline p) ? new PolylineCurve(p) :
                c.ToPolyline(0, 0, Math.PI, 0, 0, _accTol*5.0, 0, 0, true);

            return plC;
        }

        internal static ToolPath toPath(object scraps)
        {
            ToolPath oP = new ToolPath();
            if (scraps is IToolPointContainer) { oP.AddRange(((IToolPointContainer)scraps).getSinglePath()); }
            else if (scraps is Point3d) { oP.Add((Point3d)scraps); }
            else if (scraps is IEnumerable)
            {
                foreach (object oB in scraps as IEnumerable)
                {
                    if (oB is IToolPointContainer) { oP.AddRange(((IToolPointContainer)oB).getSinglePath()); }
                    if (oB is Point3d) { oP.Add((Point3d)oB); }
                }
            }
            return oP;
        }

        #region Point extraction and previews
        public ToolPath getSinglePath() => deepClone();
        // Get the list of tooltip locations
        public List<Point3d> getPoints()
        {
            List<Point3d> points = new List<Point3d>();

            foreach (ToolPoint tP in this) { points.Add(tP.pt); }

            return points;
        }
        // Get the list of tool directions
        public List<Vector3d> getDirs()
        {
            List<Vector3d> dirs = new List<Vector3d>();

            foreach (ToolPoint tP in this) { dirs.Add(tP.dir); }
            return dirs;
        }
        // Create a path with the points
        public List<Point3d> getPointsandDirs(out List<Vector3d> dirs)
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
        public List<Vector3d> getSpeedFeed()
        {
            List<Vector3d> sF = new List<Vector3d>();

            foreach (ToolPoint tP in this) { sF.Add(new Vector3d(tP.speed, tP.feed, 0)); }
            return sF;
        }

        // Bounding Box for previews
        public BoundingBox getBoundingBox()
        {
            BoundingBox bb = BoundingBox.Unset;
            for (int i = 0; i < this.Count; i++)
            { bb.Union(this[i].getBoundingBox()); }
            return bb;
        }
        // Create a polyline
        public PolylineCurve getLine() => new PolylineCurve(getPoints());

        // Lines for each toolpoint
        public List<Line> toolLines()
        {
            List<Line> lines = new List<Line>();
            foreach (ToolPoint tP in this) { lines.Add(tP.toolLine()); }
            return lines;
        }
        #endregion

        #region List Functions
        public int Count => ((IList<ToolPoint>)this._pts).Count;
        public bool IsReadOnly => ((IList<ToolPoint>)this._pts).IsReadOnly;
        public ToolPoint this[int index] { get => ((IList<ToolPoint>)this._pts)[index]; set => ((IList<ToolPoint>)this._pts)[index] = value; }
        public int IndexOf(ToolPoint item) { return ((IList<ToolPoint>)this._pts).IndexOf(item); }
        public void Insert(int index, ToolPoint item) { ((IList<ToolPoint>)this._pts).Insert(index, item); }
        public void InsertRange(int index, IEnumerable<ToolPoint> items) { this._pts.InsertRange(index, items); }
        public void RemoveAt(int index) { ((IList<ToolPoint>)this._pts).RemoveAt(index); }
        public void Add(ToolPoint item) { ((IList<ToolPoint>)this._pts).Add(item); }
        public void Add(Point3d item) { ((IList<ToolPoint>)this._pts).Add(new ToolPoint(item)); }
        public void AddRange(IEnumerable<ToolPoint> items) { this._pts.AddRange(items); }
        public void AddRange(IEnumerable<Point3d> items)
        { foreach(Point3d pt in items) { Add(pt); } }
        public void Clear() { ((IList<ToolPoint>)this._pts).Clear(); }
        public bool Contains(ToolPoint item) { return ((IList<ToolPoint>)this._pts).Contains(item); }
        public void CopyTo(ToolPoint[] array, int arrayIndex) { ((IList<ToolPoint>)this._pts).CopyTo(array, arrayIndex); }
        public bool Remove(ToolPoint item) { return ((IList<ToolPoint>)this._pts).Remove(item); }
        public IEnumerator<ToolPoint> GetEnumerator() { return ((IList<ToolPoint>)this._pts).GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return ((IList<ToolPoint>)this._pts).GetEnumerator(); }
        #endregion
    }

    // Grasshopper Type Wrapper
    public sealed class GH_ToolPath : CAMel_Goo<ToolPath>, IGH_PreviewData
    {
        public BoundingBox ClippingBox => this.Value.getBoundingBox();

        // Default Constructor
        public GH_ToolPath() { this.Value = new ToolPath(); }
        // Create from unwrapped version
        public GH_ToolPath(ToolPath tP) { this.Value = tP; }
        // Copy Constructor
        public GH_ToolPath(GH_ToolPath tP) { this.Value = tP.Value.deepClone(); }
        // Duplicate
        public override IGH_Goo Duplicate() { return new GH_ToolPath(this); }

        public override bool CastTo<T>(ref T target)
        {
            // Cast from unwrapped ToolPath
            if (typeof(T).IsAssignableFrom(typeof(ToolPath)))
            {
                target = (T)(object)this.Value;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(MachineOperation)))
            {
                target = (T)(object)new MachineOperation(this.Value);
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_MachineOperation)))
            {
                target = (T)(object)new GH_MachineOperation(new MachineOperation(this.Value));
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
            if (typeof(T).IsAssignableFrom(typeof(IMaterialForm)))
            {
                target = (T)this.Value.matForm;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_MaterialForm)))
            {
                target = (T)(object)new GH_MaterialForm(this.Value.matForm);
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(MaterialTool)))
            {
                target = (T)(object)this.Value.matTool;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_MaterialTool)))
            {
                target = (T)(object)new GH_MaterialTool(this.Value.matTool);
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(ToolPathAdditions)))
            {
                target = (T)(object)this.Value.additions;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_ToolPathAdditions)))
            {
                target = (T)(object)new GH_ToolPathAdditions(this.Value.additions);
                return true;
            }
            return false;
        }

        public override bool CastFrom(object source)
        {
            if (source == null) { return false; }
            //Cast from unwrapped ToolPath
            if (typeof(ToolPath).IsAssignableFrom(source.GetType()))
            {
                this.Value = (ToolPath)source;
                return true;
            }
            if (typeof(Curve).IsAssignableFrom(source.GetType()))
            {
                ToolPath tP = new ToolPath();
                if (((Curve)source).TryGetPolyline(out Polyline pl))
                {
                    tP.AddRange(pl);
                    this.Value = tP;
                    return true;
                }
                return false;
            }
            if (typeof(GH_Curve).IsAssignableFrom(source.GetType()))
            {
                ToolPath tP = new ToolPath();
                if (((GH_Curve)source).Value.TryGetPolyline(out Polyline pl))
                {
                    tP.AddRange(pl);
                    this.Value = tP;
                    return true;
                }
                return false;
            }

            return false;
        }

        public void DrawViewportWires(GH_PreviewWireArgs args)
        {
            args.Pipeline.DrawCurve(this.Value.getLine(), args.Color);
            args.Pipeline.DrawArrows(this.Value.toolLines(), args.Color);
        }
        public void DrawViewportMeshes(GH_PreviewMeshArgs args) { }
    }

    // Grasshopper Parameter Wrapper
    public class GH_ToolPathPar : GH_Param<GH_ToolPath>, IGH_PreviewObject
    {
        public GH_ToolPathPar() :
            base("ToolPath", "ToolPath", "Contains a collection of Tool Paths", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid
        {
            get { return new Guid("4ea6da38-c19f-43e7-85d4-ada4716c06ac"); }
        }

        public bool Hidden { get; set; }
        public bool IsPreviewCapable => true;
        public BoundingBox ClippingBox => Preview_ComputeClippingBox();
        public void DrawViewportWires(IGH_PreviewArgs args) => Preview_DrawWires(args);
        public void DrawViewportMeshes(IGH_PreviewArgs args) => Preview_DrawMeshes(args);

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Properties.Resources.toolpath;
            }
        }

    }
}