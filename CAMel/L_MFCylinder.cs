using System;
using System.Collections.Generic;
using Rhino.Geometry;
using CAMel.Types.Machine;

namespace CAMel.Types.MaterialForm
{
    class MFCylinder : IMaterialForm
    {
        public MFCylinder(Line cen, double radius, double matTol, double safeD)
        { this.centre = cen; this.radius = radius; this.materialTolerance = matTol; this.safeDistance = safeD; }
        public MFCylinder(Cylinder Cy, double matTol, double safeD)
        {
            this.centre = new Line(Cy.CircleAt(Cy.Height1).Center, Cy.CircleAt(Cy.Height2).Center);
            this.plane = new Plane(this.centre.From, this.centre.To - this.centre.From);
            this.height = (this.centre.To - this.centre.From).Length;
            this.radius = Cy.CircleAt(0).Radius;
            this.materialTolerance = matTol;
            this.safeDistance = safeD;
        }

        public bool IsValid { get { return true; } }

        public Line centre { get; private set; }
        public double radius { get; private set; }
        public Plane plane { get; private set; }
        public double height { get; private set; }

        public double materialTolerance { get; set; }

        public double safeDistance { get; set; }

        public string TypeDescription
        { get { return "This is a cylinder MaterialForm"; } }

        public string TypeName { get { return "CAMelMFCylinder"; } }

        public override string ToString()
        {
            Point3d end = this.plane.Origin + this.height * this.plane.ZAxis;
            return "MFCylinder r:"+this.radius.ToString()+" s:"+this.plane.Origin.ToString()+" e:"+end.ToString();
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
            return this.intersect(TP.pt, TP.dir, tolerance);
        }

        public bool intersect(Point3d start, Point3d end, double tolerance, out MFintersects inter)
        {
            return MFDefault.lineIntersect(this, start, end, tolerance, out inter);
        }

