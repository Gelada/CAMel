using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace CAMel.Types.MaterialForm
{
    class MFCylinder : IMaterialForm
    {
        public MFCylinder(Line cen, double radius, double matTol, double safeD)
        { this.centre = cen; this.radius = radius; this.materialTolerance = matTol; this.safeDistance = safeD; }
        public MFCylinder(Cylinder Cy, double matTol, double safeD)
        {
            this.centre = new Line(Cy.CircleAt(Cy.Height1).Center, Cy.CircleAt(Cy.Height2).Center);
            this.radius = Cy.CircleAt(0).Radius;
            this.materialTolerance = matTol;
            this.safeDistance = safeD;
        }

        public bool IsValid { get { return true; } }

        public Line centre {
            get { return this.centre; }
            private set
            {
                this.centre = value;
                this.Pl = new Plane(this.centre.From, this.centre.To - this.centre.From);
                this.H = (this.centre.To - this.centre.From).Length;
            }
        }
        public double radius { get; private set; }
        public Plane Pl { get; private set; }
        public double H { get; private set; }

        public double materialTolerance { get; set; }

        public double safeDistance { get; set; }

        public string TypeDescription
        { get { return "This is a cylinder MaterialForm"; } }

        public string TypeName { get { return "CAMelMFCylinder"; } }

        public ICAMel_Base Duplicate()
        {
            return (ICAMel_Base)this.MemberwiseClone();
        }

        public ToolPath InsertRetract(ToolPath TP)
        {
            return MFDefault.InsertRetract(this, TP);
        }

        public intersects intersect(ToolPoint TP, double tolerance)
        {
            return this.intersect(TP.Pt, -TP.Dir, tolerance);
        }

        public bool intersect(Point3d start, Point3d end, double tolerance, out intersects inter)
        {
            return MFDefault.lineIntersect(this, start, end, tolerance, out inter);
        }

        public intersects intersect(Point3d PtIn, Vector3d dirIn, double tolerance)
        {
            dirIn.Unitize();
            Line exCen = this.centre;
            // expand by tolerance
            exCen.Extend(tolerance, tolerance);
            double exRadius = this.radius + tolerance + this.materialTolerance;

            // convert to Box coordinates
            Point3d Pt = new Point3d();
            Vector3d dir = new Vector3d();
            this.Pl.RemapToPlaneSpace((Point3d)dirIn, out Pt);
            dir = (Vector3d)Pt;
            this.Pl.RemapToPlaneSpace(PtIn, out Pt);
            // give the projections of the points to the cylinder's plane
            Point3d Pt2d = Pt;
            Pt2d.Z = 0;
            Vector3d dir2d = dir;
            dir2d.Z = 0;

            // test to see where the cylinder is hit. 

            intersects inters = new intersects();
            Vector3d intPt;
            double lineP;
            double linePcen;
            double linePshift;
            double cenDist;
            // test for top and bottom

            if (dir.Z == 0) // parallel to plane
            {
                if(Pt.Z <= this.H && Pt.Z >= 0) // hits cylinder
                {
                    // Find the closest point on the line, the distance to it and so 
                    // the distance along the line from the closest point to the 
                    // cylinder
                    linePcen = (Vector3d)Pt2d * dir2d;
                    cenDist = ((Vector3d)(Pt2d + linePcen * dir2d)).Length;
                    linePshift = Math.Sqrt(exRadius * exRadius - cenDist*cenDist);
                    // add the two intersection points.
                    intPt = (Vector3d)Pt + (linePcen + linePshift) * dir;
                    inters.Add(this.fromPlane((Point3d)intPt),(Vector3d)this.fromPlane((Point3d)zeroZ(intPt)),linePcen+linePshift);
                    intPt = (Vector3d)Pt + (linePcen - linePshift) * dir;
                    inters.Add(this.fromPlane((Point3d)intPt), (Vector3d)this.fromPlane((Point3d)zeroZ(intPt)), linePcen - linePshift);
                }
            } else
            {
                lineP = (-tolerance-Pt.Z / dir.Z);
                intPt = (Vector3d)Pt + lineP*dir;
                if (new Vector3d(intPt.X, intPt.Y, 0).Length <= exRadius) // hit bottom
                {
                    inters.Add(this.fromPlane((Point3d)intPt), -this.Pl.ZAxis, lineP);
                }
                lineP = (this.H+tolerance-Pt.Z / dir.Z);
                intPt = (Vector3d)Pt + lineP * dir;
                if (new Vector3d(intPt.X,intPt.Y,0).Length <= exRadius) // hit top
                {
                    inters.Add((Point3d)intPt, this.Pl.ZAxis, lineP);
                }

                if(inters.Count < 2) // not all hits top or bottom
                {
                    // Find the closest point on the line, the distance to it and so 
                    // the distance along the line from the closest point to the 
                    // cylinder
                    linePcen = (Vector3d)Pt2d * dir2d;
                    cenDist = ((Vector3d)(Pt2d + linePcen * dir2d)).Length;
                    linePshift = Math.Sqrt(exRadius * exRadius - cenDist * cenDist);
                    // add the two intersection points.
                    intPt = (Vector3d)Pt + (linePcen + linePshift) * dir;
                    if (intPt.Z >= 0 && intPt.Z <= this.H)
                    {
                        inters.Add(this.fromPlane((Point3d)intPt), (Vector3d)this.fromPlane((Point3d)zeroZ(intPt)), linePcen + linePshift);
                    }
                    intPt = (Vector3d)Pt + (linePcen - linePshift) * dir;
                    if (intPt.Z >= 0 && intPt.Z <= this.H)
                    {
                        inters.Add(this.fromPlane((Point3d)intPt), (Vector3d)this.fromPlane((Point3d)zeroZ(intPt)), linePcen - linePshift);
                    }
                }
            }
            return inters;
        }

        private Point3d fromPlane(Point3d Pt)
        {
            return this.Pl.PointAt(Pt.X, Pt.Y, Pt.Z);
        }
        private static Point3d zeroZ(Point3d Pt)
        {
            return new Point3d(Pt.X, Pt.Y, 0);
        }
        private static Vector3d zeroZ(Vector3d Pt)
        {
            return new Vector3d(Pt.X, Pt.Y, 0);
        }

        public ToolPath refine(ToolPath TP, Machine M)
        {
            return MFDefault.refine(this, TP, M);
        }

        public bool getBrep(ref object brep)
        {
            if (this.IsValid)
            {
                brep = new Cylinder(
                    new Circle(this.Pl, this.radius), 
                    (this.centre.To - this.centre.From).Length
                    ).ToBrep(true,true);
                return true;
            }
            else { return false; }
        }

        public bool getBrep(ref Brep brep)
        {
            if (this.IsValid)
            {
                brep = new Cylinder(
                    new Circle(this.Pl, this.radius),
                    (this.centre.To - this.centre.From).Length
                    ).ToBrep(true, true);
                return true;
            }
            else { return false; }
        }

        public bool getMesh(ref object mesh)
        {
            if (this.IsValid)
            {
                mesh = Mesh.CreateFromCylinder(
                    new Cylinder(
                    new Circle(this.Pl, this.radius),
                    (this.centre.To - this.centre.From).Length
                    ),
                    1, 360);
                return true;
            }
            else { return false; }

        }

        public bool getMesh(ref Mesh mesh)
        {
            if (this.IsValid)
            {
                mesh = Mesh.CreateFromCylinder(
                    new Cylinder(
                    new Circle(this.Pl, this.radius),
                    (this.centre.To - this.centre.From).Length
                    ),
                    1, 360);
                return true;
            }
            else { return false; }
        }
    }

}
