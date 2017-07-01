using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using CAMel.Types.MaterialForm;

namespace CAMel.Types.MaterialForm
{

    public struct intersection
    {
        public intersection(Point3d Pt, Vector3d Away,double lineP)
        {
            this.Pt = Pt;
            this.lineP = lineP;
            this.isSet = true;
            this.Away = Away;
        }

        public Point3d Pt { get; private set; }    // Point of intersection
        public Vector3d Away { get { return this.Away; } // direction to get away from the material (eg normal)
            private set
            {
                this.Away = value;
                this.Away.Unitize();
            }
        } 
        public double lineP { get; private set; }  // position along intersecting line
        public bool isSet { get; private set; }
    }

    public class intersects
    {
        public intersects()
        {
            inters = new List<intersection>();
            through = new intersection(); // creates an unset value
            first = new intersection(); // creates and unset value
            midOut = new Vector3d();
        }
        public List<intersection> inters { get; private set; } // List of intersections 

        public double thrDist { get; private set; }
        public double firstDist { get; private set; }

        public intersection through
        { // intersection with highest lineParameter
            get { return this.through; }
            private set
            {
                this.through = value;
                this.thrDist = value.lineP;
            }
        }
        public intersection first
        { // intersection with lowest lineParameter
            get { return this.first; }
            private set
            {
                this.first = value;
                this.firstDist = value.lineP;
            }
        }

        public Point3d mid // midpoint through material
        {
            get { return (this.first.Pt + this.through.Pt) / 2; }
        }
        public Vector3d midOut
        { // direction to head to surface from the middle of middle of the line
            get { return this.midOut }
            set
            {
                this.midOut = value;
                this.midOut.Unitize();
            }
        }

        public void Add(intersection inter)
        {
            this.inters.Add(inter);

            if(!this.through.isSet || this.through.lineP < inter.lineP ) { this.through = inter; }
            if(!this.first.isSet || this.first.lineP > inter.lineP) { this.first = inter; }
        }
        public void Add(Point3d Pt, Vector3d away, double lineP)
        {
            this.Add(new intersection(Pt, away, lineP));
        }

        public int Count
        {
            get { return this.inters.Count; }
        }

        public bool hits
        { get { return (this.inters.Count > 0); } }
    }

    internal static class MFDefault
    {
        internal static ToolPath InsertRetract(IMaterialForm MF, ToolPath TP)
        {
            ToolPath irTP = new ToolPath(TP);
            irTP.Additions.insert = false;
            irTP.Additions.retract = false;

            intersection inter;

            // check if we have something to do
            if (TP.Additions.insert && irTP.Pts.Count > 0) // add insert
            {
                //note we do this backwards adding points to the start of the path.

                // get distance to surface and insert direction
                inter = MF.intersect(irTP.Pts[0], 0).through;

                // point on material surface

                ToolPoint tempTP = new ToolPoint(irTP.Pts[0]);
                tempTP.Pt = tempTP.Pt + tempTP.Dir * inter.lineP;
                tempTP.feed = TP.MatTool.feedPlunge;
                irTP.Pts.Insert(0, tempTP);

                // point out at safe distance

                tempTP = new ToolPoint(irTP.Pts[0]);
                tempTP.Pt = tempTP.Pt + inter.Away * (MF.safeDistance);
                tempTP.feed = 0; // we can use a rapid move
                irTP.Pts.Insert(0, tempTP);
            }
            if (TP.Additions.retract && irTP.Pts.Count > 0) // add retract
            {
                // get distance to surface and retract direction
                inter = MF.intersect(irTP.Pts[irTP.Pts.Count - 1], 0).through;

                ToolPoint tempTP = new ToolPoint(irTP.Pts[irTP.Pts.Count - 1]);

                // set speed to the plunge feed rate.
                tempTP.feed = TP.MatTool.feedPlunge;

                // Pull back to surface
                tempTP.Pt = tempTP.Pt + tempTP.Dir * inter.lineP;
                tempTP.feed = 0; // we can use a rapid move

                irTP.Pts.Add(tempTP);

                // Pull away to safe distance

                tempTP = new ToolPoint(irTP.Pts[irTP.Pts.Count - 1]);
                tempTP.Pt = tempTP.Pt + inter.Away * (MF.safeDistance);
                tempTP.feed = 0; // we can use a rapid move
                irTP.Pts.Add(tempTP);
            }
            return irTP;
        }

