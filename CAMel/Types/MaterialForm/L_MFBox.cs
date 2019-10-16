namespace CAMel.Types.MaterialForm
{
    using System;

    using CAMel.Types.Machine;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    /// <summary>TODO The mf box.</summary>
    public class MFBox : IMaterialForm
    {
        /// <summary>Initializes a new instance of the <see cref="MFBox"/> class.</summary>
        /// <param name="box">TODO The box.</param>
        /// <param name="matTol">TODO The mat tol.</param>
        /// <param name="safeD">TODO The safe d.</param>
        public MFBox(Box box, double matTol, double safeD)
        {
            this.box = box;
            this.materialTolerance = matTol;
            this.safeDistance = safeD;
            this.myMesh = null;
        }

        /// <summary>Gets the box.</summary>
        private Box box { get; }

        /// <summary>Gets the material tolerance.</summary>
        public double materialTolerance { get; }

        /// <summary>Gets the safe distance.</summary>
        public double safeDistance { get; }

        /// <inheritdoc />
        /// <summary>TODO The type description.</summary>
        public string TypeDescription => "This is a box MaterialForm";

        /// <inheritdoc />
        /// <summary>TODO The type name.</summary>
        public string TypeName => "CAMelMFBox";

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="T:System.String" />.</returns>
        public override string ToString() => "Box Material (" + this.box.X.Min.ToString("0.00") + ", " + this.box.Y.Min.ToString("0.00") + ", " + this.box.Z.Min.ToString("0.00") + ") "
                                             + "to (" + this.box.X.Max.ToString("0.00") + ", " + this.box.Y.Max.ToString("0.00") + ", " + this.box.Z.Max.ToString("0.00") + ")";

        /// <summary>TODO The intersect.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <returns>The <see cref="MFintersects"/>.</returns>
        public MFintersects intersect(ToolPoint tP, double tolerance) => this.intersect(tP.pt, tP.dir, tolerance);

        /// <summary>TODO The intersect.</summary>
        /// <param name="start">TODO The start.</param>
        /// <param name="end">TODO The end.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <param name="inter">TODO The inter.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public bool intersect(Point3d start, Point3d end, double tolerance, out MFintersects inter)
            => MFDefault.lineIntersect(this, start, end, tolerance, out inter);

        /// <summary>TODO The intersect.</summary>
        /// <param name="ptIn">TODO The pt in.</param>
        /// <param name="dirIn">TODO The dir in.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <returns>The <see cref="MFintersects"/>.</returns>
        public MFintersects intersect(Point3d ptIn, Vector3d dirIn, double tolerance)
        {
            dirIn.Unitize();
            Box exB = this.box;
            exB.Inflate(tolerance + this.materialTolerance); // expand to tolerance

            // convert to Box coordinates
            this.box.Plane.RemapToPlaneSpace((Point3d)dirIn, out Point3d pt);
            Vector3d dir = (Vector3d)pt;
            this.box.Plane.RemapToPlaneSpace(ptIn, out pt);

            // test to hit each face (could stop after 2, if efficiency worth it)
            MFintersects inters = new MFintersects();
            if (testFace(exB.X.Max, exB.Y, exB.Z, pt, dir, out double dist))
            { inters.add(this.fromPlane(pt + dir * dist), this.box.Plane.XAxis, dist); }
            if (testFace(exB.X.Min, exB.Y, exB.Z, pt, dir, out dist))
            { inters.add(this.fromPlane(pt + dir * dist), -this.box.Plane.XAxis, dist); }
            if (testFace(exB.Y.Max, exB.X, exB.Z, new Point3d(pt.Y, pt.X, pt.Z), new Vector3d(dir.Y, dir.X, dir.Z), out dist))
            { inters.add(this.fromPlane(pt + dir * dist), this.box.Plane.YAxis, dist); }
            if (testFace(exB.Y.Min, exB.X, exB.Z, new Point3d(pt.Y, pt.X, pt.Z), new Vector3d(dir.Y, dir.X, dir.Z), out dist))
            { inters.add(this.fromPlane(pt + dir * dist), -this.box.Plane.YAxis, dist); }
            if (testFace(exB.Z.Max, exB.X, exB.Y, new Point3d(pt.Z, pt.X, pt.Y), new Vector3d(dir.Z, dir.X, dir.Y), out dist))
            { inters.add(this.fromPlane(pt + dir * dist), this.box.Plane.ZAxis, dist); }
            if (testFace(exB.Z.Min, exB.X, exB.Y, new Point3d(pt.Z, pt.X, pt.Y), new Vector3d(dir.Z, dir.X, dir.Y), out dist))
            { inters.add(this.fromPlane(pt + dir * dist), -this.box.Plane.ZAxis, dist); }

            inters.midOut = inters.count > 1 ? this.midOutDir(inters.mid, tolerance) : new Vector3d();

            return inters;
        }

        // test the X faces, for other faces reorder point and direction.
        /// <summary>TODO The test face.</summary>
        /// <param name="face">TODO The face.</param>
        /// <param name="odi1">TODO The odi 1.</param>
        /// <param name="odi2">TODO The odi 2.</param>
        /// <param name="pt">TODO The pt.</param>
        /// <param name="dir">TODO The dir.</param>
        /// <param name="dist">TODO The dist.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        private static bool testFace(double face, Interval odi1, Interval odi2, Point3d pt, Vector3d dir, out double dist)
        {
            double intDist;
            double shift;

            if (Math.Abs(dir.X) > CAMel_Goo.Tolerance)
            {
                shift = (face - pt.X) / dir.X;
                intDist = shift * dir.Length;
            }
            else // parallel
            {
                dist = 0;
                return false;
            }

            Vector3d inter = (Vector3d)(pt + shift * dir);
            if (odi1.Min < inter.Y && inter.Y < odi1.Max &&
                odi2.Min < inter.Z && inter.Z < odi2.Max) // hit plane
            {
                dist = intDist;
                return true;
            }

            dist = 0;
            return false;
        }

        /// <summary>TODO The from plane.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <returns>The <see cref="Point3d"/>.</returns>
        private Point3d fromPlane(Point3d pt) => this.box.Plane.PointAt(pt.X, pt.Y, pt.Z);

        /// <summary>TODO The mid out dir.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <returns>The <see cref="Vector3d"/>.</returns>
        /// <exception cref="FormatException"></exception>
        private Vector3d midOutDir(Point3d pt, double tolerance)
        {
            double uTol = this.materialTolerance + tolerance;

            // check how close to each face, return normal of closest
            double closeD = this.box.X.Max + uTol - pt.X;
            Vector3d outD = this.box.Plane.XAxis;
            if (closeD > pt.X - this.box.X.Min + uTol)
            {
                closeD = pt.X - this.box.X.Min + uTol;
                outD = -this.box.Plane.XAxis;
            }

            if (closeD > this.box.Y.Max + uTol - pt.Y)
            {
                closeD = this.box.Y.Max + uTol - pt.Y;
                outD = this.box.Plane.YAxis;
            }

            if (closeD > pt.Y - this.box.Y.Min + uTol)
            {
                closeD = pt.Y - this.box.Y.Min + uTol;
                outD = -this.box.Plane.YAxis;
            }

            if (closeD > this.box.Z.Max + uTol - pt.Z)
            {
                closeD = this.box.Z.Max + uTol - pt.Z;
                outD = this.box.Plane.ZAxis;
            }

            if (closeD > pt.Z - this.box.Z.Min + uTol)
            {
                closeD = pt.Z - this.box.Z.Min + uTol;
                outD = -this.box.Plane.ZAxis;
            }

            if (closeD < -2 * uTol) { throw new FormatException("MidOutDir in MFBox called for point outside the Box."); }
            return outD;
        }

        /// <summary>TODO The refine.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="m">TODO The m.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        public ToolPath refine(ToolPath tP, IMachine m) => MFDefault.refine(this, tP, m);

        /// <summary>TODO The my mesh.</summary>
        private Mesh myMesh;
        /// <summary>TODO The set mesh.</summary>
        /// <returns>The <see cref="Mesh"/>.</returns>
        [NotNull]
        private Mesh setMesh() => Mesh.CreateFromBox(this.box, 1, 1, 1) ?? new Mesh();

        /// <summary>TODO The get mesh.</summary>
        /// <returns>The <see cref="Mesh"/>.</returns>
        public Mesh getMesh() => this.myMesh ?? (this.myMesh = this.setMesh());

        /// <summary>TODO The my brep.</summary>
        private Brep myBrep;
        /// <summary>TODO The set brep.</summary>
        /// <returns>The <see cref="Brep"/>.</returns>
        [NotNull]
        private Brep setBrep() => Brep.CreateFromBox(this.box) ?? new Brep();

        /// <summary>TODO The get brep.</summary>
        /// <returns>The <see cref="Brep"/>.</returns>
        [NotNull]
        public Brep getBrep() => this.myBrep ?? (this.myBrep = this.setBrep());
        /// <summary>TODO The get bounding box.</summary>
        /// <returns>The <see cref="BoundingBox"/>.</returns>
        public BoundingBox getBoundingBox() => this.box.BoundingBox;
    }
}
