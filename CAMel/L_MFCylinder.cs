using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace CAMel.Types.MaterialForm
{
    class MFCylinder : IMaterialForm
    {
        public bool IsValid
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public double materialTolerance
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

        public double safeDistance
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

        public string TypeDescription
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string TypeName
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public ICAMel_Base Duplicate()
        {
            throw new NotImplementedException();
        }

        public ToolPath InsertRetract(ToolPath TP)
        {
            throw new NotImplementedException();
        }

        public ToolPath InsertRetract(ToolPath TP, Vector3d dir)
        {
            throw new NotImplementedException();
        }

        public double intersect(Point3d Pt, Vector3d direction, double tolerance, out Vector3d Norm)
        {
            throw new NotImplementedException();
        }

        public double intersect(Point3d Pt, Vector3d direction, double tolerance, out Vector3d Norm, out DirectedPointInsideOutside dist)
        {
            throw new NotImplementedException();
        }

        public double intersect(ToolPoint TP, double tolerance, out Vector3d Norm)
        {
            throw new NotImplementedException();
        }

        public double intersect(ToolPoint TP, double tolerance, out Vector3d Norm, out DirectedPointInsideOutside dist)
        {
            throw new NotImplementedException();
        }

        public bool cutThrough(Point3d FromPt, Point3d ToPt, double tolerance, out Point3d mid, out Vector3d outD)
        {
            throw new NotImplementedException();
        }

        public ToolPath Refine(ToolPath TP, Machine M)
        {
            throw new NotImplementedException();
        }

        public bool getBrep(ref object brep)
        {
            throw new NotImplementedException();
        }

        public bool getBrep(ref Brep brep)
        {
            throw new NotImplementedException();
        }

        public bool getMesh(ref object mesh)
        {
            throw new NotImplementedException();
        }

        public bool getMesh(ref Mesh mesh)
        {
            throw new NotImplementedException();
        }

    }
}
