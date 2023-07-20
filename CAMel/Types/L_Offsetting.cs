namespace CAMel.Types
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    //using ClipperLib;
    using Clipper2Lib;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    // Wrapper classes and library interfaces for leveraging the clipper
    // library for doing offsetting
    /// <summary>TODO The offsetting.</summary>
    public static class Offsetting
    {
        /// <summary>TODO The sc f.</summary>
        private const double ScF = 2000000000.0;
        /// <summary>TODO The offset.</summary>
        /// <param name="p">TODO The p.</param>
        /// <param name="d">TODO The d.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        public static List<PolylineCurve> Offset([NotNull] List<PolylineCurve> pLs, double d)
        {
            // Convert to Clipper paths
            PathsD paths = toPathsD(pLs);

            // Offset the paths.

            PathsD offset = Clipper.InflatePaths(paths, -d, JoinType.Round, EndType.Polygon,2,6);
            offset = Clipper.SimplifyPaths(offset, CAMel_Goo.Tolerance);

            return toPolylineCurves(offset, true);
        }

        public static List<Polyline> Offset([NotNull] List<Polyline> pLs, double d)
        { 
            List<PolylineCurve> pLc = new List<PolylineCurve>();
            foreach(Polyline pL in pLs) { pLc.Add(new PolylineCurve(pL)); }
            List<PolylineCurve> plc = Offset(pLc, d);
            List<Polyline> pl = new List<Polyline>();
            foreach(PolylineCurve pc in plc) { pl.Add(pc.ToPolyline()); }
            return pl;
        }

        public static List<PolylineCurve> Offset([NotNull] PolylineCurve pL, double d) => Offset(new List<PolylineCurve> { pL }, d);
        public static List<Polyline> Offset([NotNull] Polyline pL, double d) => Offset(new List<Polyline> { pL }, d);

        public static List<PolylineCurve> CleanCurve(PolylineCurve pLc)
        {
            PathsD path = toPathsD(new List<PolylineCurve> { pLc });

            path = Clipper.Union(path, new PathsD(), FillRule.NonZero,6);
            path = Clipper.SimplifyPaths(path,CAMel_Goo.Tolerance);

            return toPolylineCurves(path, true);
        }
        public static PolylineCurve CleanToLongest(PolylineCurve pLc)
        {
            List<PolylineCurve> clean = CleanCurve(pLc);
            PolylineCurve longest = clean[0];
            double length = longest.GetLength();
            for(int i=1; i<clean.Count; i++)
            {
                double newLength = clean[i].GetLength();
                if (newLength > length)
                {
                    length = newLength;
                    longest = clean[i];
                }
            }

            // Find the best start point
            double dist = 1000000000000000000;


            longest.ClosestPoint(pLc.PointAtStart, out double t, dist);
            longest.ChangeClosedCurveSeam(t);

            // Get the correct direction
            pLc.ClosestPoint(longest.PointAtStart, out t, dist);
            if(pLc.TangentAt(t)*longest.TangentAtStart < 0) { longest.Reverse(); }

            return longest;
        }

        private static PathsD toPathsD(List<PolylineCurve> pLs)
        {
            PathsD paths = new PathsD();
            foreach(PolylineCurve pLc in pLs)
            {
                pLc.TryGetPolyline(out Polyline pL);
                PathD path = new PathD();
                foreach (Point3d pt in pL) { path.Add(new PointD(pt.X, pt.Y)); }
                paths.Add(path);
            }
            return paths;
        }

        private static List<PolylineCurve> toPolylineCurves(PathsD paths, bool close)
        {
            List<PolylineCurve> pLs = new List<PolylineCurve>();
            foreach(PathD path in paths)
            {
                Polyline pl = new Polyline();
                foreach(PointD pt in path) { pl.Add(new Point3d(pt.x, pt.y,0)); }
                if (close) { pl.Add(pl[0]); }
                pLs.Add(new PolylineCurve(pl));
            }
            return pLs;
        }

        /*

        /// <summary>TODO The d toi.</summary>
        /// <param name="p">TODO The p.</param>
        /// <param name="sc">TODO The sc.</param>
        /// <returns>The <see cref="IntPoint"/>.</returns>
        private static IntPoint dToi(Point3d p, double sc) => new IntPoint((long)Math.Round(p.X * sc), (long)Math.Round(p.Y * sc));
        /// <summary>TODO The i tod.</summary>
        /// <param name="p">TODO The p.</param>
        /// <param name="sc">TODO The sc.</param>
        /// <returns>The <see cref="Point3d"/>.</returns>
        private static Point3d iTod(IntPoint p, double sc) => new Point3d(p.X / sc, p.Y / sc, 0);

        /// <summary>TODO The p lto int.</summary>
        /// <param name="pc">TODO The pc.</param>
        /// <param name="sc">TODO The sc.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        // ReSharper disable once SuggestBaseTypeForParameter
        private static List<List<IntPoint>> pLtoInt([NotNull] PolylineCurve pc, double sc)
        {
            pc.TryGetPolyline(out Polyline p);
            List<IntPoint> intP = p.Select(pt => dToi(pt, sc)).ToList();

            List<List<IntPoint>> intPl = new List<List<IntPoint>> { intP };

            return intPl;
        }

        /// <summary>TODO The int to pl.</summary>
        /// <param name="iPs">TODO The i ps.</param>
        /// <param name="sc">TODO The sc.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        private static List<PolylineCurve> intToPl([NotNull] IEnumerable<List<IntPoint>> iPs, double sc)
        {
            List<PolylineCurve> pls = new List<PolylineCurve>();

            foreach (List<IntPoint> intP in iPs.Where(x => x != null))
            {
                Polyline pl = new Polyline();
                foreach (IntPoint iP in intP) { pl.Add(iTod(iP, sc)); }
                if (intP.Count > 0) { pl.Add(iTod(intP[0], sc)); }
                pls.Add(new PolylineCurve(pl));
            }

            return pls;
        }
        */
    }
}
