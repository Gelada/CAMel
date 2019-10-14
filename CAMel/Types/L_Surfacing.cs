namespace CAMel.Types
{
    using System;
    using System.Collections.Generic;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    // Functions to generate operations
    public static class Surfacing
    {
        [NotNull]
        public static SurfacePath parallel([CanBeNull] Curve c, Plane dir, double stepOver, bool zZ, SurfToolDir sTD, BoundingBox bb, [CanBeNull] MaterialTool mT)
        {
            if (mT == null) { Exceptions.matToolException(); }

            Curve uC = c;
            if (c == null) // default to curve running along X-direction on Plane.
            { uC = new LineCurve(dir.PointAt(bb.Min.X, bb.Min.Y), dir.PointAt(bb.Max.X, bb.Min.Y)); }
            BoundingBox bbc = uC.GetBoundingBox(dir); // bounding box for curve

            List<Curve> paths = new List<Curve>(); // Curves to use
            Curve tempC = uC.DuplicateCurve();
            if (tempC == null) { throw new NullReferenceException("Rhino.Geometry.Curve.DuplicateCurve failed."); }
            tempC.Translate((Vector3d) dir.PointAt(0, bb.Min.Y - bbc.Max.Y, bb.Max.Z - bbc.Min.Z + 0.1));

            // create enough curves to guarantee covering surface

            for (double width = 0; width <= bb.Max.Y - bb.Min.Y + bbc.Max.Y - bbc.Min.Y; width += stepOver * mT.toolWidth)
            {
                tempC.Translate((Vector3d) dir.PointAt(0, stepOver * mT.toolWidth, 0));
                paths.Add(tempC.DuplicateCurve());
                if (zZ) { tempC.Reverse(); }
            }
            // Join paths for zigzag
            if (!zZ) { return new SurfacePath(paths, mT, -dir.ZAxis, sTD); }

            PolyCurve joined = new PolyCurve();
            joined.Append(paths[0]);
            for (int i = 1; i < paths.Count; i++)
            {
                Curve join = Curve.CreateBlendCurve(joined, paths[i], BlendContinuity.Position);
                joined.Append(join);
                joined.Append(paths[i]);
            }

            paths = new List<Curve> {joined};

            return new SurfacePath(paths, mT, -dir.ZAxis, sTD);
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
            const double Angle = 90;
            if (c == null)
            {
                for (i = 0; i < Angle; i++)
                {
                    cTp.Add(new ToolPoint(new Point3d(outerRadius, 2 * Math.PI * i / Angle, 0)));
                }

                zMin = zMax = 0;
            }
            else
            {
                cTp.convertCurve(c, new Vector3d(0, 0, 1), 1);
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
                        turns += 2.0 * Math.PI;
                    }
                    else if (angle < Math.PI / 2.0 && tp.pt.Y > 3.0 * Math.PI / 2.0)
                    {
                        turns -= 2.0 * Math.PI;
                    }
                    angle = tp.pt.Y;
                    temp = tp.pt;
                    temp.Y += turns;
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

                int shiftL = (int) Math.Ceiling(Angle * Math.Abs((startPt.Y - endPt.Y) / (2.0 * Math.PI)));
                for (i = 1; i < shiftL; i++)
                {
                    cTp.Add(new ToolPoint(
                        new Point3d(outerRadius,
                            (i * startPt.Y + (shiftL - i) * endPt.Y) / shiftL,
                            (i * startPt.Z + (shiftL - i) * endPt.Z) / shiftL)
                    ));
                }
            }

            // Create helix from the loop
            if (cTp.firstP == null || cTp.lastP == null) { throw new NullReferenceException("SurfacePath.helix has somehow ended up with a zero length curve."); }
            double winding = (cTp.lastP.pt.Y - cTp.firstP.pt.Y) / (2.0 * Math.PI);
            double raisePer = stepOver * mT.toolWidth; // height dealt with by each loop
            double roth =
                bb.Max.Z - bb.Min.Z // height of surface
                + zMax - zMin; // height variation in path
            double rot = roth / (winding * raisePer);

            raisePer /= 2.0 * Math.PI; // convert to per radian

            List<Point3d> spiralPath = new List<Point3d>();
            for (i = -1; i <= Math.Abs(rot + 1); i++) // strange limits to make sure we go top to bottom
            {
                foreach (ToolPoint tPt in cTp)
                {
                    double h = (2.0 * Math.PI * winding * i + tPt.pt.Y) * raisePer;
                    if (h < 0) { h = 0; } // do a first loop on the top
                    if (h > roth) { h = roth; } // do the final loop at the bottom
                    h = h + bb.Min.Z - zMax + tPt.pt.Z;
                    Point3d tempPt = CAMel_Goo.fromCyl(new Point3d(
                        outerRadius,
                        -tPt.pt.Y,
                        h));
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

            return new SurfacePath(paths, mT, dir.ZAxis, cc, sTD);
        }
        // TODO make work for planes and with a boundary curve to spiral to
        [NotNull]
        public static SurfacePath spiral([CanBeNull] Curve c, Plane dir, double r, double stepOver, SurfToolDir sTd, BoundingBox bb, [NotNull] MaterialTool mT)
        {
            double raisePer = stepOver * mT.toolWidth;
            // find radius of sphere containing bounding box centered on origin.

            double radius = CAMel_Goo.boundSphere(bb, Point3d.Origin);

            List<Point3d> spiralPath = new List<Point3d>();
            double h = radius;
            double th = 0;
            double spiralRatio = r * Math.PI / raisePer;
            double st = 10.0 / spiralRatio * (Math.PI / 180.0);

            // Apply spherical spiral
            while (h > bb.Min.Z)
            {
                h = radius * Math.Cos(th);
                double temp = radius * Math.Sin(th);
                spiralPath.Add(dir.PointAt(temp * Math.Cos(spiralRatio * th), temp * Math.Sin(spiralRatio * th), h));
                th += st;
            }

            // Make a final loop at the bottom
            for (double thl = 0; thl < 2 * Math.PI; thl += st)
            {
                double temp = radius * Math.Sin(th);
                spiralPath.Add(dir.PointAt(temp * Math.Cos(spiralRatio * th + thl), temp * Math.Sin(spiralRatio * th + thl), h));
            }

            List<Curve> paths = new List<Curve>
                {
                    Curve.CreateInterpolatedCurve(spiralPath, 3)
                };

            return new SurfacePath(paths, mT, dir.Origin, sTd);
        }
    }
}
