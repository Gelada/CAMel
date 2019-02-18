using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.Types
{

    // Functions to generate operations
    public static class Surfacing
    {
        [NotNull]
        public static SurfacePath parallel([CanBeNull] Curve c, Plane dir, double stepOver, bool zZ, SurfToolDir sTD, BoundingBox bb, [CanBeNull] MaterialTool mT)
        {
            if(mT == null) {Exceptions.matToolException();}

            Curve uC = c;
            if (c == null) // default to curve running along X-direction on Plane.
            { uC = new LineCurve(dir.PointAt(bb.Min.X, bb.Min.Y), dir.PointAt(bb.Max.X, bb.Min.Y)); }
            BoundingBox bbc = uC.GetBoundingBox(dir); // bounding box for curve

            List<Curve> paths = new List<Curve>(); // Curves to use
            Curve tempC = uC.DuplicateCurve();
            if(tempC == null) { throw new NullReferenceException("Rhino.Geometry.Curve.DuplicateCurve failed.");}
            tempC.Translate((Vector3d)dir.PointAt(0, bb.Min.Y - bbc.Max.Y, bb.Max.Z - bbc.Min.Z + 0.1));

            // create enough curves to guarantee covering surface

            for (double width = 0; width <= bb.Max.Y - bb.Min.Y + bbc.Max.Y - bbc.Min.Y; width = width + stepOver * mT.toolWidth)
            {
                tempC.Translate((Vector3d)dir.PointAt(0, stepOver * mT.toolWidth, 0));
                paths.Add(tempC.DuplicateCurve());
                if (zZ) { tempC.Reverse(); }
            }

            return new SurfacePath(paths, -dir.ZAxis, sTD);
        }

        [NotNull]
        public static SurfacePath helix([CanBeNull] Curve c, Plane dir, double stepOver, SurfToolDir sTD, BoundingBox bb, [CanBeNull] MaterialTool mT)
        {
            if (mT == null) { Exceptions.matToolException(); }

            double outerRadius = new Vector3d(bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y, 0).Length / 2;

            // Use Toolpath so we standardise Curve conversion
            ToolPath cTp = new ToolPath(mT);

            double zMin = 0, zMax = 0;
            int i;
            const double addAngle = 90;
            if (c == null)
            {
                for (i = 0; i < addAngle; i++)
                {
                    cTp.Add(new ToolPoint(new Point3d(outerRadius, 2 * Math.PI * i / addAngle, 0)));
                }
                zMin = zMax = 0;
            }
            else
            {
                cTp.convertCurve(c, new Vector3d(0, 0, 1));
                bool first = true;
                double turns = 0;
                double angle = 0;
                // convert to cylindrical coordinates
                foreach (ToolPoint tp in cTp)
                {
                    dir.RemapToPlaneSpace(tp.pt, out Point3d cylPt);
                    Point3d temp = CAMel_Goo.toCyl(cylPt);
                    temp.X = outerRadius;
                    tp.pt = temp;
                    if (first)
                    {
                        zMin = tp.pt.Z;
                        zMax = tp.pt.Z;
                        angle = tp.pt.Y;
                        first = false;
                    }
                    else if (tp.pt.Z < zMin) { zMin = tp.pt.Z; }
                    else if (tp.pt.Z > zMax) { zMax = tp.pt.Z; }

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
                if (cTp.firstP == null || cTp.lastP == null) { throw new NullReferenceException("SurfacePath.helix has somehow ended up with a zero length curve."); }
                Point3d startPt = cTp.firstP.pt;
                Point3d endPt = cTp.lastP.pt;

                if (endPt.Y > 0)
                { startPt.Y = startPt.Y + turns + 2.0 * Math.PI; }
                else
                { startPt.Y = startPt.Y + turns - 2.0 * Math.PI; }


                int shiftL = (int)Math.Ceiling(addAngle * Math.Abs((startPt.Y - endPt.Y) / (2.0 * Math.PI)));
                for (i = 1; i < shiftL; i++)
                {
                    cTp.Add(new ToolPoint(
                        new Point3d(outerRadius,
                            (i * startPt.Y + (shiftL - i) * endPt.Y) / shiftL,
                            (i * startPt.Z + (shiftL - i) * endPt.Z) / shiftL)
                        ));
                }

            }

            // Create spiral from the loop
            if (cTp.firstP == null || cTp.lastP == null) { throw new NullReferenceException("SurfacePath.helix has somehow ended up with a zero length curve."); }
            double winding = (cTp.lastP.pt.Y - cTp.firstP.pt.Y) / (2.0 * Math.PI);
            double raisePer = stepOver * mT.toolWidth; // height dealt with by each loop
            double rot =
                (bb.Max.Z - bb.Min.Z // eight of surface
                + (zMax - zMin) // height variation in path
                )
                / (winding * raisePer);

            raisePer = raisePer / (2.0 * Math.PI);      // convert to per radian

            List<Point3d> spiralPath = new List<Point3d>();

            for (i = -1; i <= Math.Abs(rot); i++) // strange limits to make sure we go top to bottom
            {
                foreach (ToolPoint tPt in cTp)
                {
                    Point3d tempPt = CAMel_Goo.fromCyl(new Point3d(
                        outerRadius,
                        -tPt.pt.Y,
                        bb.Min.Z - zMax + tPt.pt.Z + (2.0 * Math.PI * winding * i + tPt.pt.Y) * raisePer));
                    tempPt = dir.PointAt(tempPt.X, tempPt.Y, tempPt.Z);
                    spiralPath.Add(tempPt);
                }
            }

            List<Curve> paths = new List<Curve>
            {
                Curve.CreateInterpolatedCurve(spiralPath, 3)
            };

            LineCurve cc = new LineCurve(
                dir.PointAt(bb.Center.X, bb.Center.Y, bb.Min.Z),
                dir.PointAt(bb.Center.X, bb.Center.Y, bb.Max.Z));

            return new SurfacePath(paths, dir.ZAxis, cc, sTD);
        }

    }
}
