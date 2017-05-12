using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace CAMel.Types
{
    // The type of projection. 
    // Parallel is along a single direction
    // Cylindrical points towards a path along a direction
    // Spherical points towards a point

    public enum SurfProj {
        Parallel,
        Cylindrical,
        Spherical,
        None
    }

    // The tool direction for surfacing.
    // Projection is along the projection direction
    // Path Tangent and Path Normal mix the projection and 
    // surface normals. 
    // Path Tangent gives the normal to the path in the plane given by the path tangent and projection direction
    // Path Normal gives the normal to the path in the plane given by the surface tangent normal to the path and projection direction
    // Normal is surface normal
    public enum SurfToolDir {
        Projection,
        PathTangent,
        PathNormal,
        Normal
    }

    // A path that will project to a surface for surfacing
    // TODO write a subclass for each projection
    public class SurfacePath : CA_base
    {
        public Curve Path; // Curve to project
        public SurfProj SP; // Type of projection
        public Curve CylOnto; // centre line for Cylindrical projection
        public Vector3d dir; // direction for parallel projection, or line direction for cylindrical
        public Point3d Cen; // centre for spherical projection
        public SurfToolDir STD; // method to calculate tool direction

        // Default Constructor
        public SurfacePath()
        {
        }
        // 
        public SurfacePath(Curve Path,SurfProj SP, Vector3d dir,Point3d Cen,SurfToolDir STD)
        {
            this.Path = Path;
            this.SP = SP;
            this.dir = dir;
            this.Cen = Cen;
            this.STD = STD;
        }
        // Copy Constructor
        public SurfacePath(SurfacePath Os)
        {
            this.Path = Os.Path;
            this.SP = Os.SP;
            this.dir = Os.dir;
            this.Cen = Os.Cen;
            this.STD = Os.STD;
        }
        // Duplicate
        public SurfacePath Duplicate()
        {
            return new SurfacePath(this);
        }


        public override string TypeDescription
        {
            get { return "Path and projection information to generate a surfacing path"; }
        }

        public override string TypeName
        {
            get { return "SurfacePath"; }
        }

        public override string ToString()
        {
            return "Surfacing Path";
        }

        public MachineOperation GenerateOperation(Surface S, double offset, MaterialTool MT,MaterialForm MF, ToolPathAdditions TPA)
        {
            throw new System.NotImplementedException("");
        }

        public MachineOperation GenerateOperation(Brep B, double offset, MaterialTool MT, MaterialForm MF, ToolPathAdditions TPA)
        {
            throw new System.NotImplementedException("");
        }

        public MachineOperation GenerateOperation(Mesh M, double offset, MaterialTool MT, MaterialForm MF, ToolPathAdditions TPA)
        {
            throw new System.NotImplementedException("");
        }
    }

    // Grasshopper Type Wrapper
    public class GH_SurfacePath : CA_Goo<SurfacePath>
    {
        // Default Constructor
        public GH_SurfacePath()
        {
            this.Value = new SurfacePath();
        }

        // Copy Constructor.
        public GH_SurfacePath(GH_SurfacePath Op)
        {
            this.Value = new SurfacePath(Op.Value);
        }
        // Duplicate
        public override IGH_Goo Duplicate()
        {
            return new GH_SurfacePath(this);
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(SurfacePath)))
            {
                object ptr = this.Value;
                target = (Q)ptr;
                return true;
            }

            return false;
        }

        public override bool CastFrom(object source)
        {
            if (source == null)
            {
                return false;
            }
            if (source is SurfacePath)
            {
                this.Value = new SurfacePath((SurfacePath)source);
                return true;
            }
            return false;
        }
    }

    // Grasshopper Parameter Wrapper
    public class GH_SurfacePathPar : GH_Param<GH_SurfacePath>
    {
        public GH_SurfacePathPar() :
            base("Surfacing Path", "SurfacePath", "Contains the information to project a path onto a surface", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid
        {
            get { return new Guid("FCB36AFC-195B-4DFA-825B-A986875A3A86"); }
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
                return Properties.Resources.surfacepath;
            }
        }
    }

}