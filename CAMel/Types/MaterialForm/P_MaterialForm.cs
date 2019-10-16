namespace CAMel.Types.MaterialForm
{
    using System;
    using System.Collections.Generic;

    using CAMel.Types.Machine;

    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Types;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    /// <summary>TODO The m fintersection.</summary>
    public struct MFintersection
    {
        /// <summary>Initializes a new instance of the <see cref="MFintersection"/> struct.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <param name="away">TODO The away.</param>
        /// <param name="lineP">TODO The line p.</param>
        public MFintersection(Point3d pt, Vector3d away, double lineP)
        {
            this.point = pt;
            this.lineP = lineP;
            this.isSet = true;
            this.away = away;
            this.away.Unitize();
        }

        /// <summary>Gets the point.</summary>
        public Point3d point { get; } // Point of intersection
        /// <summary>Gets the away.</summary>
        public Vector3d away { get; } // direction to get away from the material (eg normal)

        /// <summary>Gets the line p.</summary>
        public double lineP { get; } // position along intersecting line
        /// <summary>Gets a value indicating whether is set.</summary>
        public bool isSet { get; }
    }

    /// <summary>TODO The m fintersects.</summary>
    public class MFintersects
    {
        /// <summary>TODO The mid out 1.</summary>
        private Vector3d midOut1;
        /// <summary>Initializes a new instance of the <see cref="MFintersects"/> class.</summary>
        public MFintersects()
        {
            this.inters = new List<MFintersection>();
            this.through = new MFintersection(); // creates an unset value
            this.first = new MFintersection(); // creates and unset value
            this.midOut = new Vector3d();
        }

        /// <summary>Gets the inters.</summary>
        [NotNull]
        private List<MFintersection> inters { get; } // List of intersections

        /// <summary>TODO The thr dist.</summary>
        public double thrDist => this.through.lineP;
        /// <summary>TODO The first dist.</summary>
        public double firstDist => this.first.lineP;

        /// <summary>Gets the through.</summary>
        public MFintersection through { get; private set; } // intersection with highest lineParameter
        /// <summary>Gets the first.</summary>
        [PublicAPI]
        public MFintersection first { get; private set; } // intersection with lowest lineParameter

        /// <summary>TODO The mid.</summary>
        public Point3d mid => (1.5 * this.first.point + this.through.point) / 2.5; // midpoint through material

        // direction to head to surface from the middle of middle of the line
        /// <summary>Gets or sets the mid out.</summary>
        public Vector3d midOut
        {
            get => this.midOut1;
            set
            {
                value.Unitize();
                this.midOut1 = value;
            }
        }

        /// <summary>TODO The add.</summary>
        /// <param name="inter">TODO The inter.</param>
        [PublicAPI]
        public void add(MFintersection inter)
        {
            this.inters.Add(inter);

            if (!this.through.isSet || this.through.lineP < inter.lineP) { this.through = inter; }
            if (!this.first.isSet || this.first.lineP > inter.lineP) { this.first = inter; }
        }

        /// <summary>TODO The add.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <param name="away">TODO The away.</param>
        /// <param name="lineP">TODO The line p.</param>
        public void add(Point3d pt, Vector3d away, double lineP)
        {
            this.add(new MFintersection(pt, away, lineP));
        }

        /// <summary>TODO The count.</summary>
        public int count => this.inters.Count;

        /// <summary>TODO The hits.</summary>
        public bool hits => this.inters.Count > 0;
    }

    /// <summary>TODO The mf default.</summary>
    internal static class MFDefault
    {
        // Does the line intersect the surface of the material?
        /// <summary>TODO The line intersect.</summary>
        /// <param name="mF">TODO The m f.</param>
        /// <param name="start">TODO The start.</param>
        /// <param name="end">TODO The end.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <param name="inters">TODO The inters.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        internal static bool lineIntersect([NotNull] IMaterialForm mF, Point3d start, Point3d end, double tolerance, [CanBeNull] out MFintersects inters)
        {
            inters = mF.intersect(start, end - start, tolerance);
            double lLength = (end - start).Length;
            return inters.hits &&
                   (inters.firstDist > 0 && inters.firstDist < lLength ||
                    inters.thrDist > 0 && inters.thrDist < lLength);
        }

        /// <summary>TODO The refine.</summary>
        /// <param name="mF">TODO The m f.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="m">TODO The m.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
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

    /// <summary>TODO The MaterialForm interface.</summary>
    public interface IMaterialForm : ICAMelBase
    {
        /// <summary>Gets the safe distance.</summary>
        double safeDistance { get; }
        /// <summary>Gets the material tolerance.</summary>
        [UsedImplicitly]
        double materialTolerance { get; }

        /// <summary>TODO The intersect.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <param name="direction">TODO The direction.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <returns>The <see cref="MFintersects"/>.</returns>
        [NotNull]
        MFintersects intersect(Point3d pt, Vector3d direction, double tolerance);
        /// <summary>TODO The intersect.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <returns>The <see cref="MFintersects"/>.</returns>
        [NotNull]
        MFintersects intersect([NotNull] ToolPoint tP, double tolerance);
        /// <summary>TODO The intersect.</summary>
        /// <param name="start">TODO The start.</param>
        /// <param name="end">TODO The end.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <param name="inters">TODO The inters.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        bool intersect(Point3d start, Point3d end, double tolerance, [NotNull] out MFintersects inters);

        /// <summary>TODO The refine.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="m">TODO The m.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        [NotNull]
        ToolPath refine([NotNull] ToolPath tP, [NotNull] IMachine m);

        /// <summary>TODO The get mesh.</summary>
        /// <returns>The <see cref="Mesh"/>.</returns>
        [NotNull]
        Mesh getMesh();
        /// <summary>TODO The get brep.</summary>
        /// <returns>The <see cref="Brep"/>.</returns>
        [CanBeNull]
        Brep getBrep();
        /// <summary>TODO The get bounding box.</summary>
        /// <returns>The <see cref="BoundingBox"/>.</returns>
        BoundingBox getBoundingBox();
    }

    /// <summary>TODO The material form.</summary>
    public static class MaterialForm
    {
        // Currently links to grasshopper to use "CastTo" behaviours.
        /// <summary>TODO The create.</summary>
        /// <param name="inputGeometry">TODO The input geometry.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <param name="safeD">TODO The safe d.</param>
        /// <param name="mF">TODO The m f.</param>
        /// <returns>The <see cref="bool"/>.</returns>
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

        /// <summary>TODO The create.</summary>
        /// <param name="inputGeometry">TODO The input geometry.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <param name="safeD">TODO The safe d.</param>
        /// <returns>The <see cref="IMaterialForm"/>.</returns>
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

        /// <summary>TODO The create.</summary>
        /// <param name="iG">TODO The i g.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <param name="safeD">TODO The safe d.</param>
        /// <returns>The <see cref="IMaterialForm"/>.</returns>
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

        /// <summary>TODO The create.</summary>
        /// <param name="inputGeometry">TODO The input geometry.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <param name="safeD">TODO The safe d.</param>
        /// <returns>The <see cref="IMaterialForm"/>.</returns>
        [NotNull]
        // ReSharper disable once SuggestBaseTypeForParameter
        private static IMaterialForm create([NotNull] Mesh inputGeometry, double tolerance, double safeD)
        {
            if (!inputGeometry.HasBrepForm) { return create(inputGeometry.GetBoundingBox(false), tolerance, safeD); }
            Brep b = Brep.TryConvertBrep(inputGeometry);
            return b != null ? create(b, tolerance, safeD) : create(inputGeometry.GetBoundingBox(false), tolerance, safeD);
        }

        /// <summary>TODO The create.</summary>
        /// <param name="b">TODO The b.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <param name="safeD">TODO The safe d.</param>
        /// <returns>The <see cref="IMaterialForm"/>.</returns>
        [NotNull]
        private static IMaterialForm create(Box b, double tolerance, double safeD)
        {
            MFBox mB = new MFBox(b, tolerance, safeD);
            return mB;
        }

        /// <summary>TODO The create.</summary>
        /// <param name="bb">TODO The bb.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <param name="safeD">TODO The safe d.</param>
        /// <returns>The <see cref="IMaterialForm"/>.</returns>
        [NotNull]
        private static IMaterialForm create(BoundingBox bb, double tolerance, double safeD)
        {
            MFBox mB = new MFBox(new Box(bb), tolerance, safeD);
            return mB;
        }

        /// <summary>TODO The create.</summary>
        /// <param name="cy">TODO The cy.</param>
        /// <param name="tolerance">TODO The tolerance.</param>
        /// <param name="safeD">TODO The safe d.</param>
        /// <returns>The <see cref="IMaterialForm"/>.</returns>
        [NotNull]
        private static IMaterialForm create(Cylinder cy, double tolerance, double safeD)
        {
            MFCylinder mC = new MFCylinder(cy, tolerance, safeD);
            return mC;
        }
    }

    // Grasshopper Type Wrapper
    /// <summary>TODO The g h_ material form.</summary>
    public sealed class GH_MaterialForm : CAMel_Goo<IMaterialForm>, IGH_PreviewData
    {
        /// <summary>Initializes a new instance of the <see cref="GH_MaterialForm"/> class.</summary>
        [UsedImplicitly]
        public GH_MaterialForm() => this.Value = null;

        // Construct from unwrapped object
        /// <summary>Initializes a new instance of the <see cref="GH_MaterialForm"/> class.</summary>
        /// <param name="mF">TODO The m f.</param>
        public GH_MaterialForm([CanBeNull] IMaterialForm mF) => this.Value = mF;

        // Copy Constructor (just reference as MaterialForm is Immutable)
        /// <summary>Initializes a new instance of the <see cref="GH_MaterialForm"/> class.</summary>
        /// <param name="mF">TODO The m f.</param>
        public GH_MaterialForm([CanBeNull] GH_MaterialForm mF) => this.Value = mF?.Value;

        /// <inheritdoc />
        [NotNull]
        public override IGH_Goo Duplicate() => new GH_MaterialForm(this);

        /// <inheritdoc />
        public override bool CastTo<T>(ref T target)
        {
            if (this.Value == null) { return false; }

            // Trivial base case, we already have a IMaterialForm, the cast is safe
            if (typeof(T).IsAssignableFrom(typeof(IMaterialForm)))
            {
                object ptr = this.Value;
                target = (T)ptr;
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(GH_Brep)))
            {
                Brep b = this.Value.getBrep();
                if (b?.IsValid != true) { return false; }

                object gHm = new GH_Brep(b);
                target = (T)gHm;
                return true;
            }

            // Cast to a Mesh if that is asked for.
            // ReSharper disable once InvertIf
            if (typeof(T).IsAssignableFrom(typeof(GH_Mesh)))
            {
                Mesh m = this.Value.getMesh();
                if (!m.IsValid) { return false; }

                object gHm = new GH_Mesh(m);
                target = (T)gHm;
                return true;
            }

            return false;
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public BoundingBox ClippingBox => this.Value?.getBoundingBox() ?? BoundingBox.Unset;

        /// <inheritdoc />
        public void DrawViewportWires([CanBeNull] GH_PreviewWireArgs args) { }

        /// <inheritdoc />
        public void DrawViewportMeshes([CanBeNull] GH_PreviewMeshArgs args)
        {
            if (this.Value == null || args?.Pipeline == null) { return; }
            args.Pipeline.DrawMeshShaded(this.Value.getMesh(), args.Material);
        }
    }

    // Grasshopper Parameter Wrapper
    /// <summary>TODO The g h_ material form par.</summary>
    public class GH_MaterialFormPar : GH_Param<GH_MaterialForm>, IGH_PreviewObject
    {
        /// <summary>Initializes a new instance of the <see cref="GH_MaterialFormPar"/> class.</summary>
        public GH_MaterialFormPar()
            : base("Material Form", "MatForm", "Contains a collection of Material Forms", "CAMel", "  Params", GH_ParamAccess.item) { }

        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("01d791bb-d6b8-42e3-a1ba-6aec037cacc3");

        /// <inheritdoc />
        public bool Hidden { get; set; }
        /// <inheritdoc />
        public bool IsPreviewCapable => true;
        /// <inheritdoc />
        public BoundingBox ClippingBox => this.Preview_ComputeClippingBox();
        /// <inheritdoc />
        public void DrawViewportWires([CanBeNull] IGH_PreviewArgs args) => this.Preview_DrawMeshes(args);
        /// <inheritdoc />
        public void DrawViewportMeshes([CanBeNull] IGH_PreviewArgs args) => this.Preview_DrawMeshes(args);

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.materialform;
    }
}