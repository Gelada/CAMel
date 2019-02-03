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
        private const double scF = 2000000000.0;
        public static List<PolylineCurve> offset(PolylineCurve P, double d)
        {
            // Bring path into clipper, scaling to take advantage of interger arithmetic
            BoundingBox BB = P.GetBoundingBox(false);
            double md = BB.Max.X;
            if(BB.Max.Y > md) { md = BB.Max.Y; }
            if (-BB.Min.X > md) { md = -BB.Min.X; }
            if (-BB.Min.Y > md) { md = -BB.Min.Y; }
            double sc = scF/md;

            List<List<IntPoint>> IP = pLtoInt(P,sc);

            // Offset the paths.

            EndType et;
            if(P.IsClosed) { et = EndType.etClosedPolygon; }
            else { et = EndType.etOpenRound; }

            ClipperOffset co = new ClipperOffset();
            co.AddPaths(IP, JoinType.jtRound, et);
            List<List<IntPoint>> oPf = new List<List<IntPoint>>();
            co.Execute(ref oPf, d*sc);

            // Clean the paths.

            List<List<IntPoint>> oP = new List<List<IntPoint>>();
            foreach (List<IntPoint> p in oPf)
            {
                List<IntPoint> clp = Clipper.CleanPolygon(p);
                oP.Add(clp);
            }

            List<PolylineCurve> oPL = intToPL(oP,sc);
            // find point closest to first of original curve

            int cp = -1;
            double pos = 0;
            double di,t, dist = 1000000000000000000;
            for (int i = 0; i < oPL.Count; i++)
            {
                oPL[i].ClosestPoint(P.PointAt(0), out t, dist);
                di = oPL[i].PointAt(t).DistanceTo(P.PointAt(0));
                if(di<dist)
                {
                    dist = d;
                    cp = i;
                    pos = t;
                }
            }

            if (cp >= 0) { oPL[cp].ChangeClosedCurveSeam(pos); }

            return oPL;
        }

        private static IntPoint dtoi (Point3d p, double sc)
        {
            return new IntPoint((long)Math.Round(p.X * sc), (long)Math.Round(p.Y * sc));
        }
        private static Point3d itod(IntPoint p, double sc)
        {
            return new Point3d((double)p.X/sc,(double)p.Y/sc,0);
        }

        private static List<List<IntPoint>> pLtoInt(PolylineCurve PC, double sc)
        {
            List<IntPoint> IntP = new List<IntPoint>();
            Polyline P;
            PC.TryGetPolyline(out P);
            foreach(Point3d p in P) { IntP.Add(dtoi(p,sc)); }
            //List<List<IntPoint>> IntPL = Clipper.SimplifyPolygon(IntP,);
            List<List<IntPoint>> IntPL = new List<List<IntPoint>> { IntP };

            return IntPL;
        }
        private static List<List<IntPoint>> pLtoInt(List<PolylineCurve> P, double sc)
        {
            List<List<IntPoint>> IntPL = new List<List<IntPoint>>();
            foreach(PolylineCurve PL in P) { IntPL.AddRange(pLtoInt(PL, sc)); }

            return IntPL;
        }

        private static List<PolylineCurve> intToPL(List<List<IntPoint>> IP, double sc)
        {
            List<PolylineCurve> PL = new List<PolylineCurve>();
            Polyline P;

            foreach(List<IntPoint> IntP in IP)
            {
                P = new Polyline();
                foreach(IntPoint ip in IntP) { P.Add(itod(ip, sc)); }
                if (IntP.Count > 0) { P.Add(itod(IntP[0], sc)); }
                PL.Add(new PolylineCurve(P));
            }

            return PL;
        }

    }
}
