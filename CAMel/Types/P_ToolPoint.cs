using System;
using System.Collections.Generic;
using System.ComponentModel;
using GH_IO.Serialization;
using GH_IO.Types;
using Grasshopper.Getters;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Utility;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.Types
{
    // One position of the machine
    public class ToolPoint : IToolPointContainer
    {
        public Point3d pt { get; set; }      // Tool Tip position
        private Vector3d _dir;
        public Vector3d dir     // Tool Direction (away from position)
        {
            get => this._dir;
            set
            {
                this._dir = value;
                this._dir.Unitize();
            }
        }

        [NotNull] public ToolPoint firstP => this;
        [NotNull] public ToolPoint lastP => this;

        public double speed { get; set; }    // Considered unset for negative values
        public double feed { get; set; }     // Considered unset for negative values
        [NotNull] private List<string> error { get; }
        [NotNull] private List<string> warning { get; }
        public string name { get; set; }
        public string preCode { get; set; }
        public string postCode { get; set; }

        // Adding anything here needs significant support:
        //  Add serialization and deserialization
        //  Add to the proxy editor
        //  Add to Constructors

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
        public ToolPoint(Point3d pt, Vector3d d, [CanBeNull] string preCode, [CanBeNull] string postCode, double speed, double feed)
        {
            this.pt = pt;
            this.dir = d;
            this.preCode = preCode ?? string.Empty;
            this.postCode = postCode ?? string.Empty;
            this.speed = speed;
            this.feed = feed;
            this.error = new List<string>();
            this.warning = new List<string>();
            this.name = string.Empty;
        }
        // Copy Constructor
        private ToolPoint([NotNull] ToolPoint tP)
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

        [NotNull]
        public ToolPoint deepClone() => new ToolPoint(this);

        [PublicAPI]
        public void addError([CanBeNull] string err)
        { if(err!=null) { this.error.Add(err); } }

        public void addWarning([CanBeNull] string warn)
        { if (warn != null) { this.warning.Add(warn);  } }

        public void writeErrorAndWarnings([NotNull] ref CodeInfo co)
        {
            foreach (string err in this.error) { co.addError(err); }
            foreach (string warn in this.warning) { co.addWarning(warn); }
        }

        public string TypeDescription => "Information about a position of the machine";
        public string TypeName => "ToolPoint";

        public override string ToString()
        {
            string outP = this.name;
            if(outP != string.Empty) { outP = outP + " "; }
            outP = outP + "Pt: (" +
                this.pt.X.ToString("0.000") + ", " + this.pt.Y.ToString("0.000") + ", " + this.pt.Z.ToString("0.000") +
                ") Dir: (" +
                this.dir.X.ToString("0.000") + ", " + this.dir.Y.ToString("0.000") + ", " + this.dir.Z.ToString("0.000") +
                ")";
            return outP;
        }

        private const double _PreviewLength = 0.2;
        internal Line toolLine() => new Line(this.pt, this.pt + this.dir* _PreviewLength);
        public ToolPath getSinglePath() => new ToolPath { this };

        public BoundingBox getBoundingBox() => new BoundingBox(new List<Point3d> { this.pt, this.pt + this.dir * _PreviewLength });

    }

    // Grasshopper Type Wrapper
    public sealed class GH_ToolPoint : CAMel_Goo<ToolPoint>, IGH_PreviewData
    {
        // Default Constructor, set up at the origin with direction set to 0 vector.
        [UsedImplicitly]
        public GH_ToolPoint() { this.Value = new ToolPoint(); }
        // Create from unwrapped version
        public GH_ToolPoint([CanBeNull] ToolPoint tP) { this.Value = tP?.deepClone(); }
        // Copy Constructor
        public GH_ToolPoint([CanBeNull] GH_ToolPoint tP) { this.Value = tP?.Value?.deepClone(); }
        // Duplicate
        [NotNull]
        public override IGH_Goo Duplicate() {return new GH_ToolPoint(this); }

        [NotNull]
        public override IGH_GooProxy EmitProxy() => new GH_ToolPointProxy(this);

        public override bool Write([CanBeNull] GH_IWriter writer)
        {
            if (this.Value == null || writer == null) { return base.Write(writer); }

            writer.SetString("name", this.Value.name);
            Point3d pt = this.Value.pt;
            writer.SetPoint3D("pt",new GH_Point3D(pt.X,pt.Y,pt.Z));
            Vector3d dir = this.Value.dir;
            writer.SetPoint3D("dir", new GH_Point3D(dir.X, dir.Y, dir.Z));
            writer.SetDouble("speed",this.Value.speed);
            writer.SetDouble("feed", this.Value.feed);
            writer.SetString("preCode", this.Value.preCode);
            writer.SetString("postCode", this.Value.postCode);

            return base.Write(writer);
        }

        // Deserialize this instance from a Grasshopper reader object.
        public override bool Read([CanBeNull] GH_IReader reader)
        {
            if (reader == null) { return false; }
            try
            {
                ToolPoint tPt = new ToolPoint();
                if (reader.ItemExists("name")) { tPt.name = reader.GetString("name") ?? string.Empty;}
                if (reader.ItemExists("pt"))
                {
                    GH_Point3D pt = reader.GetPoint3D("pt");
                    tPt.pt = new Point3d(pt.x,pt.y,pt.z);
                }
                if (reader.ItemExists("dir"))
                {
                    GH_Point3D pt = reader.GetPoint3D("dir");
                    tPt.dir = new Vector3d(pt.x, pt.y, pt.z);
                }
                if (reader.ItemExists("feed")) { tPt.feed = reader.GetDouble("feed"); }
                if (reader.ItemExists("speed")) { tPt.speed = reader.GetDouble("speed"); }
                if (reader.ItemExists("preCode")) { tPt.name = reader.GetString("preCode") ?? string.Empty; }
                if (reader.ItemExists("postCode")) { tPt.name = reader.GetString("postCode") ?? string.Empty; }

                this.Value = tPt;
                return base.Read(reader);
            }
            catch (Exception ex) when (ex is OverflowException || ex is InvalidCastException || ex is NullReferenceException)
            {
                return false;
            }
        }

        public override bool CastTo<T>(ref T target)
        {
            if (this.Value == null) { return false;}
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
            // ReSharper disable once InvertIf
            if (typeof(T).IsAssignableFrom(typeof(GH_Vector)))
            {
                object ptr =new GH_Vector(this.Value.dir);
                target = (T)ptr;
                return true;
            }
            return false;
        }

        public override bool CastFrom([CanBeNull] object source)
        {
            switch (source) {
                case null: return false;
                case GH_Point pointGoo:
                    this.Value = new ToolPoint(pointGoo.Value);
                    return true;
            }

            Point3d pt = Point3d.Unset;
            if (!GH_Convert.ToPoint3d(source, ref pt, GH_Conversion.Both)) { return false; }
            this.Value = new ToolPoint(pt);
            return true;
        }

        public BoundingBox ClippingBox => this.Value?.toolLine().BoundingBox ?? BoundingBox.Unset;

        public void DrawViewportWires([CanBeNull] GH_PreviewWireArgs args)
        {
            if (this.Value == null || args?.Pipeline == null) { return; }
            args.Pipeline.DrawPoint(this.Value.pt, args.Color);
            args.Pipeline.DrawArrow(this.Value.toolLine(), args.Color);
        }
        public void DrawViewportMeshes([CanBeNull] GH_PreviewMeshArgs args) { }
    }

    // Grasshopper Parameter Wrapper
    public class GH_ToolPointPar : GH_PersistentParam<GH_ToolPoint>, IGH_PreviewObject
    {
        public GH_ToolPointPar() :
            base("ToolPoint", "ToolPt", "Contains a collection of Tool Points", "CAMel", "  Params") { }
        public override Guid ComponentGuid => new Guid("0bbed7c1-88a9-4d61-b7cb-e0dfe82b1b86");

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
        protected override System.Drawing.Bitmap Icon => Properties.Resources.toolpoint;

        protected override GH_GetterResult Prompt_Plural(ref List<GH_ToolPoint> values)
        {
            return GH_GetterResult.success;
        }

        // ReSharper disable once RedundantAssignment
        protected override GH_GetterResult Prompt_Singular([CanBeNull] ref GH_ToolPoint value)
        {
            Rhino.Input.RhinoGet.GetPoint("Tooltip Position", true, out Point3d point);
            Vector3d dir = GH_VectorGetter.GetVector()?.Value ?? Vector3d.ZAxis;
            value = new GH_ToolPoint(new ToolPoint(point,dir));
            return GH_GetterResult.success;
        }
    }


    public class GH_ToolPointProxy : GH_GooProxy<GH_ToolPoint>
    {
        public GH_ToolPointProxy([CanBeNull] GH_ToolPoint obj) : base(obj)
        { }

        [CanBeNull]
        [Category(" General"), Description("Optional Name attached to point."), DisplayName(" Name"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
        public string name
        {
            get => this.Owner?.Value?.name ?? string.Empty;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
                this.Owner.Value.name = value ?? string.Empty;
            }
        }

        [CanBeNull]
        [Category(" General"), Description("Position of tool tip."), DisplayName("Point"),
         RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
        public GH_Point3d_Wrapper pt
        {
            get
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
                Point3d rPt = this.Owner.Value.pt;
                GH_Point3d_Wrapper ghPoint3DWrapper = new GH_Point3d_Wrapper(ref rPt, pointChanged);
                if (this.Owner == null) { throw new NullReferenceException(); }
                this.Owner.Value.pt = rPt;
                return ghPoint3DWrapper;
            }
        }
        private void pointChanged([CanBeNull] GH_Point3d_Wrapper sender, Point3d point)
        {
            if (this.Owner == null) { throw new NullReferenceException(); }
            if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
            this.Owner.Value.pt = point;
        }
        [CanBeNull]
        [Category(" General"), Description("Direction of tool (for rotary and 5-axis) (from tip down shaft)."), DisplayName("Direction"),
         RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
        public GH_Vector3d_Wrapper dir
        {
            get
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
                Vector3d rDir = this.Owner.Value.dir;
                GH_Vector3d_Wrapper ghVector3DWrapper = new GH_Vector3d_Wrapper(ref rDir, dirChanged);
                if (this.Owner == null) { throw new NullReferenceException(); }
                this.Owner.Value.dir = rDir;
                return ghVector3DWrapper;
            }
        }
        private void dirChanged([CanBeNull] GH_Vector3d_Wrapper sender, Vector3d rDir)
        {
            if (this.Owner == null) { throw new NullReferenceException(); }
            if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
            this.Owner.Value.dir = rDir;
        }
        [Category(" Settings"), Description("Spindle Speed (some machines will ignore)"), DisplayName("Speed"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
        public double speed
        {
            get => this.Owner?.Value?.speed ?? -1;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
                this.Owner.Value.speed = value;
            }
        }
        [Category(" Settings"), Description("Feed Rate"), DisplayName("Feed"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
        public double feed
        {
            get => this.Owner?.Value?.feed ?? -1;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
                this.Owner.Value.feed = value;
            }
        }
        [CanBeNull]
        [Category("Code"), Description("Code to run before point"), DisplayName("preCode"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
        public string preCode
        {
            get => this.Owner?.Value?.preCode ?? string.Empty;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
                this.Owner.Value.preCode = value ?? string.Empty;
            }
        }
        [CanBeNull]
        [Category("Code"), Description("Code to run after point"), DisplayName("postCode"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
        public string postCode
        {
            get => this.Owner?.Value?.postCode ?? string.Empty;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
                this.Owner.Value.postCode = value ?? string.Empty;
            }
        }
    }

}