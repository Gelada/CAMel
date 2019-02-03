using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
        private List<ToolPoint> Pts;     // Positions of the machine
        public MaterialTool matTool { get; set; }   // Material and tool to cut it with
        public IMaterialForm matForm { get; set; }    // Shape of the material
        public ToolPathAdditions Additions;       // Features we might add to the path 

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
            this.Pts = new List<ToolPoint>();
            this.matTool = null;
            this.matForm = null;
            this.Additions = new ToolPathAdditions();
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Just a MaterialTool
        public ToolPath(MaterialTool MT)
        {
            this.name = string.Empty;
            this.Pts = new List<ToolPoint>();
            this.matTool = MT;
            this.matForm = null;
            this.Additions = null;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Just a MaterialForm
        public ToolPath(IMaterialForm MF)
        {
            this.name = string.Empty;
            this.Pts = new List<ToolPoint>();
            this.matTool = null;
            this.matForm = MF;
            this.Additions = null;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // MaterialTool and Form
        public ToolPath(string name, MaterialTool MT, IMaterialForm MF)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.matTool = MT;
            this.matForm = MF;
            this.Additions = new ToolPathAdditions();
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // MaterialTool, Form and features
        public ToolPath(string name, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.matTool = MT; 
            this.matForm = MF;
            this.Additions = TPA;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Copy Constructor
        private ToolPath(ToolPath TP)
        {
            this.name = string.Copy(TP.name);
            this.Pts = new List<ToolPoint>();
            foreach (ToolPoint pt in TP) { this.Add(pt.deepClone()); }
            this.matTool = TP.matTool;
            this.matForm = TP.matForm;
            this.preCode = string.Copy(TP.preCode);
            this.postCode = string.Copy(TP.postCode);
            this.Additions = TP.Additions.deepClone();
        }

        public ToolPath deepClone() => new ToolPath(this);
        // create a lifted path
        public ToolPath deepClone(double h, IMachine M)
        {
            ToolPath tP = deepCloneWithNewPoints(new List<ToolPoint>());
            foreach (ToolPoint tPt in this)
            {
                ToolPoint newTPt = tPt.deepClone();
                newTPt.pt = newTPt.pt + h * M.toolDir(tPt);
                tP.Add(newTPt);
            }
            return tP;
        }

        public ToolPath deepCloneWithNewPoints(List<ToolPoint> Pts)
        {
            ToolPath newTP = new ToolPath()
            {
                name = string.Copy(this.name),
                matTool = this.matTool,
                matForm = this.matForm,
                preCode = string.Copy(this.preCode),
                postCode = string.Copy(this.postCode),
                Additions = this.Additions.deepClone(),
                Pts = Pts
            };
            return newTP;
        }
        // Copy in features from the valid ToolPath if this does not yet have its own. 
        public void validate(ToolPath valid, IMachine M)
        {
            this.matTool = this.matTool ?? valid.matTool;
            this.matForm = this.matForm ?? valid.matForm;
            this.Additions = this.Additions ?? M.defaultTPA;
        }

        public string TypeDescription => "An action of the machine, for example cutting a single line";
        public string TypeName => "ToolPath"; 

        public string name { get; set; }

        public string preCode { get; set; }
        public string postCode { get; set; }

        public override string ToString() => "Toolpath: " + this.name + " with " + this.Count + " points.";

        // Main functions

        // Process any additions to the path and return 
        // list of list of toolpaths (for stepdown)
        public List<List<ToolPath>> processAdditions(IMachine M, out List<ToolPath> fP)
        {
            if (this.matTool == null) { matToolException(); }
            if (this.matForm == null) { matFormException(); }

            // adjust path for three axis (or index three axis)
            ToolPath useTP;
            if (this.Additions.threeAxisHeightOffset) { useTP = M.threeAxisHeightOffset(this); }
            else { useTP = this; }

            // add steps into material
            var roughPaths = new List<List<ToolPath>>();
            if(useTP.Additions.stepDown) { roughPaths = M.stepDown(useTP); }

            // add copies of the original path, making sure step down is false
            // Use levels given by the property onion in the ToolPathAdditions

            fP = M.finishPaths(useTP);

            // add insert and retract moves

            for (int i = 0; i < roughPaths.Count; i++)
                {
                    for (int j = 0; j < roughPaths[i].Count; j++)
                    { roughPaths[i][j] = M.insertRetract(roughPaths[i][j]); }
                }
            for (int i = 0; i < fP.Count; i++) { fP[i] = M.insertRetract(fP[i]); }

            return roughPaths;
        }

        // Use a curve and direction vector to create a path of toolpoints
        public bool convertCurve(Curve c, Vector3d d)
        {
            if (this.matTool == null) { matToolException(); }
            // Create polyline approximation
            Polyline PL;
            ToolPoint TPt;

            // Check we are dealing with a valid curve.

            if (c != null && c.IsValid)
            {
                Curve c2 = c.ToPolyline(0, 0, Math.PI, 0, 0, this.matTool.tolerance, this.matTool.minStep, 20.0 * this.matTool.toolWidth, true);
                c2.TryGetPolyline(out PL);
            }
            else { return false; }


            this.Pts = new List<ToolPoint>();

            // Add the points to the Path

            foreach (Point3d Pt in PL)
            {
                TPt = new ToolPoint(Pt, d);
                this.Add(TPt);
            }
            return true;
        }
        private const double accTol = 0.0001;
        public static PolylineCurve convertAccurate(Curve C)
        {
            Polyline P;
            PolylineCurve PlC;
            // Check if already a polyline, otherwise make one
            if (C.TryGetPolyline(out P)) { PlC = new PolylineCurve(P); }
            else { PlC = C.ToPolyline(0, 0, Math.PI, 0, 0, accTol*5.0, 0, 0, true); }

            return PlC;
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
        public ToolPath getSinglePath() => this.deepClone();
        // Get the list of tooltip locations
        public List<Point3d> getPoints()
        {
            List<Point3d> Points = new List<Point3d>();

            foreach (ToolPoint tP in this) { Points.Add(tP.pt); }

            return Points;
        }
        // Get the list of tool directions
        public List<Vector3d> getDirs()
        {
            List<Vector3d> Dirs = new List<Vector3d>();

            foreach (ToolPoint tP in this) { Dirs.Add(tP.dir); }
            return Dirs;
        }
        // Create a path with the points 
        public List<Point3d> getPointsandDirs(out List<Vector3d> Dirs)
        {
            List<Point3d> Ptsout = new List<Point3d>();
            Dirs = new List<Vector3d>();
            foreach (ToolPoint P in this)
            {
                Ptsout.Add(P.pt);
                Dirs.Add(P.dir);
            }
            return Ptsout;
        }
        // Get the list of speeds and feeds (a vector with speed in X and feed in Y)
        public List<Vector3d> getSpeedFeed()
        {
            List<Vector3d> SF = new List<Vector3d>();

            foreach (ToolPoint tP in this) { SF.Add(new Vector3d(tP.speed, tP.feed, 0)); }
            return SF;
        }

        // Bounding Box for previews
        public BoundingBox getBoundingBox()
        {
            BoundingBox BB = BoundingBox.Unset;
            for (int i = 0; i < this.Count; i++)
            { BB.Union(this[i].getBoundingBox()); }
            return BB;
        }
        // Create a polyline
        public PolylineCurve getLine()
        {
            return new PolylineCurve(this.getPoints());
        }
        // Lines for each toolpoint
        public List<Line> toolLines()
        {
            List<Line> lines = new List<Line>();
            foreach (ToolPoint TP in this) { lines.Add(TP.toolLine()); }
            return lines;
        }
        #endregion

        #region List Functions
        public int Count => ((IList<ToolPoint>)this.Pts).Count;
        public bool IsReadOnly => ((IList<ToolPoint>)this.Pts).IsReadOnly;
        public ToolPoint this[int index] { get => ((IList<ToolPoint>)this.Pts)[index]; set => ((IList<ToolPoint>)this.Pts)[index] = value; }
        public int IndexOf(ToolPoint item) { return ((IList<ToolPoint>)this.Pts).IndexOf(item); }
        public void Insert(int index, ToolPoint item) { ((IList<ToolPoint>)this.Pts).Insert(index, item); }
        public void InsertRange(int index, IEnumerable<ToolPoint> items) { this.Pts.InsertRange(index, items); }
        public void RemoveAt(int index) { ((IList<ToolPoint>)this.Pts).RemoveAt(index); }
        public void Add(ToolPoint item) { ((IList<ToolPoint>)this.Pts).Add(item); }
        public void Add(Point3d item) { ((IList<ToolPoint>)this.Pts).Add(new ToolPoint(item)); }
        public void AddRange(IEnumerable<ToolPoint> items) { this.Pts.AddRange(items); }
        public void AddRange(IEnumerable<Point3d> items)
        { foreach(Point3d Pt in items) { this.Add(Pt); } }
        public void Clear() { ((IList<ToolPoint>)this.Pts).Clear(); }
        public bool Contains(ToolPoint item) { return ((IList<ToolPoint>)this.Pts).Contains(item); }
        public void CopyTo(ToolPoint[] array, int arrayIndex) { ((IList<ToolPoint>)this.Pts).CopyTo(array, arrayIndex); }
        public bool Remove(ToolPoint item) { return ((IList<ToolPoint>)this.Pts).Remove(item); }
        public IEnumerator<ToolPoint> GetEnumerator() { return ((IList<ToolPoint>)this.Pts).GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return ((IList<ToolPoint>)this.Pts).GetEnumerator(); }
        #endregion
    }

    // Grasshopper Type Wrapper
    public class GH_ToolPath : CAMel_Goo<ToolPath>, IGH_PreviewData
    {
        public BoundingBox ClippingBox => this.Value.getBoundingBox();

        // Default Constructor
        public GH_ToolPath() { this.Value = new ToolPath(); }
        // Create from unwrapped version
        public GH_ToolPath(ToolPath TP) { this.Value = TP; }
        // Copy Constructor
        public GH_ToolPath(GH_ToolPath TP) { this.Value = TP.Value.deepClone(); }
        // Duplicate
        public override IGH_Goo Duplicate() { return new GH_ToolPath(this); }

        public override bool CastTo<Q>(ref Q target)
        {
            // Cast from unwrapped ToolPath
            if (typeof(Q).IsAssignableFrom(typeof(ToolPath)))
            {
                target = (Q)(object)this.Value;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(MachineOperation)))
            {
                target = (Q)(object)new MachineOperation(this.Value);
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_MachineOperation)))
            {
                target = (Q)(object)new GH_MachineOperation(new MachineOperation(this.Value));
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
            if (typeof(Q).IsAssignableFrom(typeof(IMaterialForm)))
            {
                target = (Q)(object)this.Value.matForm;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_MaterialForm)))
            {
                target = (Q)(object)new GH_MaterialForm(this.Value.matForm);
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(MaterialTool)))
            {
                target = (Q)(object)this.Value.matTool;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_MaterialTool)))
            {
                target = (Q)(object)new GH_MaterialTool(this.Value.matTool);
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(ToolPathAdditions)))
            {
                target = (Q)(object)this.Value.Additions;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_ToolPathAdditions)))
            {
                target = (Q)(object)new GH_ToolPathAdditions(this.Value.Additions);
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
                ToolPath TP = new ToolPath();
                Polyline PL = null;
                if (((Curve)source).TryGetPolyline(out PL))
                {
                    TP.AddRange(PL);
                    this.Value = TP;
                    return true;
                }
                return false;
            }
            if (typeof(GH_Curve).IsAssignableFrom(source.GetType()))
            {
                ToolPath TP = new ToolPath();
                Polyline PL = null;
                if (((GH_Curve)source).Value.TryGetPolyline(out PL))
                {
                    TP.AddRange(PL);
                    this.Value = TP;
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
                return Properties.Resources.toolpath;
            }
        }
        
    }
}