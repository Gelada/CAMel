using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
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
        [NotNull] public List<string> error { get; }
        [NotNull] public List<string> warning { get; }
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
        {
            if(err!=null) { this.error.Add(err); }
        }

        public void addWarning([CanBeNull] string warn)
        {
            if (warn != null) { this.warning.Add(warn);  }
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
    public class GH_ToolPointPar : GH_Param<GH_ToolPoint>, IGH_PreviewObject
    {
        public GH_ToolPointPar() :
            base("ToolPoint", "ToolPt", "Contains a collection of Tool Points", "CAMel", "  Params", GH_ParamAccess.item) { }
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
    }
}