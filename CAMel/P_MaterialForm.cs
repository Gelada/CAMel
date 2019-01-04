using System;
using System.Collections.Generic;

using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using CAMel.Types.MaterialForm;
using CAMel.Types.Machine;
using static CAMel.Exceptions;

namespace CAMel.Types.MaterialForm
{

    public struct MFintersection
    {
        public MFintersection(Point3d Pt, Vector3d Away,double lineP)
        {
            this.point = Pt;
            this.lineP = lineP;
            this.isSet = true;
            this._away = Away;
            this._away.Unitize();
        }

        public Point3d point { get; private set; }    // Point of intersection
        private Vector3d _away;
        public Vector3d away { get { return this._away; } // direction to get away from the material (eg normal)
            private set
            {
                this._away = value;
                this._away.Unitize();
            }
        } 
        public double lineP { get; private set; }  // position along intersecting line
        public bool isSet { get; private set; }
    }

    
    public class MFintersects
    {
        public MFintersects()
        {
            this.inters = new List<MFintersection>();
            this.through = new MFintersection(); // creates an unset value
            this.first = new MFintersection(); // creates and unset value
            this.midOut = new Vector3d();
        }
        public List<MFintersection> inters { get; private set; } // List of intersections 

        public double thrDist { get { return this.through.lineP; } }
        public double firstDist { get { return this.first.lineP; } }

        public MFintersection through { get; protected set; }// intersection with highest lineParameter
        public MFintersection first { get; protected set; } // intersection with lowest lineParameter

        public Point3d mid // midpoint through material
        {
            get { return (this.first.point + this.through.point) / 2; }
        }
        private Vector3d _midOut;
        public Vector3d midOut
        { // direction to head to surface from the middle of middle of the line
            get { return this._midOut; }
            set
            {
                value.Unitize();
                this._midOut = value;
            }
        }

        public void Add(MFintersection inter)
        {
            this.inters.Add(inter);

            if(!this.through.isSet || this.through.lineP < inter.lineP ) { this.through = inter; }
            if(!this.first.isSet || this.first.lineP > inter.lineP) { this.first = inter; }
        }
        public void Add(Point3d Pt, Vector3d away, double lineP)
        {
            this.Add(new MFintersection(Pt, away, lineP));
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
        internal static ToolPath insertRetract(IMaterialForm MF, ToolPath tP)
        {
            if (tP.matTool == null) { matToolException(); }
            if (MF == null) { matFormException(); }
            ToolPath irTP = tP.deepClone();
            irTP.Additions.insert = false;
            irTP.Additions.retract = false;

            MFintersection inter;

            double utol = MF.safeDistance * 1.05;
            ToolPoint tempTP;

            // check if we have something to do
            if (tP.Additions.insert && irTP.Count > 0) // add insert
            {
                //note we do this backwards adding points to the start of the path.

                // get distance to surface and insert direction
                inter = MF.intersect(irTP.firstP, 0).through;

                // check to see if there was an intersection
                if (inter.isSet)
                {
                    // point on material surface

                    tempTP = irTP.firstP.deepClone();
                    tempTP.pt = inter.point;
                    tempTP.feed = tP.matTool.feedPlunge;
                    irTP.Insert(0, tempTP);

                    // point out at safe distance

                    tempTP = irTP.firstP.deepClone();
                    tempTP.pt = tempTP.pt + inter.away * utol;
                    tempTP.feed = 0; // we can use a rapid move
                    irTP.Insert(0, tempTP);
                } else
                {
                    // check intersection with material extended to safe distance
                    inter = MF.intersect(irTP.firstP,utol).through;
                    if(inter.isSet)
                    {
                        // point out at safe distance
                        tempTP = irTP.firstP.deepClone();
                        tempTP.pt = inter.point;
                        tempTP.feed = 0; // we can use a rapid move
                        irTP.Insert(0, tempTP);
                    } //  otherwise nothing needs to be added as we do not interact with material
                }
            }
            if (tP.Additions.retract && irTP.Count > 0) // add retract
            {
                // get distance to surface and retract direction
                inter = MF.intersect(irTP.lastP, 0).through;
                if (inter.isSet)
                {
                    tempTP = irTP.lastP.deepClone();

                    // set speed to the plunge feed rate.
                    tempTP.feed = tP.matTool.feedPlunge;

                    // Pull back to surface
                    tempTP.pt = inter.point;

                    irTP.Add(tempTP);

                    // Pull away to safe distance

                    tempTP = irTP.lastP.deepClone();
                    tempTP.pt = tempTP.pt + inter.away * utol;
                    tempTP.feed = 0; // we can use a rapid move
                    irTP.Add(tempTP);
                } else
                {
                    // check intersection with material extended to safe distance
                    inter = MF.intersect(irTP.lastP, utol).through;
                    if (inter.isSet)
                    {
                        // point out at safe distance
                        tempTP = irTP.lastP.deepClone();
                        tempTP.pt = inter.point;
                        tempTP.feed = 0; // we can use a rapid move
                        irTP.Add(tempTP);
                    } //  otherwise nothing needs to be added as we do not interact with material
                }
            }
            return irTP;
        }

        // Does the line intersect the surface of the material?
        internal static bool lineIntersect(IMaterialForm MF,Point3d start, Point3d end, double tolerance, out MFintersects inters)
        {
            inters = MF.intersect(start, end - start, tolerance);
            double lLength = (end - start).Length;
            return (inters.hits && 
                ((inters.firstDist > 0 && inters.firstDist < lLength) ||
                 (inters.thrDist > 0 && inters.thrDist < lLength))
                );
        }

        internal static ToolPath refine(IMaterialForm MF, ToolPath TP,IMachine M)
        {
            // for each line check if it intersects 
            // the MF and add those points. 
            // also add the midpoint if going more than half way through
            // TODO problem of long lines getting deep 

            ToolPath refined = TP.deepCloneWithNewPoints(new List<ToolPoint>());

            // Add the first ToolPoint

            if (TP.Count > 0) { refined.Add(TP.firstP); }
            
            double lineLen;
            MFintersects inters;

            // TODO refine on significant changes of direction
            for (int i = 0; i < TP.Count - 1; i++)
            {
                // for every line between points check if we leave or enter the material

                if(MF.intersect(TP[i].pt, TP[i + 1].pt, 0, out inters))
                {
                    lineLen = (TP[i + 1].pt - TP[i].pt).Length;

                    if (inters.firstDist > 0) // add first intersection if on line
                    {
                        refined.Add(M.interpolate(TP[i], TP[i + 1],TP.matTool, inters.firstDist / lineLen, false));
                    }
                   
                    if(inters.firstDist > 0 && lineLen > inters.thrDist) // add midpoint of intersection if it passes right through
                    {
                        refined.Add(M.interpolate(TP[i], TP[i + 1],TP.matTool, (inters.firstDist+inters.thrDist) / (2.0*lineLen),false));
                    }
                    if(lineLen > inters.thrDist) // add last intersection if on line
                    {
                        refined.Add(M.interpolate(TP[i], TP[i + 1], TP.matTool, inters.thrDist / lineLen,false));
                    }

                }
                refined.Add(TP[i + 1]);
            }

            return refined;
        }
    }

