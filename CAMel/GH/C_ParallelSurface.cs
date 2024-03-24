namespace CAMel.GH
{
    using System;
    using System.Collections.Generic;
    using CAMel.Types;

    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Types;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    /// <inheritdoc />
    /// <summary>TODO The c_ parallel surface path.</summary>
    [UsedImplicitly]
    public class C_ParallelSurfacePath : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the CreateToolPath class.
        /// </summary>
        public C_ParallelSurfacePath()
            : base(
                "Create Parallel Surfacing Path", "SurfacePath",
                "Create a parallel surfacing recipe",
                "CAMel", " ToolPaths") { }

        // put this item in the second batch (surfacing strategies)
        /// <inheritdoc />
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddGeometryParameter("Surface", "S", "Brep or Mesh to Mill", GH_ParamAccess.item);
            pManager.AddCurveParameter("Curve", "C", "Curve to run parallel to", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Direction", "Dir", "Plane to use, -Z is projection direction, curve moves parallel to Y.", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[3].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddIntegerParameter("Tool Direction", "TD", "Method used to calculate tool direction for 5-Axis\n 0: Projection\n 1: Path Tangent\n 2: Path Normal\n 3: Normal\n 4: Material Normal", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Step over", "SO", "Stepover as a multiple of tool width. Default to Tools side load(for negative values).", GH_ParamAccess.item, -1);
            pManager.AddIntegerParameter("Style", "St", "Choose features by adding, +4 lift tool to stop gouging rather than offsetting, +2 precise boundary, +1 back and forth.", GH_ParamAccess.item, 5);
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_SurfacePathPar(), "SurfacePath", "SP", "Surfacing Path", GH_ParamAccess.item);
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }
            IGH_Goo geom = null;
            Curve c = null; // path to move parallel to
            Plane dir = Plane.WorldXY; // Direction to project onto the surface
            MaterialTool mT = null; // The materialtool, mainly for tool width
            int tD = 0;
            double stepOver = 0;
            int style = 5; // +4 lift not offset, +2 precise boundary, +1 zig zag not just zig

            if (!da.GetData(0, ref geom)) { return; }
            da.GetData(1, ref c);
            if (!da.GetData(2, ref dir)) { return; }
            if (!da.GetData(3, ref mT)) { return; }
            if (!da.GetData(4, ref tD)) { return; }
            if (!da.GetData(5, ref stepOver)) { return; }
            if (!da.GetData(6, ref style)) { return; }

            // extract bits from style
            bool zz = (style & 1) == 1;
            bool precise = (style & 2) == 2;
            bool lift = (style & 4) == 4;

            if (stepOver < 0) { stepOver = mT.sideLoad; }
            if (stepOver > mT.sideLoad) { this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Stepover exceeds suggested sideLoad for the material/tool."); }

            Vector3d lineDir = c.PointAtEnd - c.PointAtStart; // get y direction from the input curve
            lineDir.Transform(Transform.PlaneToPlane(Plane.WorldXY, dir));
            lineDir.Z = 0;

            Plane uDir = new Plane(dir.Origin, lineDir, Vector3d.CrossProduct(dir.ZAxis, lineDir));
            PolylineCurve bc;
            Mesh m = new Mesh();

            // process the bounding curve
            if (!geom.CastTo(out bc))
            {
                if (geom.CastTo(out Curve cbc))
                {
                    if(precise)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Surface, Brep or mesh must be given for precise boundary surfacing.");
                        return;
                    }
                    bc = cbc.ToPolyline(mT.tolerance, 0.01, mT.minStep, mT.toolWidth * 10);
                }
                if (geom.CastTo(out Surface s))
                {
                    m = Mesh.CreateFromSurface(s, MeshingParameters.FastRenderMesh);
                    m.Weld(Math.PI);
                    bc = new PolylineCurve(Shadows.MeshShadow(m, dir));
                } 
                else if (geom.CastTo(out Brep b))
                {
                    m = Mesh.CreateFromBrep(b, MeshingParameters.FastRenderMesh)[0];
                    m.Weld(Math.PI);
                    bc = new PolylineCurve(Shadows.MeshShadow(m, dir));
                } 
                else if (geom.CastTo(out m)) 
                { 
                    m.Weld(Math.PI);
                    bc = new PolylineCurve(Shadows.MeshShadow(m, dir)); 
                } 
                else
                { this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The region to mill must be a curve, surface, mesh or brep."); }
            }

            // set Surfacing direction
            SurfToolDir sTD = SurfacePath.getSurfDir(tD);
            if (sTD == SurfToolDir.Error)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input parameter TD does not correspond to a tool direction");
                return;
            }

            if (precise)
            {
                SurfacePath sP = Surfacing.PreciseParallel(c, uDir, stepOver, zz, sTD, m, mT, lift);
                da.SetData(0, new GH_SurfacePath(sP));
            }
            else
            {
                // make shadow a little smaller
                ToolPath bcTp = new ToolPath(mT);
                bcTp.convertCurve(bc, dir.ZAxis);

                bcTp = ToolPath.planeOffset(bcTp, uDir.ZAxis * mT.toolWidth * CAMel_Goo.surfaceEdge, dir.ZAxis)[0];

                SurfacePath sP = Surfacing.parallel(c, uDir, stepOver, zz, sTD, bcTp.getLine(), mT, lift);
                da.SetData(0, new GH_SurfacePath(sP));
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.surfacingzigzag;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{974D5053-AD40-40E6-9163-7110F345C98D}");
    }
}