using System;
using System.Collections.Generic;

using Rhino.Geometry;

using ClipperLib;

namespace CAMel.Types
{

    // Wrapper clases and library interfaces for leveraging the clipper
    // library for doing offsetting
    public static class Offsetting
    {
        private const double _scF = 2000000000.0;
        public static List<PolylineCurve> offset(PolylineCurve p, double d)
        {
            // Bring path into clipper, scaling to take advantage of interger arithmetic
            BoundingBox bb = p.GetBoundingBox(false);
            double md = bb.Max.X;
            if(bb.Max.Y > md) { md = bb.Max.Y; }
            if (-bb.Min.X > md) { md = -bb.Min.X; }
            if (-bb.Min.Y > md) { md = -bb.Min.Y; }
            double sc = _scF/md;

            List<List<IntPoint>> iPs = pLtoInt(p,sc);

            // Offset the paths.

            EndType et;
            if(p.IsClosed) { et = EndType.etClosedPolygon; }
            else { et = EndType.etOpenRound; }

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
            double di,t, dist = 1000000000000000000;
            for (int i = 0; i < oPl.Count; i++)
            {
                oPl[i].ClosestPoint(p.PointAtStart, out t, dist);
                di = oPl[i].PointAt(t).DistanceTo(p.PointAt(0));
                if(di<dist)
                {
                    dist = d;
                    cp = i;
                    pos = t;
                }
            }

            if (cp >= 0) { oPl[cp].ChangeClosedCurveSeam(pos); }

            return oPl;
        }

        private static IntPoint dtoi (Point3d p, double sc)
        {
            return new IntPoint((long)Math.Round(p.X * sc), (long)Math.Round(p.Y * sc));
        }
        private static Point3d itod(IntPoint p, double sc)
        {
            return new Point3d(p.X/sc,p.Y/sc,0);
        }

        private static List<List<IntPoint>> pLtoInt(PolylineCurve pc, double sc)
        {
            List<IntPoint> intP = new List<IntPoint>();
            Polyline P;
            pc.TryGetPolyline(out P);
            foreach(Point3d p in P) { intP.Add(dtoi(p,sc)); }
            //List<List<IntPoint>> IntPL = Clipper.SimplifyPolygon(IntP,);
            List<List<IntPoint>> intPl = new List<List<IntPoint>> { intP };

            return intPl;
        }
        private static List<List<IntPoint>> pLtoInt(List<PolylineCurve> p, double sc)
        {
            List<List<IntPoint>> intPl = new List<List<IntPoint>>();
            foreach(PolylineCurve pl in p) { intPl.AddRange(pLtoInt(pl, sc)); }

            return intPl;
        }

        private static List<PolylineCurve> intToPl(List<List<IntPoint>> iPs, double sc)
        {
            List<PolylineCurve> PL = new List<PolylineCurve>();
            Polyline P;

            foreach(List<IntPoint> intP in iPs)
            {
                P = new Polyline();
                foreach(IntPoint iP in intP) { P.Add(itod(iP, sc)); }
                if (intP.Count > 0) { P.Add(itod(intP[0], sc)); }
                PL.Add(new PolylineCurve(P));
            }

            return PL;
        }

    }
}
