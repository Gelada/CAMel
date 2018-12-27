using System;
using System.Collections.Generic;
using Rhino.Geometry;
using CAMel.Types.Machine;

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

        public override string ToString()
        {
            return "MFBox "+this.B.ToString();
        }

        public ICAMel_Base Duplicate()
        {
            return (ICAMel_Base) this.MemberwiseClone();
        }

        public ToolPath InsertRetract(ToolPath TP)
        {
            return MFDefault.InsertRetract(this, TP);
        }

        public MFintersects intersect(ToolPoint TP, double tolerance)
        {
            return this.intersect(TP.Pt,TP.Dir, tolerance);
        }

        public bool intersect(Point3d start, Point3d end, double tolerance, out MFintersects inter)
        {
            return MFDefault.lineIntersect(this, start, end, tolerance, out inter);
        }

        public MFintersects intersect(Point3d PtIn, Vector3d dirIn, double tolerance)
        {
            dirIn.Unitize();
            Box exB = this.B;
            exB.Inflate(tolerance+this.materialTolerance); // expand to tolerance

            // convert to Box coordinates
            Point3d Pt = new Point3d();
            Vector3d dir = new Vector3d();
            this.B.Plane.RemapToPlaneSpace((Point3d)dirIn, out Pt);
            dir = (Vector3d)Pt;
            this.B.Plane.RemapToPlaneSpace(PtIn, out Pt);

            // test to hit each face (could stop after 2, if efficiency worth it)

            MFintersects inters = new MFintersects();
            double dist;
            if(testFace(exB.X.Max,exB.Y,exB.Z,Pt, dir, out dist))
            { inters.Add(this.fromPlane(Pt + dir * dist), this.B.Plane.XAxis, dist); }
            if (testFace(exB.X.Min, exB.Y, exB.Z, Pt, dir, out dist))
            { inters.Add(this.fromPlane(Pt + dir * dist), -this.B.Plane.XAxis, dist); }
            if (testFace(exB.Y.Max, exB.X, exB.Z, new Point3d(Pt.Y,Pt.X,Pt.Z), new Vector3d (dir.Y,dir.X,dir.Z), out dist))
            { inters.Add(this.fromPlane(Pt + dir * dist), this.B.Plane.YAxis, dist); }
            if (testFace(exB.Y.Min, exB.X, exB.Z, new Point3d(Pt.Y, Pt.X, Pt.Z), new Vector3d(dir.Y, dir.X, dir.Z), out dist))
            { inters.Add(this.fromPlane(Pt + dir * dist), -this.B.Plane.YAxis, dist); }
            if (testFace(exB.Z.Max, exB.X, exB.Y, new Point3d(Pt.Z, Pt.X, Pt.Y), new Vector3d(dir.Z, dir.X, dir.Y), out dist))
            { inters.Add(this.fromPlane(Pt + dir * dist), this.B.Plane.ZAxis, dist); }
            if (testFace(exB.Z.Min, exB.X, exB.Y, new Point3d(Pt.Z, Pt.X, Pt.Y), new Vector3d(dir.Z, dir.X, dir.Y), out dist))
            { inters.Add(this.fromPlane(Pt + dir * dist), -this.B.Plane.ZAxis, dist); }

            if (inters.Count > 1)
            {
                inters.midOut = this.midOutDir(inters.mid, tolerance);
            } else
            {
                inters.midOut = new Vector3d();
            }

            return inters;

        }
        // test the X faces, for other faces reorder point and direction.

        private bool testFace( double face, Interval odi1, Interval odi2, Point3d Pt, Vector3d Dir,  out double dist)
        {
            double intDist;
            double shift;
            
            if (Dir.X != 0)
            {
                shift = (face - Pt.X) / (Dir.X);
                intDist = shift * Dir.Length;
            } else // parallel
            {
                dist = 0;
                return false;
            }
            Vector3d inter = (Vector3d)(Pt + (shift * Dir));
            if( odi1.Min < inter.Y && inter.Y < odi1.Max &&
                odi2.Min < inter.Z && inter.Z < odi2.Max ) // hit plane
            {
                dist = intDist;
                return true;
            }
            dist = 0;
            return false;
        }

        private Point3d fromPlane(Point3d Pt)
        {
            return this.B.Plane.PointAt(Pt.X, Pt.Y, Pt.Z);
        }

        private Vector3d midOutDir(Point3d Pt, double tolerance)
        {
            double utol = materialTolerance + tolerance;
            double closeD;
            Vector3d outD;
            // check how close to each face, return normal of closest
            closeD = this.B.X.Max+utol - Pt.X;
            outD = this.B.Plane.XAxis;
            if(closeD > (Pt.X-this.B.X.Min-utol))
            {
                closeD = (Pt.X- this.B.X.Min-utol);
                outD = -this.B.Plane.XAxis;
            }
            if (closeD > (this.B.Y.Max+utol - Pt.Y))
            {
                closeD = (this.B.Y.Max+utol - Pt.Y);
                outD = this.B.Plane.YAxis;
            }
            if (closeD > (Pt.Y - this.B.Y.Min-utol))
            {
                closeD = (Pt.Y - this.B.Y.Min-utol);
                outD = -this.B.Plane.YAxis;
            }
            if (closeD > (this.B.Z.Max+utol - Pt.Z))
            {
                closeD = (this.B.Z.Max+utol - Pt.Z);
                outD = this.B.Plane.ZAxis;
            }
            if (closeD > (Pt.Z - this.B.Z.Min-utol))
            {
                closeD = (Pt.Z - this.B.Z.Min-utol);
                outD = -this.B.Plane.ZAxis;
            }
            if(closeD < -2*utol) { throw new FormatException("MidOutDir in MFBox called for point outside the Box."); }
            return outD;
        }

        public ToolPath refine(ToolPath TP, IMachine M)
        {
            return MFDefault.refine(this, TP, M);
        }

        public bool getBrep(ref object brep)
        {
            if (this.B.IsValid)
            {
                brep = this.B.ToBrep();
                return true;
            } else { return false; }
        }

        public bool getBrep(ref Brep brep)
        {
            if (this.B.IsValid)
            {
                brep = this.B.ToBrep();
                return true;
            }
            else { return false; }
        }

        public bool getMesh(ref object mesh)
        {
            if (this.B.IsValid)
            {
                mesh = Mesh.CreateFromBox(this.B, 1, 1, 1);
                return true;
            }
            else { return false; }

        }

        public bool getMesh(ref Mesh mesh)
        {
            if (this.B.IsValid)
            {
                mesh = Mesh.CreateFromBox(this.B, 1, 1, 1);
                return true;
            }
            else { return false; }
        }
    }
}
