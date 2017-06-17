using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace CAMel.Types.MaterialForm
{

    class MFBox : IMaterialForm
    {
        public MFBox(Box box, double matTol, double safeD)
        { this.B = box; this.materialTolerance = matTol; this.safeDistance = safeD; }

        public bool IsValid { get { return B.IsValid; } }

        public Box B { get; private set; }

        public double materialTolerance { get; set; }

        public double safeDistance { get; set; }

        public string TypeDescription
        { get { return "This is a box MaterialForm"; } }

        public string TypeName { get { return "CAMelMFBox"; } }

        public double closestDanger(List<Point3d> route, out Point3d cPt, out Vector3d away, out int i)
        {
            throw new NotImplementedException();
        }

        public double closestDanger(List<Point3d> route, IMaterialForm toForm, out Point3d cPt, out Vector3d away, out int i)
        {
            throw new NotImplementedException();
        }

        public ICAMel_Base Duplicate()
        {
            return (ICAMel_Base) this.MemberwiseClone();
        }

        public ToolPath InsertRetract(ToolPath TP)
        {
            throw new NotImplementedException();
        }

        public ToolPath InsertRetract(ToolPath TP, Vector3d dir)
        {
            throw new NotImplementedException();
        }

        public double intersect(Point3d Pt, Vector3d direction,double tolerance, out Vector3d Norm)
        {
            DirectedPointInsideOutside dpio;
            return this.intersect(Pt, direction, tolerance, out Norm, out dpio);
        }

        public double intersect(ToolPoint TP, double tolerance, out Vector3d Norm, out DirectedPointInsideOutside dist)
        {
            throw new NotImplementedException();
        }

        public double intersect(ToolPoint TP, double tolerance, out Vector3d Norm)
        {
            DirectedPointInsideOutside dpio;
            return this.intersect(TP.Pt,-TP.Dir, tolerance, out Norm, out dpio);
        }

        public double intersect(Point3d Pt, Vector3d direction, double tolerance, out Vector3d Norm, out DirectedPointInsideOutside dist)
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
