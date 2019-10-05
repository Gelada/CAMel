using System;
using CAMel.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.GH
{
    [UsedImplicitly]
    public class C_SpiralSurfacePath : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the CreateToolPath class.
        /// </summary>
        public C_SpiralSurfacePath()
            : base("Create Spiral Surfacing Path", "SurfacePath",
                "Create a spiral surfacing recipe",
                "CAMel", " ToolPaths") { }

        // put this item in the second batch (surfacing strategies)
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddGenericParameter("Bounding Box", "BB",
                "Region to Mill as a bounding box oriented by Dir, will be calculated if you add the Mesh or Brep to Mill.",
                GH_ParamAccess.item);
            pManager.AddCurveParameter("Curve", "C", "Curve (not currently used)", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[1].Optional = true; // Curve
            pManager.AddPlaneParameter("Plane", "Pl", "Plane to use, Start above origin on Z.", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddNumberParameter("Radius", "R", "Radius of sphere, use 0 for a spiral on a plane.",
                GH_ParamAccess.item, 0.0);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[3].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddIntegerParameter("Tool Direction", "TD", "Method used to calculate tool direction for 5-Axis\n 0: Projection\n 1: Path Tangent\n 2: Path Normal\n 3: Normal", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Step over", "SO", "Stepover as a multiple of tool width. Default to Tools side load (for negative values).", GH_ParamAccess.item, -1);
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_SurfacePathPar(), "SurfacePath", "SP", "Surfacing Path", GH_ParamAccess.item);
            //pManager.AddCurveParameter("Paths", "P", "Paths", GH_ParamAccess.list);
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
            Curve c = null; // path to use in future.
            Plane dir = Plane.WorldXY; // Plane to rotate in as you rise.
            MaterialTool mT = null; // The materialtool, mainly for tool width
            int tD = 0;
            double stepOver = 0;
            double r = 0;

            if (!da.GetData(0, ref geom)) { return; }
            da.GetData(1, ref c);
            if (!da.GetData(2, ref dir)) { return; }
            if (!da.GetData(3, ref r)) { return; }
            if (!da.GetData(4, ref mT)) { return; }
            if (!da.GetData(5, ref tD)) { return; }
            if (!da.GetData(6, ref stepOver)) { return; }

            if (stepOver < 0) { stepOver = mT.sideLoad; }
            if (stepOver > mT.sideLoad) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Stepover exceeds suggested sideLoad for the material/tool."); }

            // process the bounding box

            if (!geom.CastTo(out BoundingBox bb))
            {
                if (geom.CastTo(out Surface s))
                {
                    bb = s.GetBoundingBox(dir); // extents of S in the coordinate system
                    dir.Origin = dir.PointAt(bb.Center.X, bb.Center.Y, 0); // Centre everything
                    bb = s.GetBoundingBox(dir); // extents of S in the coordinate system
                }
                else if (geom.CastTo(out Brep b))
                {
                    bb = b.GetBoundingBox(dir); // extents of S in the coordinate system
                    dir.Origin = dir.PointAt(bb.Center.X, bb.Center.Y, 0); // Centre everything
                    bb = b.GetBoundingBox(dir); // extents of S in the coordinate system
                }
                else if (geom.CastTo(out Mesh m))
                {
                    bb = m.GetBoundingBox(dir); // extents of S in the coordinate system
                    dir.Origin = dir.PointAt(bb.Center.X, bb.Center.Y, 0); // Centre everything
                    bb = m.GetBoundingBox(dir); // extents of S in the coordinate system
                }
                else
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The region to mill (BB) must be a bounding box, surface, mesh or brep.");
                }
            }

            // set Surfacing direction
            SurfToolDir sTd;
            switch (tD)
            {
                case 0:
                    sTd = SurfToolDir.Projection;
                    break;
                case 1:
                    sTd = SurfToolDir.PathTangent;
                    break;
                case 2:
                    sTd = SurfToolDir.PathNormal;
                    break;
                case 3:
                    sTd = SurfToolDir.Normal;
                    break;
                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        "Input parameter TD can only have values 0,1,2 or 3");
                    return;
            }

            SurfacePath sP = Surfacing.spiral(c, dir, r, stepOver, sTd, bb, mT);
            da.SetData(0, new GH_SurfacePath(sP));
            //DA.SetDataList(1, Paths);
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.surfacingspiral;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{DB426DC7-60B0-4597-97F7-86B94BF28301}");
    }
}