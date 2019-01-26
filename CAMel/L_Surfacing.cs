using System;
using System.Collections.Generic;

using Rhino.Geometry;

using CAMel.Types.MaterialForm;

namespace CAMel.Types
{

    // Functions to generate operations
    public static class Surfacing
    {
        public static SurfacePath Parallel(Curve C, Plane Dir, double stepOver, bool zZ, SurfToolDir sTD, BoundingBox BB, MaterialTool MT)
        {
            Curve uC  = C;
            if (C==null) // default to curve running along X-direction on Plane. 
            { uC = new LineCurve(Dir.PointAt(BB.Min.X, BB.Min.Y), Dir.PointAt(BB.Max.X, BB.Min.Y));  }
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
    }
}
