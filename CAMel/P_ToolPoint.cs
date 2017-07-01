using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace CAMel.Types
{
    // One position of the machine
    public class ToolPoint : IToolPointContainer
    {
        public Point3d Pt;      // Tool Tip position
        private Vector3d _Dir;
        public Vector3d Dir     // Tool Direction (away from position)
        {
            get { return this._Dir; }
            set 
            { 
                this._Dir = value;
                this._Dir.Unitize();
            }
        }
        public double speed;    // Considered unset for negative values
        public double feed;     // Considered unset for negative values
        public List<string> error;
        public List<string> warning;

        // Default Constructor, set up at the origin with direction set to 0 vector.
        public ToolPoint()
        {
            this.Pt = new Point3d(0, 0, 0);
            this.Dir = new Vector3d(0, 0, 0);
            this.speed = -1;
            this.feed = -1;
            this.error = new List<string>();
            this.warning = new List<string>();
        }
        // Just a point, set direction to 0 vector.
        public ToolPoint(Point3d Pt)
        {
            this.Pt = Pt;
            this.Dir = new Vector3d(0, 0, 0);
            this.speed = -1;
            this.feed = -1;
            this.error = new List<string>();
            this.warning = new List<string>();
        }
        // Use point and direction, normalise direction if not 0 vector.
        public ToolPoint(Point3d Pt, Vector3d D)
        {
            this.Pt = new Point3d(Pt);
            this.Dir = new Vector3d(D);
            if (this.Dir.Length > 0) this.Dir.Unitize();
            this.speed = -1;
            this.feed = -1;
            this.error = new List<string>();
            this.warning = new List<string>();
        }
        // Use point direction and extra Code, normalise direction if not 0 vector.
        public ToolPoint(Point3d Pt, Vector3d D, string Code)
        {
            this.Pt = new Point3d(Pt);
            this.Dir = new Vector3d(D);
            if (this.Dir.Length > 0) this.Dir.Unitize();
            this.localCode = Code;
            this.speed = -1;
            this.feed = -1;
            this.error = new List<string>();
            this.warning = new List<string>();
        }
        // Use point direction and override speed and feed, normalise direction if not 0 vector.
        public ToolPoint(Point3d Pt, Vector3d D, double speed, double feed)
        {
            this.Pt = new Point3d(Pt);
            this.Dir = new Vector3d(D);
            if (this.Dir.Length > 0) this.Dir.Unitize();
            this.speed = speed;
            this.feed = feed;
            this.error = new List<string>();
            this.warning = new List<string>();
        }
        // Use point direction extra Code and override speed and feed, normalise direction if not 0 vector.
        public ToolPoint(Point3d Pt, Vector3d D, string Code, double speed, double feed)
        {
            this.Pt = new Point3d(Pt);
            this.Dir = new Vector3d(D);
            if (this.Dir.Length > 0) this.Dir.Unitize();
            this.localCode = Code;
            this.speed = speed;
            this.feed = feed;
            this.error = new List<string>();
            this.warning = new List<string>();
        }
        // Copy Constructor
        public ToolPoint(ToolPoint TP)
        {
            this.Pt = new Point3d(TP.Pt);
            this.Dir = new Vector3d(TP.Dir);
            this.localCode = TP.localCode;
            this.speed = TP.speed;
            this.feed = TP.feed;
            this.name = TP.name;
            this.error = TP.error;
            this.warning = TP.warning;
        }

        public string TypeDescription
        {
            get { return "Information about a position of the machine"; }
        }

        public string TypeName
        {
            get { return "ToolPoint"; }
        }

        public string name
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public string localCode
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public bool IsValid
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override string ToString()
        {
            string outp = "";
            if (name != "")
                outp = outp + name;
            outp = outp + "Pt: " + this.Pt.ToString() + " Dir: " + this.Dir.ToString();
            if (localCode != "")
                outp = outp + "\n" + localCode;
            if (speed >= 0 || feed >= 0)
                outp = "\n" + outp + "Speed: " + this.speed.ToString() + " Feed: " + this.feed.ToString();
            return outp;
        }

        public ICAMel_Base Duplicate()
        {
            return new ToolPoint(this);
        }
    }

    // Grasshopper Type Wrapper
    public class GH_ToolPoint : GH_ToolPointContainer<ToolPoint>
    {
        // Default Constructor, set up at the origin with direction set to 0 vector.
        public GH_ToolPoint()
        {
            this.Value = new ToolPoint();
        }
        // Just a point, set direction to 0 vector.
        public GH_ToolPoint(Point3d Pt)
        {
            this.Value = new ToolPoint(Pt);
        }
        // Use point and direction, normalise direction if not 0 vector.
        public GH_ToolPoint(Point3d Pt, Vector3d D)
        {
            this.Value = new ToolPoint(Pt, D);
        }
        // Use point direction and extra Code, normalise direction if not 0 vector.
        public GH_ToolPoint(Point3d Pt, Vector3d D, string Code)
        {
            this.Value = new ToolPoint(Pt,D,Code);
        }
        // Use point direction and override speed and feed, normalise direction if not 0 vector.
        public GH_ToolPoint(Point3d Pt, Vector3d D, double speed, double feed)
        {
            this.Value = new ToolPoint(Pt,D,speed,feed);
        }
        // Use point direction extra Code and override speed and feed, normalise direction if not 0 vector.
        public GH_ToolPoint(Point3d Pt, Vector3d D, string Code, double speed, double feed)
        {
            this.Value = new ToolPoint(Pt,D,Code,speed,feed);
        }        
        // Create from unwrapped version
        public GH_ToolPoint(ToolPoint TP)
        {
            this.Value = new ToolPoint(TP);
        }
        // Copy Constructor
        public GH_ToolPoint(GH_ToolPoint TP)
        {
            this.Value = new ToolPoint(TP.Value);
        }
        // Duplicate
        public override IGH_Goo Duplicate()
        {
            return new GH_ToolPoint(this);
        }
  
        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(Point3d)))
            {
                object ptr = new Point3d(this.Value.Pt);
                target = (Q)ptr;
                return true;
            }
            return false;
        }

        public override bool CastFrom(object source)
        {
            if (source == null) return false;
            Point3d Pt = new Point3d(0, 0, 0);
            if (GH_Convert.ToPoint3d(source, ref Pt, GH_Conversion.Primary))
            {
                this.Value = new ToolPoint(Pt);
                return true;
            }
            return false;
        }
    }

    // Grasshopper Parameter Wrapper
    public class GH_ToolPointPar : GH_Param<GH_ToolPoint>
    {
        public GH_ToolPointPar() :
            base("ToolPoint", "ToolPt", "Contains a collection of Tool Points", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid
        {
            get { return new Guid("0bbed7c1-88a9-4d61-b7cb-e0dfe82b1b86"); }
        }
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