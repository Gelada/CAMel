﻿using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using CAMel.Types.MaterialForm;

namespace CAMel.Types.MaterialForm
{
    // Geometry Intersection type for directed points
    public enum DirectedPointInsideOutside
    { Inside, Outside, Through }

    public struct intersection
    {
        intersection(Point3d Pt, Vector3d Away,double lineP)
        {
            this.Pt = Pt;
            this.Away = Away;
            this.lineP = lineP;
        }
        public Point3d Pt { get; set; }    // Point of intersection
        public Vector3d Away { get; set; } // direction to get away from the material (eg normal)
        public double lineP { get; set; }  // position along intersecting line
    }

    public class intersects
    {
        public List<intersection> inters; // List of intersections
        public Point3d mid // midpoint through material
        {
            get { return (this.first.Pt + this.through.Pt) / 2; }
        }
        public Vector3d midOut { get; set; } // direction to head to surface from the middle of middle of the line
        //TODO set these through adding items to inters
        public intersection through { // intersection with highest lineParameter
            get
            { return this.through; }
            set
            {
                this.through = value;
                this.thrDist = value.lineP;
            }
        }
        public intersection first
        { // intersection with lowest lineParameter
            get
            { return this.first; }
            set
            {
                this.first = value;
                this.firstDist = value.lineP;
            }
        }

        public double thrDist { get; private set; }
        public double firstDist { get; private set; }
        public bool hits
        {
            get => (this.inters.Count > 0);
        }
    }


    public interface IMaterialForm : ICAMel_Base
    {
        double safeDistance { get; set; }
        double materialTolerance { get; set; }

        //double intersect(Point3d Pt, Vector3d direction, double tolerance);
        intersects intersect(Point3d Pt, Vector3d direction, double tolerance);
        //double intersect(ToolPoint TP, double tolerance);
        intersects intersect(ToolPoint TP, double tolerance);

        ToolPath Refine(ToolPath TP, Machine M);
        ToolPath InsertRetract(ToolPath TP);

        bool getBrep(ref Brep brep);
        bool getBrep(ref object brep);

        bool getMesh(ref Mesh mesh);
        bool getMesh(ref object mesh);
    }
    
    public static class MaterialForm
    {
        // FIXME: These functions need to do some inspection of the input geometry and determine what MaterialForm
        // is best suited for use. Currently these are defaulted to a basic unit box.
        public static IMaterialForm create(object inputGeometry, double tolerance, double safeD)
        {
            Interval i = new Interval(0.0, 1.0);
            Box b = new Box(Plane.WorldXY, i, i, i);
            return new MFBox(b, tolerance, safeD);
        }

        // FIXME: These functions need to do some inspection of the input geometry and determine what MaterialForm
        // is best suited for use. Currently these are defaulted to a basic unit box.
        public static IMaterialForm create(Brep inputGeometry, double tolerance, double safeD)
        {
            Interval i = new Interval(0.0, 1.0);
            Box b = new Box(Plane.WorldXY, i, i, i);
            return new MFBox(b, tolerance, safeD);
        }

        // FIXME: These functions need to do some inspection of the input geometry and determine what MaterialForm
        // is best suited for use. Currently these are defaulted to a basic unit box.
        public static IMaterialForm create(Mesh inputGeometry, double tolerance, double safeD)
        {
            Interval i = new Interval(0.0, 1.0);
            Box b = new Box(Plane.WorldXY, i, i, i);
            return new MFBox(b, tolerance, safeD);
        }
    }
}


namespace CAMel.Types
{
    // Grasshopper Type Wrapper
    public class GH_MaterialForm : CAMel_Goo<IMaterialForm>
    {
        // Construct from unwrapped object
        public GH_MaterialForm(IMaterialForm MF)
        {
            this.Value = MF;
        }

        // Copy Constructor.
        public GH_MaterialForm(GH_MaterialForm MF)
        {
            this.Value = (IMaterialForm) MF.Value.Duplicate();
        }

        // Duplicate
        public override IGH_Goo Duplicate()
        {
            return new GH_MaterialForm(this);
        }

        public override bool CastTo<Q>(ref Q target)
        {
            // Trivial base case, we already have a IMaterialForm, the cast is safe
            if (typeof(Q).IsAssignableFrom(typeof(IMaterialForm)))
            {
                object ptr = this.Value;
                target = (Q)ptr;
                return true;
            }

            // Cast to Brep or if that is asked for.
            if (typeof(Q).IsAssignableFrom(typeof(Brep)))
            {
                object b = null;
                if (this.Value.getBrep(ref b))
                {
                    target = (Q)b;
                    return true;
                }
            }

            // Cast to a Mesh if that is asked for.
            if (typeof(Q).IsAssignableFrom(typeof(Mesh)))
            {
                object m = null;
                if (this.Value.getMesh(ref m))
                {
                    target = (Q)m;
                    return true;
                }
            }
            return false;
        }

        public override bool CastFrom(object source)
        {
            if (source == null)
            {
                return false;
            }
            if (source is IMaterialForm)
            {
                this.Value = (IMaterialForm) source;
                return true;
            }
            return false;
        }

    }

    // Grasshopper Parameter Wrapper
    public class GH_MaterialFormPar : GH_Param<GH_MaterialForm>
    {
        public GH_MaterialFormPar() :
            base("Material Form", "MatForm", "Contains a collection of Material Forms", "CAMel", "  Params", GH_ParamAccess.item) { }

        public override Guid ComponentGuid
        {
            get { return new Guid("01d791bb-d6b8-42e3-a1ba-6aec037cacc3"); }
        }
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Properties.Resources.materialform;
            }
        }
    }

}