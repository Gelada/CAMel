using System;
using System.Collections.Generic;
using CAMel.Types.Machine;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.Types.MaterialForm
{
    public struct MFintersection
    {
        public MFintersection(Point3d pt, Vector3d away, double lineP)
        {
            this.point = pt;
            this.lineP = lineP;
            this.isSet = true;
            this.away = away;
            this.away.Unitize();
        }

        public Point3d point { get; } // Point of intersection
        public Vector3d away { get; } // direction to get away from the material (eg normal)

        public double lineP { get; } // position along intersecting line
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
        [NotNull]
        private List<MFintersection> inters { get; } // List of intersections

        public double thrDist => this.through.lineP;
        public double firstDist => this.first.lineP;

        public MFintersection through { get; private set; } // intersection with highest lineParameter
        [PublicAPI] public MFintersection first { get; private set; } // intersection with lowest lineParameter

        public Point3d mid => (1.5*this.first.point + this.through.point) / 2.5; // midpoint through material

        private Vector3d _midOut;

        public Vector3d midOut
        {
            // direction to head to surface from the middle of middle of the line
            get => this._midOut;
            set
            {
                value.Unitize();
                this._midOut = value;
            }
        }

        [PublicAPI]
        public void add(MFintersection inter)
        {
            this.inters.Add(inter);

            if (!this.through.isSet || this.through.lineP < inter.lineP) { this.through = inter; }
            if (!this.first.isSet || this.first.lineP > inter.lineP) { this.first = inter; }
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
        // Does the line intersect the surface of the material?
        internal static bool lineIntersect([NotNull] IMaterialForm mF, Point3d start, Point3d end, double tolerance, [CanBeNull] out MFintersects inters)
        {
            inters = mF.intersect(start, end - start, tolerance);
            double lLength = (end - start).Length;
            return inters.hits &&
                   (inters.firstDist > 0 && inters.firstDist < lLength ||
                    inters.thrDist > 0 && inters.thrDist < lLength);
        }

        [NotNull]
        internal static ToolPath refine([NotNull] IMaterialForm mF, [NotNull] ToolPath tP, [NotNull] IMachine m)
        {
            if (tP.matTool == null) { Exceptions.matToolException(); }
            // for each line check if it intersects
            // the MF and add those points.
            // also add the midpoint if going more than half way through
            // TODO problem of long lines getting deep

            ToolPath refined = tP.deepCloneWithNewPoints(new List<ToolPoint>());

            // Add the first ToolPoint

            if (tP.Count > 0) { refined.Add(tP.firstP); }

            // TODO refine on significant changes of direction
            for (int i = 0; i < tP.Count - 1; i++)
            {
                // for every line between points check if we leave or enter the material

                if (mF.intersect(tP[i].pt, tP[i + 1].pt, 0, out MFintersects inters))
                {
                    double lineLen = (tP[i + 1].pt - tP[i].pt).Length;

                    if (inters.firstDist > 0) // add first intersection if on line
                    {
                        refined.Add(m.interpolate(tP[i], tP[i + 1], tP.matTool, inters.firstDist / lineLen, false));
                    }

                    if (inters.firstDist > 0 && lineLen > inters.thrDist) // add midpoint of intersection if it passes right through
                    {
                        refined.Add(m.interpolate(tP[i], tP[i + 1], tP.matTool, (inters.firstDist + inters.thrDist) / (2.0 * lineLen), false));
                    }
                    if (lineLen > inters.thrDist) // add last intersection if on line
                    {
                        refined.Add(m.interpolate(tP[i], tP[i + 1], tP.matTool, inters.thrDist / lineLen, false));
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
        [UsedImplicitly] double materialTolerance { get; }

        [NotNull] MFintersects intersect(Point3d pt, Vector3d direction, double tolerance);
        [NotNull] MFintersects intersect([NotNull] ToolPoint tP, double tolerance);
        bool intersect(Point3d start, Point3d end, double tolerance, [NotNull] out MFintersects inters);

        [NotNull] ToolPath refine([NotNull] ToolPath tP, [NotNull] IMachine m);

        [NotNull] Mesh getMesh();
        BoundingBox getBoundingBox();
    }

    public static class MaterialForm
    {
        // Currently links to grasshopper to use "CastTo" behaviours.
        public static bool create([NotNull] IGH_Goo inputGeometry, double tolerance, double safeD, [CanBeNull] out IMaterialForm mF)
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

        [NotNull]
        private static IMaterialForm create([NotNull] Surface inputGeometry, double tolerance, double safeD)
        {
            if (!inputGeometry.TryGetCylinder(out Cylinder cy))
            {
                return create(inputGeometry.GetBoundingBox(false), tolerance, safeD);
            }
            // Cope with rhinoBug in TryGetCylinder
            BoundingBox bb = inputGeometry.GetBoundingBox(cy.CircleAt(0).Plane);
            cy.Height1 = bb.Min.Z;
            cy.Height2 = bb.Max.Z;
            return create(cy, tolerance, safeD);
        }

        [NotNull]
        private static IMaterialForm create([NotNull] Brep iG, double tolerance, double safeD)
        {
            if (iG.Surfaces == null || iG.Surfaces.Count != 1 || iG.Surfaces[0] == null ||
                !iG.Surfaces[0].TryGetCylinder(out Cylinder cy))
            {
                return create(iG.GetBoundingBox(false), tolerance, safeD);
            }
            // Cope with rhinoBug in TryGetCylinder
            BoundingBox bb = iG.GetBoundingBox(cy.CircleAt(0).Plane);
            cy.Height1 = bb.Min.Z;
            cy.Height2 = bb.Max.Z;
            return create(cy, tolerance, safeD);
        }

        [NotNull]
        // ReSharper disable once SuggestBaseTypeForParameter
        private static IMaterialForm create([NotNull] Mesh inputGeometry, double tolerance, double safeD)
        {
            if (!inputGeometry.HasBrepForm) { return create(inputGeometry.GetBoundingBox(false), tolerance, safeD); }
            Brep b = Brep.TryConvertBrep(inputGeometry);
            return b != null ? create(b, tolerance, safeD) : create(inputGeometry.GetBoundingBox(false), tolerance, safeD);
        }

        [NotNull]
        private static IMaterialForm create(Box b, double tolerance, double safeD)
        {
            MFBox mB = new MFBox(b, tolerance, safeD);
            return mB;
        }

        [NotNull]
        private static IMaterialForm create(BoundingBox bb, double tolerance, double safeD)
        {
            MFBox mB = new MFBox(new Box(bb), tolerance, safeD);
            return mB;
        }

        [NotNull]
        private static IMaterialForm create(Cylinder cy, double tolerance, double safeD)
        {
            MFCylinder mC = new MFCylinder(cy, tolerance, safeD);
            return mC;
        }
    }

// Grasshopper Type Wrapper
    public sealed class GH_MaterialForm : CAMel_Goo<IMaterialForm>, IGH_PreviewData
    {
        [UsedImplicitly] public GH_MaterialForm() { this.Value = null; }
        // Construct from unwrapped object
        public GH_MaterialForm([CanBeNull] IMaterialForm mF) { this.Value = mF; }
        // Copy Constructor (just reference as MaterialForm is Immutable)
        public GH_MaterialForm([CanBeNull] GH_MaterialForm mF) { this.Value = mF?.Value; }
        // Duplicate
        [NotNull] public override IGH_Goo Duplicate() => new GH_MaterialForm(this);

        public override bool CastTo<T>(ref T target)
        {
            if (this.Value == null) { return false; }
            // Trivial base case, we already have a IMaterialForm, the cast is safe
            if (typeof(T).IsAssignableFrom(typeof(IMaterialForm)))
            {
                object ptr = this.Value;
                target = (T) ptr;
                return true;
            }

            // Cast to a Mesh if that is asked for.
            // ReSharper disable once InvertIf
            if (typeof(T).IsAssignableFrom(typeof(GH_Mesh)))
            {
                Mesh m = this.Value.getMesh();
                if (!m.IsValid) { return false; }

                object gHm = new GH_Mesh(m);
                target = (T) gHm;
                return true;
            }
            return false;
        }

        public override bool CastFrom([CanBeNull] object source)
        {
            switch (source) {
                case null: return false;
                case IMaterialForm mF:
                    this.Value = mF;
                    return true;
                default: return false;
            }
        }

        public BoundingBox ClippingBox => this.Value?.getBoundingBox() ?? BoundingBox.Unset;

        public void DrawViewportWires([CanBeNull] GH_PreviewWireArgs args) { }

        public void DrawViewportMeshes([CanBeNull] GH_PreviewMeshArgs args)
        {
            if (this.Value == null || args?.Pipeline == null) { return; }
            args.Pipeline.DrawMeshShaded(this.Value.getMesh(), args.Material);
        }
    }

// Grasshopper Parameter Wrapper
    public class GH_MaterialFormPar : GH_Param<GH_MaterialForm>, IGH_PreviewObject
    {
        public GH_MaterialFormPar() :
            base("Material Form", "MatForm", "Contains a collection of Material Forms", "CAMel", "  Params", GH_ParamAccess.item) { }

        public override Guid ComponentGuid => new Guid("01d791bb-d6b8-42e3-a1ba-6aec037cacc3");

        public bool Hidden { get; set; }
        public bool IsPreviewCapable => true;
        public BoundingBox ClippingBox => Preview_ComputeClippingBox();
        public void DrawViewportWires([CanBeNull] IGH_PreviewArgs args) => Preview_DrawMeshes(args);
        public void DrawViewportMeshes([CanBeNull] IGH_PreviewArgs args) => Preview_DrawMeshes(args);

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.materialform;
    }
}