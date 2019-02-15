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
        public MFintersection(Point3d pt, Vector3d away,double lineP)
        {
            this.point = pt;
            this.lineP = lineP;
            this.isSet = true;
            this.away = away;
            this.away.Unitize();
        }

        public Point3d point { get; } // Point of intersection
        public Vector3d away { get; } // direction to get away from the material (eg normal)

        public double lineP { get; }  // position along intersecting line
        public bool isSet { get; }
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

        private List<MFintersection> inters { get; } // List of intersections 

        public double thrDist => this.through.lineP;
        public double firstDist => this.first.lineP; 

        public MFintersection through { get; private set; }// intersection with highest lineParameter
        public MFintersection first { get; private set; } // intersection with lowest lineParameter

        public Point3d mid => (this.first.point + this.through.point) / 2; // midpoint through material

        private Vector3d _midOut;
        public Vector3d midOut
        { // direction to head to surface from the middle of middle of the line
            get => this._midOut; 
            set
            {
                value.Unitize();
                this._midOut = value;
            }
        }

        public void add(MFintersection inter)
        {
            this.inters.Add(inter);

            if(!this.through.isSet || this.through.lineP < inter.lineP ) { this.through = inter; }
            if(!this.first.isSet || this.first.lineP > inter.lineP) { this.first = inter; }
        }
        public void add(Point3d pt, Vector3d away, double lineP)
        {
            add(new MFintersection(pt, away, lineP));
        }

        public int count => this.inters.Count;

        public bool hits => this.inters.Count > 0;
    }

    internal static class MFDefault
    {
        internal static ToolPath insertRetract(IMaterialForm mF, ToolPath tP)
        {
            if (tP.matTool == null) { matToolException(); return null; }
            if (mF == null) { matFormException(); return null; }
            ToolPath irTP = tP.deepClone();
            irTP.additions.insert = false;
            irTP.additions.retract = false;

            MFintersection inter;

            double uTol = mF.safeDistance * 1.05;
            ToolPoint tempTP;

            // check if we have something to do
            if (tP.additions.insert && irTP.Count > 0) // add insert
            {
                //note we do this backwards adding points to the start of the path.

                // get distance to surface and insert direction
                inter = mF.intersect(irTP.firstP, 0).through;

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
                    tempTP.pt = tempTP.pt + inter.away * uTol;
                    tempTP.feed = 0; // we can use a rapid move
                    irTP.Insert(0, tempTP);
                } else
                {
                    // check intersection with material extended to safe distance
                    inter = mF.intersect(irTP.firstP,uTol).through;
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
            if (tP.additions.retract && irTP.Count > 0) // add retract
            {
                // get distance to surface and retract direction
                inter = mF.intersect(irTP.lastP, 0).through;
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
                    tempTP.pt = tempTP.pt + inter.away * uTol;
                    tempTP.feed = 0; // we can use a rapid move
                    irTP.Add(tempTP);
                } else
                {
                    // check intersection with material extended to safe distance
                    inter = mF.intersect(irTP.lastP, uTol).through;
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
        internal static bool lineIntersect(IMaterialForm mF,Point3d start, Point3d end, double tolerance, out MFintersects inters)
        {
            inters = mF.intersect(start, end - start, tolerance);
            double lLength = (end - start).Length;
            return (inters.hits && 
                ((inters.firstDist > 0 && inters.firstDist < lLength) ||
                 (inters.thrDist > 0 && inters.thrDist < lLength))
                );
        }

        internal static ToolPath refine(IMaterialForm mF, ToolPath tP,IMachine m)
        {
            // for each line check if it intersects 
            // the MF and add those points. 
            // also add the midpoint if going more than half way through
            // TODO problem of long lines getting deep 

            ToolPath refined = tP.deepCloneWithNewPoints(new List<ToolPoint>());

            // Add the first ToolPoint

            if (tP.Count > 0) { refined.Add(tP.firstP); }
            
            double lineLen;
            MFintersects inters;

            // TODO refine on significant changes of direction
            for (int i = 0; i < tP.Count - 1; i++)
            {
                // for every line between points check if we leave or enter the material

                if(mF.intersect(tP[i].pt, tP[i + 1].pt, 0, out inters))
                {
                    lineLen = (tP[i + 1].pt - tP[i].pt).Length;

                    if (inters.firstDist > 0) // add first intersection if on line
                    {
                        refined.Add(m.interpolate(tP[i], tP[i + 1],tP.matTool, inters.firstDist / lineLen, false));
                    }
                   
                    if(inters.firstDist > 0 && lineLen > inters.thrDist) // add midpoint of intersection if it passes right through
                    {
                        refined.Add(m.interpolate(tP[i], tP[i + 1],tP.matTool, (inters.firstDist+inters.thrDist) / (2.0*lineLen),false));
                    }
                    if(lineLen > inters.thrDist) // add last intersection if on line
                    {
                        refined.Add(m.interpolate(tP[i], tP[i + 1], tP.matTool, inters.thrDist / lineLen,false));
                    }

                }
                refined.Add(tP[i + 1]);
            }

            return refined;
        }
    }

    public interface IMaterialForm : ICAMelBase
    {
        double safeDistance { get; }
        double materialTolerance { get; }

        MFintersects intersect(Point3d pt, Vector3d direction, double tolerance);
        MFintersects intersect(ToolPoint tP, double tolerance);
        bool intersect(Point3d start, Point3d end, double tolerance, out MFintersects inters); 

        ToolPath refine(ToolPath tP,IMachine m);
        ToolPath insertRetract(ToolPath tP);

        Mesh getMesh();
        BoundingBox getBoundingBox();
    }
    
    public static class MaterialForm
    {
        // Currently links to grasshopper to use "CastTo" behaviours. 
        public static bool create(IGH_Goo inputGeometry, double tolerance, double safeD, out IMaterialForm mF)
        {
            if (inputGeometry.CastTo(out Box boxT))
            {
                mF = create(boxT, tolerance, safeD);
                return true;
            }
            if (inputGeometry.CastTo(out Cylinder cyT))
            {
                mF = create(cyT, tolerance, safeD);
                return true;
            }
            if (inputGeometry.CastTo(out Mesh meshT))
            {
                mF = create(meshT, tolerance, safeD);
                return true;
            }
            if (inputGeometry.CastTo(out Surface surfT))
            {
                mF = create(surfT, tolerance, safeD);
                return true;
            }
            if (inputGeometry.CastTo(out Brep brepT))
            {
                mF = create(brepT, tolerance, safeD);
                return true;
            }
            mF = null;
            return false;
        }

        private static IMaterialForm create(Surface inputGeometry, double tolerance, double safeD)
        {
            Cylinder Cy;
            if (inputGeometry.TryGetCylinder(out Cy))
            {
                // Cope with bug in TryGetCylinder
                BoundingBox bb = inputGeometry.GetBoundingBox(Cy.CircleAt(0).Plane);
                Cy.Height1 = bb.Min.Z;
                Cy.Height2 = bb.Max.Z;
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
            if(inputGeometry.Surfaces.Count == 1 && inputGeometry.Surfaces[0].TryGetCylinder(out Cylinder cy))
            {
                // Cope with bug in TryGetCylinder
                BoundingBox bb = inputGeometry.GetBoundingBox(cy.CircleAt(0).Plane);
                cy.Height1 = bb.Min.Z;
                cy.Height2 = bb.Max.Z;
                return create(cy, tolerance, safeD);
            }

            // TODO throw warning that we are just using bounding box
            return create(inputGeometry.GetBoundingBox(false), tolerance, safeD);
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

        private static IMaterialForm create(Box b, double tolerance, double safeD)
        {
            MFBox mB = new MFBox(b, tolerance, safeD);
            return mB;
        }

        private static IMaterialForm create(BoundingBox bb, double tolerance, double safeD)
        {
            MFBox mB = new MFBox(new Box(bb), tolerance, safeD);
            return mB;
        }

        private static IMaterialForm create(Cylinder cy, double tolerance, double safeD)
        {
            MFCylinder mC = new MFCylinder(cy, tolerance, safeD);
            return mC;
        }
    }
}


namespace CAMel.Types
{
    // Grasshopper Type Wrapper
    public sealed class GH_MaterialForm : CAMel_Goo<IMaterialForm>, IGH_PreviewData
    {
        public BoundingBox ClippingBox => this.Value.getBoundingBox();

        public GH_MaterialForm() { this.Value = null; }
        // Construct from unwrapped object
        public GH_MaterialForm(IMaterialForm mF) { this.Value = mF; }
        // Copy Constructor (just reference as MaterialForm is Immutable)
        public GH_MaterialForm(GH_MaterialForm mF) { this.Value = mF.Value; }
        // Duplicate
        public override IGH_Goo Duplicate() { return new GH_MaterialForm(this); }

        public override bool CastTo<T>(ref T target)
        {
            // Trivial base case, we already have a IMaterialForm, the cast is safe
            if (typeof(T).IsAssignableFrom(typeof(IMaterialForm)))
            {
                object ptr = this.Value;
                target = (T)ptr;
                return true;
            }

            // Cast to a Mesh if that is asked for.
            if (typeof(T).IsAssignableFrom(typeof(GH_Mesh)))
            {
                Mesh m = this.Value.getMesh();
                if (m.IsValid)
                {
                    object gHm = new GH_Mesh(m);
                    target = (T)gHm;
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
                this.Value = (IMaterialForm)source;
                return true;
            }
            return false;
        }

        public void DrawViewportWires(GH_PreviewWireArgs args)
        {
        }

        public void DrawViewportMeshes(GH_PreviewMeshArgs args)
        {
            args.Pipeline.DrawMeshShaded(this.Value.getMesh(), args.Material);
        }
    }

    // Grasshopper Parameter Wrapper
    public class GH_MaterialFormPar : GH_Param<GH_MaterialForm>, IGH_PreviewObject
    {
        public GH_MaterialFormPar() :
            base("Material Form", "MatForm", "Contains a collection of Material Forms", "CAMel", "  Params", GH_ParamAccess.item) { }

        public override Guid ComponentGuid
        {
            get { return new Guid("01d791bb-d6b8-42e3-a1ba-6aec037cacc3"); }
        }

        public bool Hidden { get; set; }
        public bool IsPreviewCapable => true;
        public BoundingBox ClippingBox => Preview_ComputeClippingBox();
        public void DrawViewportWires(IGH_PreviewArgs args) => Preview_DrawMeshes(args);
        public void DrawViewportMeshes(IGH_PreviewArgs args) => Preview_DrawMeshes(args);

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