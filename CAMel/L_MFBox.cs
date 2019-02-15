using System;

using Rhino.Geometry;

using CAMel.Types.Machine;

namespace CAMel.Types.MaterialForm
{

    class MFBox : IMaterialForm
    {
        public MFBox(Box box, double matTol, double safeD)
        {
            this.box = box;
            this.materialTolerance = matTol;
            this.safeDistance = safeD;
            this._myMesh = null;
        }

        public Box box { get; }

        public double materialTolerance { get; }

        public double safeDistance { get; }

        public string TypeDescription
        { get { return "This is a box MaterialForm"; } }

        public string TypeName { get { return "CAMelMFBox"; } }

        public override string ToString()
        {
            return "Box Material ("+ this.box.X.Min.ToString("0.00") + ", " + this.box.Y.Min.ToString("0.00") + ", " + this.box.Z.Min.ToString("0.00")+") "
                + "to ("+ this.box.X.Max.ToString("0.00") + ", " + this.box.Y.Max.ToString("0.00") + ", " + this.box.Z.Max.ToString("0.00")+")";
        }
        
        public ToolPath insertRetract(ToolPath tP) => MFDefault.insertRetract(this, tP);

        public MFintersects intersect(ToolPoint tP, double tolerance) => intersect(tP.pt,tP.dir, tolerance);

        public bool intersect(Point3d start, Point3d end, double tolerance, out MFintersects inter) 
            => MFDefault.lineIntersect(this, start, end, tolerance, out inter);

        public MFintersects intersect(Point3d ptIn, Vector3d dirIn, double tolerance)
        {
            dirIn.Unitize();
            Box exB = this.box;
            exB.Inflate(tolerance+this.materialTolerance); // expand to tolerance

            // convert to Box coordinates
            this.box.Plane.RemapToPlaneSpace((Point3d)dirIn, out Point3d pt);
            Vector3d dir = (Vector3d)pt;
            this.box.Plane.RemapToPlaneSpace(ptIn, out pt);

            // test to hit each face (could stop after 2, if efficiency worth it)

            MFintersects inters = new MFintersects();
            double dist;
            if(testFace(exB.X.Max,exB.Y,exB.Z,pt, dir, out dist))
            { inters.add(fromPlane(pt + dir * dist), this.box.Plane.XAxis, dist); }
            if (testFace(exB.X.Min, exB.Y, exB.Z, pt, dir, out dist))
            { inters.add(fromPlane(pt + dir * dist), -this.box.Plane.XAxis, dist); }
            if (testFace(exB.Y.Max, exB.X, exB.Z, new Point3d(pt.Y,pt.X,pt.Z), new Vector3d (dir.Y,dir.X,dir.Z), out dist))
            { inters.add(fromPlane(pt + dir * dist), this.box.Plane.YAxis, dist); }
            if (testFace(exB.Y.Min, exB.X, exB.Z, new Point3d(pt.Y, pt.X, pt.Z), new Vector3d(dir.Y, dir.X, dir.Z), out dist))
            { inters.add(fromPlane(pt + dir * dist), -this.box.Plane.YAxis, dist); }
            if (testFace(exB.Z.Max, exB.X, exB.Y, new Point3d(pt.Z, pt.X, pt.Y), new Vector3d(dir.Z, dir.X, dir.Y), out dist))
            { inters.add(fromPlane(pt + dir * dist), this.box.Plane.ZAxis, dist); }
            if (testFace(exB.Z.Min, exB.X, exB.Y, new Point3d(pt.Z, pt.X, pt.Y), new Vector3d(dir.Z, dir.X, dir.Y), out dist))
            { inters.add(fromPlane(pt + dir * dist), -this.box.Plane.ZAxis, dist); }

            inters.midOut = inters.count > 1 ? midOutDir(inters.mid, tolerance) : new Vector3d();

            return inters;

        }
        // test the X faces, for other faces reorder point and direction.

        private bool testFace( double face, Interval odi1, Interval odi2, Point3d pt, Vector3d dir,  out double dist)
        {
            double intDist;
            double shift;
            
            if (Math.Abs(dir.X) > CAMel_Goo.tolerance)
            {
                shift = (face - pt.X) / (dir.X);
                intDist = shift * dir.Length;
            } else // parallel
            {
                dist = 0;
                return false;
            }
            Vector3d inter = (Vector3d)(pt + (shift * dir));
            if( odi1.Min < inter.Y && inter.Y < odi1.Max &&
                odi2.Min < inter.Z && inter.Z < odi2.Max ) // hit plane
            {
                dist = intDist;
                return true;
            }
            dist = 0;
            return false;
        }

        private Point3d fromPlane(Point3d pt) => this.box.Plane.PointAt(pt.X, pt.Y, pt.Z);

        private Vector3d midOutDir(Point3d pt, double tolerance)
        {
            double uTol = this.materialTolerance + tolerance;
            // check how close to each face, return normal of closest
            double closeD = this.box.X.Max+uTol - pt.X;
            Vector3d outD = this.box.Plane.XAxis;
            if(closeD > (pt.X-this.box.X.Min-uTol))
            {
                closeD = (pt.X- this.box.X.Min-uTol);
                outD = -this.box.Plane.XAxis;
            }
            if (closeD > (this.box.Y.Max+uTol - pt.Y))
            {
                closeD = (this.box.Y.Max+uTol - pt.Y);
                outD = this.box.Plane.YAxis;
            }
            if (closeD > (pt.Y - this.box.Y.Min-uTol))
            {
                closeD = (pt.Y - this.box.Y.Min-uTol);
                outD = -this.box.Plane.YAxis;
            }
            if (closeD > (this.box.Z.Max+uTol - pt.Z))
            {
                closeD = (this.box.Z.Max+uTol - pt.Z);
                outD = this.box.Plane.ZAxis;
            }
            if (closeD > (pt.Z - this.box.Z.Min-uTol))
            {
                closeD = (pt.Z - this.box.Z.Min-uTol);
                outD = -this.box.Plane.ZAxis;
            }
            if(closeD < -2*uTol) { throw new FormatException("MidOutDir in MFBox called for point outside the Box."); }
            return outD;
        }

        public ToolPath refine(ToolPath tP, IMachine m) => MFDefault.refine(this, tP, m);

        private Mesh _myMesh;
        private void setMesh() => this._myMesh = Mesh.CreateFromBox(this.box, 1, 1, 1);

        public Mesh getMesh()
        {
            if (this._myMesh == null) { setMesh(); }
            return this._myMesh;
        }
        public BoundingBox getBoundingBox()
        {
            return this.box.BoundingBox;
        }
    }
}
