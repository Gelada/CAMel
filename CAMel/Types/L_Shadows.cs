using System;
using System.Collections.Generic;
using System.Linq;

//using ConcaveHull;

using JetBrains.Annotations;

using Rhino.Geometry;

namespace CAMel.Types
{
    class Shadows
    {
        // Create convexHull on list of 2d points.
        /*public static Polyline ConvexHull(List<Point3d> pts)
        {
            List<Node> nPts = new List<Node>();
            int i = 1;
            foreach(Point3d pt in pts) {
                nPts.Add(new Node(pt.X, pt.Y, i));
                i++;
            }

            Hull.setConvexHull(nPts);
            List<ConcaveHull.Line> hull = Hull.setConcaveHull(0,2);

            Polyline hullPL = new Polyline();
            foreach(ConcaveHull.Line l in hull) { hullPL.Add(new Point3d(l.nodes[0].x, l.nodes[0].y, 0)); }

            return hullPL;
        }*/

        // shadow of a mesh on a plane. 
        public static Polyline MeshShadow(Mesh m, Plane Pl)
        {
            Mesh fm = m.DuplicateMesh();
            fm.Transform(Transform.PlanarProjection(Pl));
            fm.MergeAllCoplanarFaces(.0001);

            Polyline pl = new Polyline();
            foreach(int v in fm.Ngons[0].BoundaryVertexIndexList())
            {
                pl.Add(fm.Vertices[v]);
            }
            pl.Add(pl[0]);
            return pl;
        }




    }
}
