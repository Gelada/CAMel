using System;
using System.Collections.Generic;

using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace CAMel.Types
{
    // One position of the machine
    public class ToolPoint : IToolPointContainer
    {
        public Point3d pt { get; set; }      // Tool Tip position
        private Vector3d _dir;
        public Vector3d dir     // Tool Direction (away from position)
        {
            get { return this._dir; }
            set
            {
                this._dir = value;
                this._dir.Unitize();
            }
        }

        public ToolPoint firstP { get => this; }
        public ToolPoint lastP { get => this; }

        public double speed { get; set; }    // Considered unset for negative values
        public double feed { get; set; }     // Considered unset for negative values
        public List<string> error { get; private set; }
        public List<string> warning { get; private set; }
        public string name { get; set; }
        public string preCode { get; set; }
        public string postCode { get; set; }

        // Default Constructor, set up at the origin with direction set to 0 vector.
        public ToolPoint()
        {
            this.pt = new Point3d(0, 0, 0);
            this.dir = new Vector3d(0, 0, 1);
            this.speed = -1;
            this.feed = -1;
            this.error = new List<string>();
            this.warning = new List<string>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Just a point, set direction to Z vector.
        public ToolPoint(Point3d pt)
        {
            this.pt = pt;
            this.dir = new Vector3d(0, 0, 1);
            this.speed = -1;
            this.feed = -1;
            this.error = new List<string>();
            this.warning = new List<string>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Use point and direction
        public ToolPoint(Point3d pt, Vector3d d)
        {
            this.pt = pt;
            this.dir = d;
            this.speed = -1;
            this.feed = -1;
            this.error = new List<string>();
            this.warning = new List<string>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Use point direction and extra Code
        public ToolPoint(Point3d pt, Vector3d d, string preCode, string postCode)
        {
            this.pt = pt;
            this.dir = d;
            this.preCode = preCode;
            this.postCode = postCode;
            this.speed = -1;
            this.feed = -1;
            this.error = new List<string>();
            this.warning = new List<string>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Use point direction and override speed and feed
        public ToolPoint(Point3d pt, Vector3d d, double speed, double feed)
        {
            this.pt = pt;
            this.dir = d;
            this.speed = speed;
            this.feed = feed;
            this.error = new List<string>();
            this.warning = new List<string>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Use point direction, override speed and feed and add extra Code
        public ToolPoint(Point3d pt, Vector3d d, string preCode, string postCode, double speed, double feed)
        {
            this.pt = pt;
            this.dir = d;
            this.preCode = preCode;
            this.postCode = postCode;
            this.speed = speed;
            this.feed = feed;
            this.error = new List<string>();
            this.warning = new List<string>();
            this.name = string.Empty;
        }
        // Copy Constructor
        private ToolPoint(ToolPoint tP)
        {
            this.pt = tP.pt;
            this.dir = tP.dir;
            this.preCode = string.Copy(tP.preCode);
            this.postCode = string.Copy(tP.postCode);
            this.speed = tP.speed;
            this.feed = tP.feed;
            this.name = string.Copy(tP.name);
            this.error = new List<string>();
            foreach( string s in tP.error) { this.error.Add(string.Copy(s)); }
            this.warning = new List<string>();
            foreach (string s in tP.warning) { this.warning.Add(string.Copy(s)); }
        }

        public ToolPoint deepClone() => new ToolPoint(this);

        public void addError(string err)
        {
            if(this.error == null) { this.error = new List<string>(); }
            this.error.Add(err);
        }

        public void addWarning(string warn)
        {
            if (this.warning == null) { this.warning = new List<string>(); }
            this.warning.Add(warn);
        }

        public string TypeDescription => "Information about a position of the machine"; 
        public string TypeName => "ToolPoint";

        public override string ToString()
        {
            string outp = this.name;
            if(outp != string.Empty) { outp = outp + " "; }
            outp = outp + "Pt: (" +
                this.pt.X.ToString("0.000") + ", " + this.pt.Y.ToString("0.000") + ", " + this.pt.Z.ToString("0.000") +
                ") Dir: (" +
                this.dir.X.ToString("0.000") + ", " + this.dir.Y.ToString("0.000") + ", " + this.dir.Z.ToString("0.000") +
                ")";
            return outp;
        }

        private const double _previewLength = 0.2;
        internal Line toolLine() => new Line(this.pt, this.pt + this.dir* _previewLength);
        public ToolPath getSinglePath() => new ToolPath() { this };

        public BoundingBox getBoundingBox() => new BoundingBox(new List<Point3d> { this.pt, this.pt + this.dir * _previewLength });

    }

    // Grasshopper Type Wrapper
    sealed public class GH_ToolPoint : CAMel_Goo<ToolPoint>, IGH_PreviewData
    {
        // Default Constructor, set up at the origin with direction set to 0 vector.
        public GH_ToolPoint() { this.Value = new ToolPoint(); }
        // Create from unwrapped version
        public GH_ToolPoint(ToolPoint tP) { this.Value = tP.deepClone(); }
        // Copy Constructor
        public GH_ToolPoint(GH_ToolPoint tP) { this.Value = tP.Value.deepClone(); }
        // Duplicate
        public override IGH_Goo Duplicate() {return new GH_ToolPoint(this); }
  
        public override bool CastTo<T>(ref T target)
        {
            if (typeof(T).IsAssignableFrom(typeof(ToolPoint)))
            {
                object ptr = this.Value;
                target = (T)ptr;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(Point3d)))
            {
                object ptr = this.Value.pt;
                target = (T)ptr;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_Point)))
            {
                object ptr = new GH_Point(this.Value.pt);
                target = (T)ptr;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(Vector3d)))
            {
                object ptr = this.Value.dir;
                target = (T)ptr;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_Vector)))
            {
                object ptr =new GH_Vector(this.Value.dir);
                target = (T)ptr;
                return true;
            }
            return false;
        }
        
        public override bool CastFrom(object source)
        {
            if (source == null) { return false; }

            if (source is Point3d)
            {
                this.Value = new ToolPoint((Point3d)source);
                return true;
            }
            if (source is GH_Point pointGoo)
            {
                this.Value = new ToolPoint(pointGoo.Value);
                return true;
            }
            Point3d pt = Point3d.Unset;
            if (GH_Convert.ToPoint3d(source, ref pt, GH_Conversion.Both))
            {
                this.Value = new ToolPoint(pt);
                return true;
            }
            return false;
        }

        public BoundingBox ClippingBox { get => this.Value.toolLine().BoundingBox; }
        public void DrawViewportWires(GH_PreviewWireArgs args)
        {
            args.Pipeline.DrawPoint(this.Value.pt, args.Color);
            args.Pipeline.DrawArrow(this.Value.toolLine(), args.Color);
        }
        public void DrawViewportMeshes(GH_PreviewMeshArgs args) { }
    }

    // Grasshopper Parameter Wrapper
    public class GH_ToolPointPar : GH_Param<GH_ToolPoint>, IGH_PreviewObject
    {
        public GH_ToolPointPar() :
            base("ToolPoint", "ToolPt", "Contains a collection of Tool Points", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid
        {
            get { return new Guid("0bbed7c1-88a9-4d61-b7cb-e0dfe82b1b86"); }
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
                return Properties.Resources.toolpoint;
            }
        }
    }
}