        internal static bool lineIntersect(IMaterialForm MF,Point3d start, Point3d end, double tolerance, out intersects inters)
        {
            inters = MF.intersect(start, end - start, tolerance);
            return (inters.hits && inters.firstDist < (end - start).Length);
        }

        internal static ToolPath refine(IMaterialForm MF, ToolPath TP, Machine M)
        {
            // for each line check if it intersects 
            // the MF and add those points. 
            // also add the midpoint if going more than half way through
            // TODO problem of long lines getting deep 

            ToolPath refined = new ToolPath(TP);
            refined.Pts = new List<ToolPoint>();

            // Add the first ToolPoint

            if (TP.Pts.Count > 0) { refined.Pts.Add(TP.Pts[0]); }
            
            double lineLen;
            intersects inters;
            for (int i = 0; i < TP.Pts.Count - 1; i++)
            {
                // for every line between points check if we leave or enter the material

                if(MF.intersect(TP.Pts[i].Pt, TP.Pts[i + 1].Pt, 0, out inters))
                {
                    lineLen = (TP.Pts[i + 1].Pt - TP.Pts[i].Pt).Length;
                    refined.Pts.Add(M.Interpolate(TP.Pts[i], TP.Pts[i + 1], inters.firstDist / lineLen));

                    if(lineLen > inters.thrDist) //all way through add mid point and through
                    {
                        refined.Pts.Add(M.Interpolate(TP.Pts[i], TP.Pts[i + 1], (inters.firstDist+inters.thrDist) / (2.0*lineLen)));
                        refined.Pts.Add(M.Interpolate(TP.Pts[i], TP.Pts[i + 1], inters.thrDist / lineLen));
                    }
                    else if( lineLen > inters.thrDist/2) // more than half way through add mid point 
                    {
                        refined.Pts.Add(M.Interpolate(TP.Pts[i], TP.Pts[i + 1], (inters.firstDist+lineLen) / (2.0*lineLen)));
                    }
                }
                refined.Pts.Add(TP.Pts[i + 1]);
            }

            return refined;
        }
    }

    public interface IMaterialForm : ICAMel_Base
    {
        double safeDistance { get; set; }
        double materialTolerance { get; set; }

        intersects intersect(Point3d Pt, Vector3d direction, double tolerance);
        intersects intersect(ToolPoint TP, double tolerance);
        bool intersect(Point3d start, Point3d end, double tolerance, out intersects inters); 

        ToolPath refine(ToolPath TP, Machine M);
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
            if(inputGeometry.GetType() == typeof(Box))
            {
                return create((Box)inputGeometry, tolerance, safeD);
            }
            else if (inputGeometry.GetType() == typeof(Cylinder))
            {
                return create((Cylinder)inputGeometry, tolerance, safeD);
            }
            else if (inputGeometry.GetType() == typeof(Mesh))
            {
                return create((Mesh)inputGeometry, tolerance, safeD);
            }
            else if (inputGeometry.GetType() == typeof(Brep))
            {
                return create((Brep)inputGeometry, tolerance, safeD);
            }
            else
            {
                throw new FormatException("create was not able to interpret the object");
            }
        }
        
        private static IMaterialForm create(Brep inputGeometry, double tolerance, double safeD)
        {
            Cylinder Cy = new Cylinder();
            if(inputGeometry.Surfaces.Count == 1 && inputGeometry.Surfaces[0].TryGetCylinder(out Cy))
            {
                // Cope with bug in TryGetCylinder
                BoundingBox BB = inputGeometry.GetBoundingBox(Cy.CircleAt(0).Plane);
                Cy.Height1 = BB.Min.Z;
                Cy.Height2 = BB.Max.Z;
                return create(Cy, tolerance, safeD);
            }
            else
            {
                // TODO throw warning that we are just using bounding box
                return create(inputGeometry.GetBoundingBox(false), tolerance, safeD);
            }
        }

        private static IMaterialForm create(Mesh inputGeometry, double tolerance, double safeD)
        {
            if(inputGeometry.HasBrepForm)
            {
                return create(Brep.TryConvertBrep(inputGeometry), tolerance, safeD);
            }
            else
            {
                // TODO throw warning that we are just using bounding box
                return create(inputGeometry.GetBoundingBox(false), tolerance, safeD);
            }
        }

        private static IMaterialForm create(Box B, double tolerance, double safeD)
        {
            return new MFBox(B, tolerance, safeD);
        }

        private static IMaterialForm create(Cylinder Cy, double tolerance, double safeD)
        {
            return new MFCylinder(Cy, tolerance, safeD);
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