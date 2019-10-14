namespace CAMel.Types.MaterialForm
{
    using System;

    using CAMel.Types.Machine;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    public class MFCylinder : IMaterialForm
    {
        public MFCylinder(Cylinder cy, double matTol, double safeD)
        {
            this.centre = new Line(cy.CircleAt(cy.Height1).Center, cy.CircleAt(cy.Height2).Center);
            this.plane = new Plane(this.centre.From, this.centre.To - this.centre.From);
            this.height = (this.centre.To - this.centre.From).Length;
            this.radius = cy.CircleAt(0).Radius;
            this.materialTolerance = matTol;
            this.safeDistance = safeD;
        }

        private Line centre { get; }
        private double radius { get; }
        private Plane plane { get; }
        private double height { get; }

        public double materialTolerance { get; }
        public double safeDistance { get; }

        public string TypeDescription => "This is a cylinder MaterialForm";

        public string TypeName => "CAMelMFCylinder";

        public override string ToString()
        {
            Point3d end = this.plane.Origin + this.height * this.plane.ZAxis;
            return "Cylinder Material r:" + this.radius + " s:" + this.plane.Origin + " e:" + end;
        }

        public MFintersects intersect(ToolPoint tP, double tolerance) => intersect(tP.pt, tP.dir, tolerance);

        public bool intersect(Point3d start, Point3d end, double tolerance, out MFintersects inter)
            => MFDefault.lineIntersect(this, start, end, tolerance, out inter);

        public MFintersects intersect(Point3d ptIn, Vector3d dirIn, double tolerance)
        {
            double uTol = tolerance + this.materialTolerance;

            // expand by tolerance
            double exRadius = this.radius + uTol;

            // convert to Cylinder coordinates
            this.plane.RemapToPlaneSpace(dirIn + this.plane.Origin, out Point3d pt);
            Vector3d dir = (Vector3d) pt;
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
                    linePcen = (Vector3d) pt2D * dir2D;
                    cenDist = ((Vector3d) (pt2D - linePcen * dir2D)).Length;
                    if (cenDist < exRadius)
                    {
                        linePshift = Math.Sqrt(exRadius * exRadius - cenDist * cenDist);
                        // add the two intersection points.
                        intPt = (Vector3d) pt + (linePshift - linePcen) * dir;
                        inters.add(fromPlane((Point3d) intPt), fromPlane((Point3d) zeroZ(intPt)) - this.plane.Origin, linePshift - linePcen);
                        intPt = (Vector3d) pt + (-linePshift - linePcen) * dir;
                        inters.add(fromPlane((Point3d) intPt), fromPlane((Point3d) zeroZ(intPt)) - this.plane.Origin, -linePshift - linePcen);
                    }
                }
            }
            else
            {
                double lineP = (-uTol - pt.Z) / dir.Z;
                intPt = (Vector3d) pt + lineP * dir;
                if (zeroZ(intPt).Length <= exRadius) // hit bottom
                {
                    inters.add(fromPlane((Point3d) intPt), -this.plane.ZAxis, lineP);
                }
                lineP = (this.height + uTol - pt.Z) / dir.Z;
                intPt = (Vector3d) pt + lineP * dir;
                if (zeroZ(intPt).Length <= exRadius) // hit top
                {
                    inters.add(fromPlane((Point3d) intPt), this.plane.ZAxis, lineP);
                }

                if (inters.count < 2 && flatDist > 0) // not all hits top or bottom
                {
                    // Find the closest point on the line, the distance to it and so
                    // the distance along the line from the closest point to the
                    // cylinder
                    linePcen = (Vector3d) pt2D * dir2D / flatDist;
                    cenDist = ((Vector3d) (pt2D - linePcen * dir2D / flatDist)).Length;
                    linePshift = Math.Sqrt(exRadius * exRadius - cenDist * cenDist);
                    // add the two intersection points.
                    intPt = (Vector3d) pt + (linePshift - linePcen) * dir / flatDist;
                    if (intPt.Z >= -uTol && intPt.Z <= this.height + uTol)
                    {
                        inters.add(
                            fromPlane((Point3d) intPt),
                            fromPlane((Point3d) zeroZ(intPt)) - this.plane.Origin,
                            (linePshift - linePcen) / flatDist);
                    }
                    intPt = (Vector3d) pt + (-linePshift - linePcen) * dir / flatDist;
                    if (intPt.Z >= -uTol && intPt.Z <= this.height + uTol)
                    {
                        inters.add(
                            fromPlane((Point3d) intPt),
                            fromPlane((Point3d) zeroZ(intPt)) - this.plane.Origin,
                            (-linePshift - linePcen) / flatDist);
                    }
                }
            }
            inters.midOut = inters.count > 1 ? midOutDir(inters.mid, dirIn, tolerance) : new Vector3d();
            return inters;
        }
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
            if (closeD > this.radius + uTol - ((Vector3d) zeroZ(pt)).Length) // closer to side?
            {
                closeD = this.radius + uTol - ((Vector3d) zeroZ(pt)).Length;
                if (((Vector3d) zeroZ(pt)).Length > 0.000001)
                {
                    outD = fromPlane(zeroZ(pt)) - this.plane.Origin;
                }
                else
                {
                    this.plane.RemapToPlaneSpace(dirIn + this.plane.Origin, out Point3d dirP);
                    outD = fromPlane(new Point3d(dirP.Y, -dirP.X, 0)) - this.plane.Origin;
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
        private Point3d fromPlane(Point3d pt) => this.plane.PointAt(pt.X, pt.Y, pt.Z);

        private static Point3d zeroZ(Point3d pt) => new Point3d(pt.X, pt.Y, 0);
        private static Vector3d zeroZ(Vector3d pt) => new Vector3d(pt.X, pt.Y, 0);

        public ToolPath refine(ToolPath tP, IMachine m) => MFDefault.refine(this, tP, m);

        private Mesh myMesh;

        [NotNull]
        private Mesh setMesh() => Mesh.CreateFromCylinder(
                                      new Cylinder(
                                          new Circle(this.plane, this.radius),
                                          (this.centre.To - this.centre.From).Length
                                      ), 1, 360) ?? new Mesh();

        public Mesh getMesh() => this.myMesh ?? (this.myMesh = setMesh());

        private Brep myBrep;
        [NotNull]
        private Brep setBrep() => Brep.CreateFromCylinder(
                                      new Cylinder(
                                          new Circle(this.plane, this.radius),
                                          (this.centre.To - this.centre.From).Length
                                      ), true, true) ?? new Brep();

        [NotNull] public Brep getBrep() => this.myBrep ?? (this.myBrep = setBrep());
        public BoundingBox getBoundingBox()
        {
            this.myMesh = this.myMesh ?? (this.myMesh = setMesh());
            return this.myMesh.GetBoundingBox(false);
        }
    }
}
