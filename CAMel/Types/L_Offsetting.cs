using System;
using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.Types
{

    // Wrapper classes and library interfaces for leveraging the clipper
    // library for doing offsetting
    public static class Offsetting
    {
        private const double _ScF = 2000000000.0;
        [NotNull]
        public static List<PolylineCurve> offset([NotNull] PolylineCurve p, double d)
        {
            // Bring path into clipper, scaling to take advantage of integer arithmetic
            BoundingBox bb = p.GetBoundingBox(false);
            double md = bb.Max.X;
            if(bb.Max.Y > md) { md = bb.Max.Y; }
            if (-bb.Min.X > md) { md = -bb.Min.X; }
            if (-bb.Min.Y > md) { md = -bb.Min.Y; }
            double sc = _ScF/md;

            List<List<IntPoint>> iPs = pLtoInt(p,sc);

            // Offset the paths.

            EndType et = p.IsClosed ? EndType.etClosedPolygon : EndType.etOpenRound;

            ClipperOffset co = new ClipperOffset();
            co.AddPaths(iPs, JoinType.jtRound, et);
            List<List<IntPoint>> oPf = new List<List<IntPoint>>();
            co.Execute(ref oPf, d*sc);

            // Clean the paths.

            List<List<IntPoint>> oP = new List<List<IntPoint>>();
            foreach (List<IntPoint> iP in oPf)
            {
                List<IntPoint> clp = Clipper.CleanPolygon(iP);
                oP.Add(clp);
            }

            List<PolylineCurve> oPl = intToPl(oP,sc);
            // find point closest to first of original curve

            int cp = -1;
            double pos = 0;
            double dist = 1000000000000000000;
            for (int i = 0; i < oPl.Count; i++)
            {
                if (oPl[i] == null) { continue;}
                oPl[i].ClosestPoint(p.PointAtStart, out double t, dist);
                double di = oPl[i].PointAt(t).DistanceTo(p.PointAt(0));
                if (!(di < dist)) { continue; }

                dist = d;
                cp = i;
                pos = t;
            }

            if (cp >= 0 && oPl[cp]!=null) { oPl[cp].ChangeClosedCurveSeam(pos); }

            return oPl;
        }

        private static IntPoint dToi (Point3d p, double sc)
        {
            return new IntPoint((long)Math.Round(p.X * sc), (long)Math.Round(p.Y * sc));
        }
        private static Point3d iTod(IntPoint p, double sc)
        {
            return new Point3d(p.X/sc,p.Y/sc,0);
        }

        [NotNull]
        // ReSharper disable once SuggestBaseTypeForParameter
        private static List<List<IntPoint>> pLtoInt([NotNull] PolylineCurve pc, double sc)
        {
            List<IntPoint> intP = new List<IntPoint>();
            pc.TryGetPolyline(out Polyline p);
            foreach(Point3d pt in p) { intP.Add(dToi(pt,sc)); }
            //List<List<IntPoint>> IntPL = Clipper.SimplifyPolygon(IntP,);
            List<List<IntPoint>> intPl = new List<List<IntPoint>> { intP };

            return intPl;
        }

        [NotNull]
        private static List<PolylineCurve> intToPl([NotNull] IEnumerable<List<IntPoint>> iPs, double sc)
        {
            List<PolylineCurve> pls = new List<PolylineCurve>();

            foreach(List<IntPoint> intP in iPs.Where(x => x != null))
            {
                Polyline pl = new Polyline();
                foreach(IntPoint iP in intP) { pl.Add(iTod(iP, sc)); }
                if (intP.Count > 0) { pl.Add(iTod(intP[0], sc)); }
                pls.Add(new PolylineCurve(pl));
            }

            return pls;
        }

    }
}