        public MFintersects intersect(Point3d PtIn, Vector3d dirIn, double tolerance)
        {
            double utol = tolerance + this.materialTolerance;

            // expand by tolerance
            double exRadius = this.radius + utol;

            // convert to Cylinder coordinates
            Point3d Pt = new Point3d();
            Vector3d dir = new Vector3d();
            this.plane.RemapToPlaneSpace((Point3d)(dirIn+this.plane.Origin), out Pt);
            dir = (Vector3d)Pt;
            dir.Unitize();
            this.plane.RemapToPlaneSpace(PtIn, out Pt);
            // give the projections of the points to the cylinder's planes
            Point3d Pt2d = Pt;
            Pt2d.Z = 0;
            Vector3d dir2d = dir;
            dir2d.Z = 0;
            double flatDist = dir2d.Length;
            // test to see where the cylinder is hit. 

            MFintersects inters = new MFintersects();
            Vector3d intPt;
            double lineP;
            double linePcen;
            double linePshift;
            double cenDist;
            // test for top and bottom

            if (dir.Z == 0) // parallel to plane
            {
                if(Pt.Z <= this.height+utol && Pt.Z >= -utol) // hits cylinder
                {
                    // Find the closest point on the line, the distance to it and so 
                    // the distance along the line from the closest point to the 
                    // cylinder
                    linePcen = (Vector3d)Pt2d * dir2d;
                    cenDist = ((Vector3d)(Pt2d - linePcen * dir2d)).Length;
                    if (cenDist < exRadius)
                    {
                        linePshift = Math.Sqrt(exRadius * exRadius - cenDist * cenDist);
                        // add the two intersection points.
                        intPt = (Vector3d)Pt + (linePshift - linePcen) * dir;
                        inters.Add(this.fromPlane((Point3d)intPt), this.fromPlane((Point3d)zeroZ(intPt)) - this.plane.Origin, linePshift - linePcen);
                        intPt = (Vector3d)Pt + (-linePshift - linePcen) * dir;
                        inters.Add(this.fromPlane((Point3d)intPt), this.fromPlane((Point3d)zeroZ(intPt)) - this.plane.Origin, -linePshift - linePcen);
                    }
                }
            } else
            {
                lineP = (-utol-Pt.Z) / dir.Z;
                intPt = (Vector3d)Pt + lineP*dir;
                if (zeroZ(intPt).Length <= exRadius) // hit bottom
                {
                    inters.Add(this.fromPlane((Point3d)intPt), -this.plane.ZAxis, lineP);
                }
                lineP = (this.height+utol-Pt.Z) / dir.Z;
                intPt = (Vector3d)Pt + lineP * dir;
                if (zeroZ(intPt).Length <= exRadius) // hit top
                {
                    inters.Add(this.fromPlane((Point3d)intPt), this.plane.ZAxis, lineP);
                }

                if(inters.Count < 2 && flatDist > 0) // not all hits top or bottom
                {
                    // Find the closest point on the line, the distance to it and so 
                    // the distance along the line from the closest point to the 
                    // cylinder
                    linePcen = (Vector3d)Pt2d * dir2d/flatDist;
                    cenDist = ((Vector3d)(Pt2d - linePcen * dir2d/flatDist)).Length;
                    linePshift = Math.Sqrt(exRadius * exRadius - cenDist * cenDist);
                    // add the two intersection points.
                    intPt = (Vector3d)Pt + (linePshift-linePcen) * dir/flatDist;
                    if (intPt.Z >= 0 && intPt.Z <= this.height)
                    {
                        inters.Add(
                            this.fromPlane((Point3d)intPt), 
                            this.fromPlane((Point3d)zeroZ(intPt)) - this.plane.Origin, 
                            (linePshift-linePcen)/flatDist);
                    }
                    intPt = (Vector3d)Pt + (-linePshift-linePcen) * dir/flatDist;
                    if (intPt.Z >= 0 && intPt.Z <= this.height)
                    {
                        inters.Add(
                            this.fromPlane((Point3d)intPt), 
                            (Vector3d)(this.fromPlane((Point3d)zeroZ(intPt))-this.plane.Origin), 
                            (-linePshift-linePcen)/flatDist);
                    }
                }
            }
            if (inters.Count > 1)
            {
                inters.midOut = this.midOutDir(inters.mid,dirIn,tolerance);
            }
            else
            {
                inters.midOut = new Vector3d();
            }
            return inters;
        }
        private Vector3d midOutDir(Point3d PtIn, Vector3d dirIn, double tolerance)
        {
            Point3d Pt = new Point3d();
            this.plane.RemapToPlaneSpace(PtIn, out Pt);
            double utol = tolerance + this.materialTolerance;
            double closeD;
            Vector3d outD;
            closeD = utol+Pt.Z; // Distance from base
            outD = -this.plane.ZAxis;
            if(closeD > this.height + utol - Pt.Z) // closer to top?
            {
                closeD = this.height + utol - Pt.Z;
                outD = this.plane.ZAxis;
            } 
            if(closeD > (this.radius + utol-((Vector3d)zeroZ(Pt)).Length)) // closer to side?
            {
                closeD = this.radius + utol - ((Vector3d)zeroZ(Pt)).Length;
                if (((Vector3d)zeroZ(Pt)).Length > 0.000001)
                {
                    outD = this.fromPlane(zeroZ(Pt)) - this.plane.Origin;
                } else
                {
                    Point3d dirP = new Point3d();
                    this.plane.RemapToPlaneSpace((Point3d)(dirIn + this.plane.Origin), out dirP);
                    outD = this.fromPlane(new Point3d(dirP.Y,-dirP.X,0))-this.plane.Origin;
                }
                outD.Unitize();
            }
            if (closeD < 0) {
                throw new FormatException("MidOutDir in MFCylinder called for point outside the Cylinder.");
            }
            return outD;
        }

        // Move a point to the Plane space
        private Point3d fromPlane(Point3d Pt)
        {
            return this.plane.PointAt(Pt.X, Pt.Y, Pt.Z);
        }

        private static Point3d zeroZ(Point3d Pt)
        {
            return new Point3d(Pt.X, Pt.Y, 0);
        }
        private static Vector3d zeroZ(Vector3d Pt)
        {
            return new Vector3d(Pt.X, Pt.Y, 0);
        }

        public ToolPath refine(ToolPath TP, IMachine M)
        {
            return MFDefault.refine(this, TP, M);
        }

        public bool getBrep(ref object brep)
        {
            if (this.IsValid)
            {
                brep = new Cylinder(
                    new Circle(this.plane, this.radius), 
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
                    new Circle(this.plane, this.radius),
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
                    new Circle(this.plane, this.radius),
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
                    new Circle(this.plane, this.radius),
                    (this.centre.To - this.centre.From).Length
                    ),
                    1, 360);
                return true;
            }
            else { return false; }
        }
    }

}
