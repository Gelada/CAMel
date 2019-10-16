namespace CAMel.Types.MaterialForm
{
    using System;

    using CAMel.Types.Machine;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    /// <summary>TODO The mf cylinder.</summary>
    public class MFCylinder : IMaterialForm
    {
        /// <summary>Initializes a new instance of the <see cref="MFCylinder"/> class.</summary>
        /// <param name="cy">TODO The cy.</param>
        /// <param name="matTol">TODO The mat tol.</param>
        /// <param name="safeD">TODO The safe d.</param>
        public MFCylinder(Cylinder cy, double matTol, double safeD)
        {
            this.centre = new Line(cy.CircleAt(cy.Height1).Center, cy.CircleAt(cy.Height2).Center);
            this.plane = new Plane(this.centre.From, this.centre.To - this.centre.From);
            this.height = (this.centre.To - this.centre.From).Length;
            this.radius = cy.CircleAt(0).Radius;
            this.materialTolerance = matTol;
            this.safeDistance = safeD;
        }

        /// <summary>Gets the centre.</summary>
        private Line centre { get; }
        /// <summary>Gets the radius.</summary>
        private double radius { get; }
        /// <summary>Gets the plane.</summary>
        private Plane plane { get; }
        /// <summary>Gets the height.</summary>
        private double height { get; }

        /// <summary>Gets the material tolerance.</summary>
        public double materialTolerance { get; }
        /// <summary>Gets the safe distance.</summary>
        public double safeDistance { get; }

        /// <summary>TODO The type description.</summary>
        public string TypeDescription => "This is a cylinder MaterialForm";

        /// <summary>TODO The type name.</summary>
        public string TypeName => "CAMelMFCylinder";

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        public override string ToString()
        {
            Point3d end = this.plane.Origin + this.height * this.plane.ZAxis;
            return "Cylinder Material r:" + this.radius + " s:" + this.plane.Origin + " e:" + end;
        }

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
            double uTol = tolerance + this.materialTolerance;

            // expand by tolerance
            double exRadius = this.radius + uTol;

            // convert to Cylinder coordinates
            this.plane.RemapToPlaneSpace(dirIn + this.plane.Origin, out Point3d pt);
            Vector3d dir = (Vector3d)pt;
            dir.Unitize();
            this.plane.RemapToPlaneSpace(ptIn, out pt);

            // give the projections of the points to the cylinder's planes
            Point3d pt2D = pt;
            pt2D.Z = 0;
            Vector3d dir2D = dir;
            dir2D.Z = 0;
            double flatDist = dir2D.Length;

            // test to see where the cylinder is hit.
            MFintersects inters = new MFintersects();
            Vector3d intPt;
            double linePcen;
            double linePshift;
            double cenDist;

            // test for top and bottom
            if (Math.Abs(dir.Z) < CAMel_Goo.Tolerance) // parallel to plane
            {
                if (pt.Z <= this.height + uTol && pt.Z >= -uTol) // hits cylinder
                {
                    // Find the closest point on the line, the distance to it and so
                    // the distance along the line from the closest point to the
                    // cylinder
                    linePcen = (Vector3d)pt2D * dir2D;
                    cenDist = ((Vector3d)(pt2D - linePcen * dir2D)).Length;
                    if (cenDist < exRadius)
                    {
                        linePshift = Math.Sqrt(exRadius * exRadius - cenDist * cenDist);

                        // add the two intersection points.
                        intPt = (Vector3d)pt + (linePshift - linePcen) * dir;
                        inters.add(this.fromPlane((Point3d)intPt), this.fromPlane((Point3d)zeroZ(intPt)) - this.plane.Origin, linePshift - linePcen);
                        intPt = (Vector3d)pt + (-linePshift - linePcen) * dir;
                        inters.add(this.fromPlane((Point3d)intPt), this.fromPlane((Point3d)zeroZ(intPt)) - this.plane.Origin, -linePshift - linePcen);
                    }
                }
            }
            else
            {
                double lineP = (-uTol - pt.Z) / dir.Z;
                intPt = (Vector3d)pt + lineP * dir;
                if (zeroZ(intPt).Length <= exRadius) // hit bottom
                {
                    inters.add(this.fromPlane((Point3d)intPt), -this.plane.ZAxis, lineP);
                }

                lineP = (this.height + uTol - pt.Z) / dir.Z;
                intPt = (Vector3d)pt + lineP * dir;
                if (zeroZ(intPt).Length <= exRadius) // hit top
                {
                    inters.add(this.fromPlane((Point3d)intPt), this.plane.ZAxis, lineP);
                }

                if (inters.count < 2 && flatDist > 0) // not all hits top or bottom
                {
                    // Find the closest point on the line, the distance to it and so
                    // the distance along the line from the closest point to the
                    // cylinder
                    linePcen = (Vector3d)pt2D * dir2D / flatDist;
                    cenDist = ((Vector3d)(pt2D - linePcen * dir2D / flatDist)).Length;
                    linePshift = Math.Sqrt(exRadius * exRadius - cenDist * cenDist);

                    // add the two intersection points.
                    intPt = (Vector3d)pt + (linePshift - linePcen) * dir / flatDist;
                    if (intPt.Z >= -uTol && intPt.Z <= this.height + uTol)
                    {
                        inters.add(
                            this.fromPlane((Point3d)intPt),
                            this.fromPlane((Point3d)zeroZ(intPt)) - this.plane.Origin,
                            (linePshift - linePcen) / flatDist);
                    }

                    intPt = (Vector3d)pt + (-linePshift - linePcen) * dir / flatDist;
                    if (intPt.Z >= -uTol && intPt.Z <= this.height + uTol)
                    {
                        inters.add(
                            this.fromPlane((Point3d)intPt),
                            this.fromPlane((Point3d)zeroZ(intPt)) - this.plane.Origin,
                            (-linePshift - linePcen) / flatDist);
                    }
                }
            }

            inters.midOut = inters.count > 1 ? this.midOutDir(inters.mid, dirIn, tolerance) : new Vector3d();
            return inters;
        }

        /// <summary>TODO The mid out dir.</summary>
        /// <param name="ptIn">TODO The pt in.</param>
        /// <param name="dirIn">TODO The dir in.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <returns>The <see cref="Vector3d"/>.</returns>
        /// <exception cref="FormatException"></exception>
        private Vector3d midOutDir(Point3d ptIn, Vector3d dirIn, double tolerance)
        {
            this.plane.RemapToPlaneSpace(ptIn, out Point3d pt);
            double uTol = tolerance + this.materialTolerance;

            // start assuming closest to bottom
            double closeD = uTol + pt.Z;
            Vector3d outD = -this.plane.ZAxis;
            if (closeD > this.height + uTol - pt.Z) // closer to top?
            {
                closeD = this.height + uTol - pt.Z;
                outD = this.plane.ZAxis;
            }

            if (closeD > this.radius + uTol - ((Vector3d)zeroZ(pt)).Length) // closer to side?
            {
                closeD = this.radius + uTol - ((Vector3d)zeroZ(pt)).Length;
                if (((Vector3d)zeroZ(pt)).Length > 0.000001)
                {
                    outD = this.fromPlane(zeroZ(pt)) - this.plane.Origin;
                }
                else
                {
                    this.plane.RemapToPlaneSpace(dirIn + this.plane.Origin, out Point3d dirP);
                    outD = this.fromPlane(new Point3d(dirP.Y, -dirP.X, 0)) - this.plane.Origin;
                }

                outD.Unitize();
            }

            if (closeD < 0)
            {
                throw new FormatException("MidOutDir in MFCylinder called for point outside the Cylinder.");
            }

            return outD;
        }

        // Move a point to the Plane space
        /// <summary>TODO The from plane.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <returns>The <see cref="Point3d"/>.</returns>
        private Point3d fromPlane(Point3d pt) => this.plane.PointAt(pt.X, pt.Y, pt.Z);

        /// <summary>TODO The zero z.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <returns>The <see cref="Point3d"/>.</returns>
        private static Point3d zeroZ(Point3d pt) => new Point3d(pt.X, pt.Y, 0);
        /// <summary>TODO The zero z.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <returns>The <see cref="Vector3d"/>.</returns>
        private static Vector3d zeroZ(Vector3d pt) => new Vector3d(pt.X, pt.Y, 0);

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
        private Mesh setMesh() =>
            Mesh.CreateFromCylinder(
                new Cylinder(
                    new Circle(this.plane, this.radius),
                    (this.centre.To - this.centre.From).Length),
                1, 360)
            ?? new Mesh();

        /// <inheritdoc />
        public Mesh getMesh() => this.myMesh ?? (this.myMesh = this.setMesh());

        /// <summary>TODO The my brep.</summary>
        private Brep myBrep;
        /// <summary>TODO The set brep.</summary>
        /// <returns>The <see cref="Brep"/>.</returns>
        [NotNull]
        private Brep setBrep() =>
            Brep.CreateFromCylinder(
                new Cylinder(
                    new Circle(this.plane, this.radius),
                    (this.centre.To - this.centre.From).Length),
                true, true)
            ?? new Brep();

        /// <inheritdoc />
        [NotNull]
        public Brep getBrep() => this.myBrep ?? (this.myBrep = this.setBrep());
        /// <summary>TODO The get bounding box.</summary>
        /// <returns>The <see cref="BoundingBox"/>.</returns>
        public BoundingBox getBoundingBox()
        {
            this.myMesh = this.myMesh ?? (this.myMesh = this.setMesh());
            return this.myMesh.GetBoundingBox(false);
        }
    }
}