    public interface IMaterialForm : ICAMel_Base
    {
        double safeDistance { get; }
        double materialTolerance { get; }

        MFintersects intersect(Point3d Pt, Vector3d direction, double tolerance);
        MFintersects intersect(ToolPoint TP, double tolerance);
        bool intersect(Point3d start, Point3d end, double tolerance, out MFintersects inters); 

        ToolPath refine(ToolPath TP,IMachine M);
        ToolPath insertRetract(ToolPath TP);

        Mesh getMesh();
    }
    
    public static class MaterialForm
    {
        // Currently links to grasshopper to use "CastTo" behaviours. 
        public static bool create(IGH_Goo inputGeometry, double tolerance, double safeD, out IMaterialForm MF)
        {
            if(inputGeometry.CastTo<Box>(out Box BoxT))
            {
                MF = create(BoxT, tolerance, safeD);
                return true;
            }
            else if (inputGeometry.CastTo<Cylinder>(out Cylinder CyT))
            {
                MF = create(CyT, tolerance, safeD);
                return true;
            }
            else if (inputGeometry.CastTo<Mesh>(out Mesh meshT))
            {
                MF = create(meshT, tolerance, safeD);
                return true;
            }
            else if (inputGeometry.CastTo<Surface>(out Surface surfT))
            {
                MF = create(surfT, tolerance, safeD);
                return true;
            }
            else if (inputGeometry.CastTo<Brep>(out Brep brepT))
            {
                MF = create(brepT, tolerance, safeD);
                return true;
            }
            else
            {
                MF = null;
                return false;
            }
        }

        private static IMaterialForm create(Surface inputGeometry, double tolerance, double safeD)
        {
            Cylinder Cy = new Cylinder();
            if (inputGeometry.TryGetCylinder(out Cy))
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
            MFBox mB = new MFBox(B, tolerance, safeD);
            return (IMaterialForm) mB;
        }

        private static IMaterialForm create(BoundingBox BB, double tolerance, double safeD)
        {
            MFBox mB = new MFBox(new Box(BB), tolerance, safeD);
            return (IMaterialForm)mB;
        }

        private static IMaterialForm create(Cylinder Cy, double tolerance, double safeD)
        {
            MFCylinder mC = new MFCylinder(Cy, tolerance, safeD);
            return (IMaterialForm)mC;
        }
    }
}


namespace CAMel.Types
{
    // Grasshopper Type Wrapper
    public class GH_MaterialForm : CAMel_Goo<IMaterialForm>
    {
        public GH_MaterialForm() { this.Value = null; }
        // Construct from unwrapped object
        public GH_MaterialForm(IMaterialForm MF) { this.Value = MF; }
        // Copy Constructor (just reference as MaterialForm is Immutable)
        public GH_MaterialForm(GH_MaterialForm MF) { this.Value = MF.Value; }
        // Duplicate
        public override IGH_Goo Duplicate() { return new GH_MaterialForm(this); }

        public override bool CastTo<Q>(ref Q target)
        {
            // Trivial base case, we already have a IMaterialForm, the cast is safe
            if (typeof(Q).IsAssignableFrom(typeof(IMaterialForm)))
            {
                object ptr = this.Value;
                target = (Q)ptr;
                return true;
            }

            // Cast to a Mesh if that is asked for.
            if (typeof(Q).IsAssignableFrom(typeof(GH_Mesh)))
            {
                Mesh m = this.Value.getMesh();
                if (m.IsValid)
                {
                    object GHm = new GH_Mesh(m);
                    target = (Q)GHm;
                    return true;
                }
            }
            return false;
        }

        public override bool CastFrom(object source)
        {
            if (source == null) { return false; }
            //Cast from unwrapped MO
            if (typeof(IMaterialForm).IsAssignableFrom(source.GetType()))
            {
                Value = (IMaterialForm)source;
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