using System;
using System.Collections.Generic;

using Rhino.Geometry;

using CAMel.Types.MaterialForm;

namespace CAMel.Types
{

    // Functions to generate operations
    public static class Surfacing
    {
        public static SurfacePath parallel(Curve C, Plane Dir, double stepOver, bool zZ, SurfToolDir sTD, BoundingBox BB, MaterialTool MT)
        {
            Curve uC = C;
            if (C == null) // default to curve running along X-direction on Plane. 
            { uC = new LineCurve(Dir.PointAt(BB.Min.X, BB.Min.Y), Dir.PointAt(BB.Max.X, BB.Min.Y)); }
            BoundingBox BBC = C.GetBoundingBox(Dir); // bounding box for curve

            List<Curve> Paths = new List<Curve>(); // Curves to use
            Curve TempC = C.DuplicateCurve();
            TempC.Translate((Vector3d)Dir.PointAt(0, BB.Min.Y - BBC.Max.Y, BB.Max.Z - BBC.Min.Z + 0.1));

            // create enough curves to guarantee covering surface

            for (double width = 0; width <= BB.Max.Y - BB.Min.Y + BBC.Max.Y - BBC.Min.Y; width = width + stepOver * MT.toolWidth)
            {
                TempC.Translate((Vector3d)Dir.PointAt(0, stepOver * MT.toolWidth, 0));
                Paths.Add(TempC.DuplicateCurve());
                if (zZ) { TempC.Reverse(); }
            }

            return new SurfacePath(Paths, -Dir.ZAxis, sTD);
        }

        public static SurfacePath helix(Curve C, Plane Dir, double stepOver, SurfToolDir sTD, BoundingBox BB, MaterialTool MT)
        {

            double outerradius = (new Vector3d(BB.Max.X - BB.Min.X, BB.Max.Y - BB.Min.Y, 0)).Length / 2;
            Cylinder Cy = new Cylinder(new Circle(Dir, outerradius))
            {
                Height1 = BB.Min.Z,
                Height2 = BB.Max.Z
            };

            // Use Toolpath so we standardise Curve convertion
            ToolPath CTP = new ToolPath(MT);

            double Zmin = 0, Zmax = 0;
            int i;
            double addangle = 90;
            if (C == null)
            {
                for (i = 0; i < addangle; i++)
                {
                    CTP.Add(new ToolPoint(new Point3d(outerradius, 2 * Math.PI * i / addangle, 0)));
                }
                Zmin = Zmax = 0;
            }
            else
            {
                CTP.convertCurve(C, new Vector3d(0, 0, 1));
                Point3d CylPt = new Point3d();
                bool first = true;
                double turns = 0;
                double angle = 0;
                // convert to cylindrical coordinates
                foreach (ToolPoint tp in CTP)
                {
                    Dir.RemapToPlaneSpace(tp.pt, out CylPt);
                    Point3d temp = CAMel_Goo.toCyl(CylPt);
                    temp.X = outerradius;
                    tp.pt = temp;
                    if (first)
                    {
                        Zmin = tp.pt.Z;
                        Zmax = tp.pt.Z;
                        angle = tp.pt.Y;
                        first = false;
                    }
                    else if (tp.pt.Z < Zmin) { Zmin = tp.pt.Z; }
                    else if (tp.pt.Z > Zmax) { Zmax = tp.pt.Z; }

                    if (angle > 3.0 * Math.PI / 2.0 && tp.pt.Y < Math.PI / 2.0)
                    {
                        turns = turns + 2.0 * Math.PI;
                    }
                    else if (angle < Math.PI / 2.0 && tp.pt.Y > 3.0 * Math.PI / 2.0)
                    {
                        turns = turns - 2.0 * Math.PI;
                    }
                    angle = tp.pt.Y;
                    temp = tp.pt;
                    temp.Y = temp.Y + turns;
                    tp.pt = temp;
                }

                // complete loop by adding points going from
                // the end point to the start point
                Point3d startPt = CTP.firstP.pt;
                Point3d endPt = CTP.lastP.pt;
                if (endPt.Y > 0)
                { startPt.Y = startPt.Y + turns + 2.0 * Math.PI; }
                else
                { startPt.Y = startPt.Y + turns - 2.0 * Math.PI; }


                int shiftl = (int)Math.Ceiling(addangle * Math.Abs((startPt.Y - endPt.Y) / (2.0 * Math.PI)));
                for (i = 1; i < shiftl; i++)
                {
                    CTP.Add(new ToolPoint(
                        new Point3d(outerradius,
                            (i * startPt.Y + (shiftl - i) * endPt.Y) / shiftl,
                            (i * startPt.Z + (shiftl - i) * endPt.Z) / shiftl)
                        ));
                }

            }

            // Create spiral from the loop
            double winding = (CTP.lastP.pt.Y - CTP.firstP.pt.Y) / (2.0 * Math.PI);
            double raisePer = (stepOver * MT.toolWidth); // height dealt with by each loop
            double rot =
                ((BB.Max.Z - BB.Min.Z) // eight of surface
                + (Zmax - Zmin) // height variation in path
                )
                / (winding * raisePer);

            raisePer = raisePer / (2.0 * Math.PI);      // convert to per radian

            List<Point3d> SpiralPath = new List<Point3d>();

            Point3d tempPt;
            for (i = -1; i <= Math.Abs(rot); i++) // strange limits to make sure we go top to bottom
            {
                for (int j = 0; j < CTP.Count; j++)
                {
                    tempPt = CAMel_Goo.fromCyl(new Point3d(
                        outerradius,
                        -CTP[j].pt.Y,
                        BB.Min.Z - Zmax + CTP[j].pt.Z + (2.0 * Math.PI * winding * i + CTP[j].pt.Y) * raisePer));
                    tempPt = Dir.PointAt(tempPt.X, tempPt.Y, tempPt.Z);
                    SpiralPath.Add(tempPt);
                }
            }

            List<Curve> Paths = new List<Curve>
            {
                Curve.CreateInterpolatedCurve(SpiralPath, 3)
            };

            LineCurve CC = new LineCurve(
                Dir.PointAt(BB.Center.X, BB.Center.Y, BB.Min.Z),
                Dir.PointAt(BB.Center.X, BB.Center.Y, BB.Max.Z));

            return new SurfacePath(Paths, Dir.ZAxis, CC, sTD);
        }

    }
}
