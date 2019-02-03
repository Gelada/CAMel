using System;

using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using CAMel.Types;

namespace CAMel
{
    public class C_ParallelSurfacePath : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateToolPath class.
        /// </summary>
        public C_ParallelSurfacePath()
            : base("Create Parallel Surfacing Path", "SurfacePath",
                "Create a parallel surfacing recipe",
                "CAMel", " ToolPaths")
        {
        }

        // put this item in the second batch (surfacing strategies)
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Surface", "S", "Brep or Mesh to Mill", GH_ParamAccess.item);
            pManager.AddCurveParameter("Curve", "C", "Curve to run parallel to", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Direction", "Dir", "Plane to use, -Z is projection direction, curve moves parallel to Y.", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.item);
            pManager[3].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddIntegerParameter("Tool Direction", "TD", "Method used to calculate tool direction for 5-Axis\n 0: Projection\n 1: Path Tangent\n 2: Path Normal\n 3: Normal", GH_ParamAccess.item,0);
            pManager.AddNumberParameter("Step over", "SO", "Stepover as a mutliple of tool width. Default to Tools side load(for negative values).", GH_ParamAccess.item, -1);
            pManager.AddBooleanParameter("Zig and Zag", "Z", "Go forward and back, or just forward along path", GH_ParamAccess.item, true);
           
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_SurfacePathPar(), "SurfacePath", "SP", "Surfacing Path", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            IGH_Goo G = null;
            BoundingBox BB = new BoundingBox(); // region to mill
            Curve C = null; // path to move parallel to 
            Plane Dir = Plane.WorldXY; // Direction to project onto the surface
            MaterialTool MT = null; // The materialtool, mainly for tool width
            int TD=0;
            double stepOver = 0;
            bool ZZ = true; // ZigZag if true, Zig if false

            if (!DA.GetData(0, ref G)) { return; }
            DA.GetData(1, ref C);
            if (!DA.GetData(2, ref Dir)) { return; }
            if (!DA.GetData(3, ref MT)) { return; }
            if (!DA.GetData(4, ref TD)) { return; }
            if (!DA.GetData(5, ref stepOver)) { return; }
            if (!DA.GetData(6, ref ZZ)) { return; }

            if (stepOver < 0) { stepOver = MT.sideLoad; }
            if (stepOver > MT.sideLoad) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Stepover exceeds suggested sideLoad for the material/tool."); }

            // process the bounding box

            if (!G.CastTo<BoundingBox>(out BB))
            {
                if (G.CastTo<Surface>(out Surface S))
                { BB = S.GetBoundingBox(Dir); }     // extents of S in the coordinate system
                else if (G.CastTo<Brep>(out Brep B))
                { BB = B.GetBoundingBox(Dir); }     // extents of B in the coordinate system 
                else if (G.CastTo<Mesh>(out Mesh M))
                { BB = M.GetBoundingBox(Dir); }     // extents of M in the coordinate system
                else
                { this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The region to mill (BB) must be a bounding box, surface, mesh or brep."); }
                BB.Inflate(MT.toolWidth);
            }

            // set Surfacing direction
            SurfToolDir STD;
            switch (TD)
            {
                case 0: STD = SurfToolDir.Projection; break;
                case 1: STD = SurfToolDir.PathTangent; break;
                case 2: STD = SurfToolDir.PathNormal; break;
                case 3: STD = SurfToolDir.Normal; break;
                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input parameter TD can only have values 0,1,2 or 3");
                    return;
            }

            SurfacePath SP = Surfacing.parallel(C, Dir, stepOver,ZZ, STD, BB, MT);
            DA.SetData(0, new GH_SurfacePath(SP));

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
                return Properties.Resources.surfacingzigzag;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{974D5053-AD40-40E6-9163-7110F345C98D}"); }
        }
    }
}