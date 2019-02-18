using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace CAMel.Types
{

    // A path that will create pockets from boundary curves,
    // direction vectors and a maximum depth.
    public class PocketPath : ICAMel_Base
    {
        public List<Curve> Paths; // Boundary Pocket Curves
        public Vector3d dir; // direction for the pocket
        public double depth; // depth of the pocket

        private Mesh M; // Private Mesh cache for calculation ease
        private Brep B; // Private Brep cache for calculation ease

        // Default Constructor
        public PocketPath()
        {
            this.Paths = new List<Curve>();
            this.dir = new Vector3d(0.0, 0.0, 1.0);
            this.depth = 1.0;
        }

        // Implicit depth Constructor
        public PocketPath(List<Curve> Paths, Vector3d dir)
        {
            this.Paths = Paths;
            this.dir = dir;
            this.depth = 1.0;
        }

        // Implicit direction Constructor
        public PocketPath(List<Curve> Paths, double depth)
        {
            this.Paths = Paths;
            this.depth = depth;
            this.dir = new Vector3d(0.0, 0.0, 1.0);
        }

        // Fully saturated Constructor
        public PocketPath(List<Curve> Paths, Vector3d dir, double depth)
        {
            this.Paths = Paths;
            this.dir = dir;
            this.depth = depth;
        }

        // Copy Constructor
        public PocketPath(PocketPath Ps)
        {
            this.Paths = Ps.Paths;
            this.dir = Ps.dir;
            this.depth = Ps.depth;
        }

        // Duplicate
        public PocketPath Duplicate()
        {
            return new PocketPath(this);
        }

        public string TypeDescription
        {
            get
            {
                return "Path information to generate a pocket";
            }
        }

        public string TypeName
        {
            get
            {
                return "PocketPath";
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
            string pfx = "Pocketing:";
            pfx += " Direction " + this.dir.ToString();
            pfx += " Depth " + this.depth.ToString();
            return base.ToString();
        }

        ICAMel_Base ICAMel_Base.Duplicate()
        {
            throw new NotImplementedException();
        }
    }
}
