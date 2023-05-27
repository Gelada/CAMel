namespace CAMel.Types
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Rhino.Geometry;
    using Rhino.Geometry.Intersect;

    // Functions to generate operations
    /// <summary>TODO The surfacing.</summary>
    public static class Surfacing
    {
        /// <summary>TODO The parallel.</summary>
        /// <param name="c">TODO The c.</param>
        /// <param name="dir">TODO The dir.</param>
        /// <param name="stepOver">TODO The step over.</param>
        /// <param name="zZ">TODO The z z.</param>
        /// <param name="sTD">TODO The s td.</param>
        /// <param name="bb">TODO The bb.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <returns>The <see cref="SurfacePath"/>.</returns>
        /// <exception cref="NullReferenceException"></exception>
        [NotNull]
        public static SurfacePath parallel([CanBeNull] Curve c, Plane dir, double stepOver, bool zZ, SurfToolDir sTD, Curve boundary, [CanBeNull] MaterialTool mT)
        {
            if (mT == null) { Exceptions.matToolException(); }

            BoundingBox bb = boundary.GetBoundingBox(dir);

            Curve uC = c;
            if (c == null) // default to curve running along X-direction on Plane.
            {
                uC = new LineCurve(dir.PointAt(bb.Min.X, bb.Min.Y), dir.PointAt(bb.Max.X, bb.Min.Y));
            }

            uC.Transform(Transform.PlanarProjection(dir));

            BoundingBox bbc = uC.GetBoundingBox(dir); // bounding box for curve

            List<Curve> paths = new List<Curve>(); // Curves to use
            Curve tempC = uC.DuplicateCurve();
            if (tempC == null) { throw new NullReferenceException("Rhino.Geometry.Curve.DuplicateCurve failed."); }
            tempC.Translate((Vector3d)dir.PointAt(bb.Min.X-bbc.Min.X, bb.Min.Y - bbc.Max.Y, 0));

            // create enough curves to guarantee covering surface
            for (double width = 0; width <= bb.Max.Y - bb.Min.Y + bbc.Max.Y - bbc.Min.Y; width += stepOver * mT.toolWidth)
            {
                tempC.Translate((Vector3d)dir.PointAt(0, stepOver * mT.toolWidth, 0));
                paths.Add(tempC.DuplicateCurve());
                if (zZ) { tempC.Reverse(); }
            }

            // cut curves by boundary
            (Curve C, double s, double e)[] cutPaths = new (Curve, double, double)[paths.Count];
            //Parallel.For(0, paths.Count, index =>
            for(int index = 0; index < paths.Count; index++)
            {
                Curve path = paths[index].DuplicateCurve();
                CurveIntersections cI = Intersection.CurveCurve(path, boundary, mT.tolerance, mT.tolerance);
                if (cI.Count > 1)
                {
                    path = path.Trim(cI[0].ParameterA, cI[cI.Count - 1].ParameterA);
                    cutPaths[index] = (path, cI[0].ParameterB, cI[cI.Count - 1].ParameterB);
                }
            };

            if (!zZ) 
            {
                paths = new List<Curve>();
                foreach(var cv in cutPaths) { if (cv.C != null) {
                        cv.C.Translate((Vector3d)dir.PointAt(0,0,bb.Max.Z - bbc.Min.Z +.1));
                        paths.Add(cv.C); 
                    } }
                return new SurfacePath(paths, mT, -dir.ZAxis, sTD); 
            }

            // Join paths for zigzag
            PolyCurve joined = new PolyCurve();
            double endv = 0;
            bool connect = false;
            var cpaths = new List<Curve>();
            for (int i = 0; i < paths.Count; i++)
            {
                if (cutPaths[i].C != null)
                {
                    if(connect)
                    {
                        double len = (endv - cutPaths[i].s);
                        double blen = boundary.Domain.Length;
                        if ((len <= blen/2.0 && len >= 0) || (len >= blen / 2.0 && len <= 0))
                        {
                            Curve cb = boundary.Trim(cutPaths[i].s, endv);
                            cb.Reverse();
                            joined.Append(cb);
                        } else
                        {
                            Curve cb = boundary.Trim(endv, cutPaths[i].s);
                            joined.Append(cb);

                        }
                    }

                    joined.Append(cutPaths[i].C);
                    connect = true;
                    endv = cutPaths[i].e;
                }
            }
            joined.Translate((Vector3d)dir.PointAt(0, 0, bb.Max.Z - bbc.Min.Z + .1));
            paths = new List<Curve> { joined };

            return new SurfacePath(paths, mT, -dir.ZAxis, sTD);
        }

        /// <summary>Create a surface path recipe as zig zag on a cylinder round an object. </summary>
        /// <param name="c">Curve up the cylinder to turn into zigzag.</param>
        /// <param name="dir">Plane for base of cylinder. </param>
        /// <param name="stepOver">Stepover between neighboring curves. </param>
        /// <param name="sTD">The <see cref="SurfToolDir"/> describing the tool direction. </param>
        /// <param name="bb">The bounding box for the area to mill.</param>
        /// <param name="mT">The <see cref="MaterialTool"/> describing the material and tool used.</param>
        /// <returns>The <see cref="SurfacePath"/> generated.</returns>
        /// <exception cref="NullReferenceException"></exception>
        [NotNull]
        public static SurfacePath parallelCylinder([CanBeNull] Curve c, Plane dir, double stepOver, SurfToolDir sTD, BoundingBox bb, [CanBeNull] MaterialTool mT)
        {
            if (mT == null) { Exceptions.matToolException(); }

            double outerRadius = new Vector3d(bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y, 0).Length / 2;

            Curve uC;

            if (c == null)
            {
                Point3d start = dir.PointAt(outerRadius, 0, bb.Min.Z);
                Point3d end = dir.PointAt(outerRadius, 0, bb.Max.Z);
                uC = new LineCurve(start, end);
            }
            else { uC = c; }

            List<Curve> paths = new List<Curve>();



            LineCurve cc = new LineCurve(
                dir.PointAt(bb.Center.X, bb.Center.Y, bb.Min.Z),
                dir.PointAt(bb.Center.X, bb.Center.Y, bb.Max.Z));

            return new SurfacePath(paths, mT, dir.ZAxis, cc, sTD);
        }

        /// <summary>Create a surface path recipe as a helix on a cylinder round an object. </summary>
        /// <param name="c">Curve round the cylinder to turn into helix.</param>
        /// <param name="dir">Plane for base of cylinder. </param>
        /// <param name="stepOver">Stepover between neighboring curves. </param>
        /// <param name="sTD">The <see cref="SurfToolDir"/> describing the tool direction. </param>
        /// <param name="bb">The bounding box for the area to mill.</param>
        /// <param name="mT">The <see cref="MaterialTool"/> describing the material and tool used.</param>
        /// <returns>The <see cref="SurfacePath"/> generated.</returns>
        /// <exception cref="NullReferenceException"></exception>
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

                int shiftL = (int)Math.Ceiling(Angle * Math.Abs((startPt.Y - endPt.Y) / (2.0 * Math.PI)));
                for (i = 1; i < shiftL; i++)
                {
                    cTp.Add(
                        new ToolPoint(
                            new Point3d(
                                outerRadius,
                                (i * startPt.Y + (shiftL - i) * endPt.Y) / shiftL,
                                (i * startPt.Z + (shiftL - i) * endPt.Z) / shiftL)));
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
                    Point3d tempPt = CAMel_Goo.fromCyl(
                        new Point3d(
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
        /// <summary>TODO The spiral.</summary>
        /// <param name="c">TODO The c.</param>
        /// <param name="dir">TODO The dir.</param>
        /// <param name="r">TODO The r.</param>
        /// <param name="stepOver">TODO The step over.</param>
        /// <param name="sTd">TODO The s td.</param>
        /// <param name="bb">TODO The bb.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <returns>The <see cref="SurfacePath"/>.</returns>
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
            while (h > bb.Min.Z + 0.0001)
            {
                h = radius * Math.Cos(th);
                double temp = radius * Math.Sin(th);
                spiralPath.Add(dir.PointAt(temp * Math.Cos(spiralRatio * th), temp * Math.Sin(spiralRatio * th), h));
                th += st;
                h = radius * Math.Cos(th);
            }

            // Make a final loop at the bottom
            for (double thl = 0; thl < 2 * Math.PI; thl += st)
            {
                double temp = radius * Math.Sin(th);
                spiralPath.Add(dir.PointAt(temp * Math.Cos(spiralRatio * th + thl), temp * Math.Sin(spiralRatio * th + thl), bb.Min.Z+0.0001));
            }

            List<Curve> paths = new List<Curve>
                {
                    Curve.CreateInterpolatedCurve(spiralPath, 3)
                };

            return new SurfacePath(paths, mT, dir.Origin, sTd);
        }
    }
}
