using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using ClipperLib;

namespace CAMel.Types
{

    // Wrapper clases and library interfaces for leveraging the clipper
    // library for doing offsetting
    public static class Offsetting
    {
        public static List<PolylineCurve> offset(PolylineCurve P, double d)
        {
            
            BoundingBox BB = P.GetBoundingBox(false);
            double md = BB.Max.X;
            if(BB.Max.Y > md) { md = BB.Max.Y; }
            if (-BB.Min.X > md) { md = -BB.Min.X; }
            if (-BB.Min.Y > md) { md = -BB.Min.Y; }
            double sc = 2000000000.0/md;

            List<List<IntPoint>> IP = PLtoInt(P,sc);
            List<List<IntPoint>> oP = Clipper.OffsetPolygons(IP, d*sc, JoinType.jtRound);

            List<PolylineCurve> oPL = InttoPL(oP,sc);

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

        private static List<List<IntPoint>> PLtoInt(PolylineCurve PC, double sc)
        {
            List<IntPoint> IntP = new List<IntPoint>();
            Polyline P;
            PC.TryGetPolyline(out P);
            foreach(Point3d p in P) { IntP.Add(dtoi(p,sc)); }
            List<List<IntPoint>> IntPL = new List<List<IntPoint>>();
            IntPL.Add(IntP);

            return IntPL;
        }
        private static List<List<IntPoint>> PLtoInt(List<PolylineCurve> P, double sc)
        {
            List<List<IntPoint>> IntPL = new List<List<IntPoint>>();
            foreach(PolylineCurve PL in P) { IntPL.AddRange(PLtoInt(PL, sc)); }

            return IntPL;
        }

        private static List<PolylineCurve> InttoPL(List<List<IntPoint>> IP, double sc)
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
