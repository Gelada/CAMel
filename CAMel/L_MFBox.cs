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

        public ICAMel_Base Duplicate()
        {
            return (ICAMel_Base) this.MemberwiseClone();
        }

        public ToolPath InsertRetract(ToolPath TP)
        {
            ToolPath irTP = new ToolPath(TP);
            irTP.Additions.insert = false;
            irTP.Additions.retract = false;

            intersection inter;

            // check if we have something to do
            if (TP.Additions.insert && irTP.Pts.Count > 0) // add insert
            {
                //note we do this backwards adding points to the start of the path.

                // get distance to surface and insert direction
                inter = this.intersect(irTP.Pts[0], 0).through;

                // point on material surface

                ToolPoint tempTP = new ToolPoint(irTP.Pts[0]);
                tempTP.Pt = tempTP.Pt + tempTP.Dir * inter.lineP;
                tempTP.feed = TP.MatTool.feedPlunge;
                irTP.Pts.Insert(0, tempTP);

                // point out at safe distance

                tempTP = new ToolPoint(irTP.Pts[0]);
                tempTP.Pt = tempTP.Pt + inter.Away * (this.safeDistance);
                tempTP.feed = 0; // we can use a rapid move
                irTP.Pts.Insert(0, tempTP);
            }
            if (TP.Additions.retract && irTP.Pts.Count > 0) // add retract
            {
                // get distance to surface and retract direction
                inter = this.intersect(irTP.Pts[irTP.Pts.Count - 1], 0).through;

                ToolPoint tempTP = new ToolPoint(irTP.Pts[irTP.Pts.Count - 1]);

                // set speed to the plunge feed rate.
                tempTP.feed = TP.MatTool.feedPlunge;

                // Pull back to surface
                tempTP.Pt = tempTP.Pt + tempTP.Dir * inter.lineP;
                tempTP.feed = 0; // we can use a rapid move

                irTP.Pts.Add(tempTP);

                // Pull away to safe distance

                tempTP = new ToolPoint(irTP.Pts[irTP.Pts.Count - 1]);
                tempTP.Pt = tempTP.Pt + inter.Away * (this.safeDistance);
                tempTP.feed = 0; // we can use a rapid move
                irTP.Pts.Add(tempTP);
            }
            return irTP;
        }

        public intersects intersect(ToolPoint TP, double tolerance)
        {
            return this.intersect(TP.Pt,-TP.Dir, tolerance);
        }

        public intersects intersect(Point3d Pt, Vector3d direction, double tolerance)
        {
            throw new NotImplementedException();
        }
        // test the X faces, for other faces reorder point and direction.

        private bool testFace(
            Interval tdi, Interval odi1, Interval odi2, 
            Point3d Pt, Vector3d Dir, 
            out double dist)
        {
            double intDist;
            double shift;

            if (Dir.X > 0)
            {
                shift = (tdi.Max - Pt.X) / (Pt.X);
                intDist = shift * Dir.Length;
            } else if (Dir.X < 0)
            {
                shift = (tdi.Min - Pt.X) / (Pt.X);
                intDist = shift * Dir.Length;
            } else // parallel
            {
                dist = 0;
                return false;
            }
            Vector3d inter = (Vector3d)(Pt + (shift * Dir));
            if( odi1.Min < inter.Y && 
                inter.Y < odi1.Max &&
                odi2.Min < inter.Z && 
                inter.Z < odi2.Max) // hit plane
            {
                dist = intDist;
                return true;
            }
            dist = 0;
            return false;
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
