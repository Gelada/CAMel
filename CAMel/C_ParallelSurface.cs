using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
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
            pManager.AddGenericParameter("Material Tool", "MT", "Information about the material and tool", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Tool Direction", "TD", "Method used to calculate tool direction for 5-Axis\n 0: Projection\n 1: Path Tangent\n 2: Path Normal\n 3: Normal", GH_ParamAccess.item,0);
            pManager.AddNumberParameter("Step over", "SO", "Stepover as a mutliple of tool width. Default .5.", GH_ParamAccess.item, 0.5);
            pManager.AddBooleanParameter("Zig and Zag", "Z", "Go forward and back, or just forward along path", GH_ParamAccess.item, true);
           
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("SurfacePath", "SP", "Surfacing Path", GH_ParamAccess.item);
            pManager.AddGenericParameter("Paths", "P", "Paths", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GeometryBase S = null; //surface to mill
            Curve C = null; // path to move parallel to 
            Plane Dir = Plane.WorldXY; // Direction to project onto the surface
            MaterialTool MT = null; // The materialtool, mainly for tool width
            int TD=0;
            double stepOver = 0;
            bool ZZ = true; // ZigZag if true, Zig if false
            bool createcurve = false; // was a curve passed in or do we go to default/

            if (!DA.GetData(0, ref S)) { return; }
            if (!DA.GetData(1, ref C))
            {
                createcurve = true;
            }
            if (!DA.GetData(2, ref Dir)) { return; }
            if (!DA.GetData(3, ref MT)) { return; }
            if (!DA.GetData(4, ref TD)) { return; }
            if (!DA.GetData(5, ref stepOver)) { return; }
            if (!DA.GetData(6, ref ZZ)) { return; }

            // set Surfacing direction
            SurfToolDir STD;
            switch (TD)
            {
                case 0:
                    STD = SurfToolDir.Projection;
                    break;
                case 1:
                    STD = SurfToolDir.PathTangent;
                    break;
                case 2:
                    STD = SurfToolDir.PathNormal;
                    break;
                case 3:
                    STD = SurfToolDir.Normal;
                    break;
                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input parameter TD can only have values 0,1,2 or 3");
                    return;
            }

            // Find surface bounding box to find our extents
            
            BoundingBox BB = S.GetBoundingBox(Dir); // extents of S in the coordinate system

            if(createcurve) // default to curve running along X-direction on Plane. 
            {
                C = new LineCurve(Dir.PointAt(BB.Min.X, BB.Min.Y), Dir.PointAt(BB.Max.X, BB.Min.Y));
            }
            BoundingBox BBC = C.GetBoundingBox(Dir); // bounding box for curve
            
            List<Curve> Paths = new List<Curve>(); // Curves to use
            Curve TempC = C.DuplicateCurve();
            TempC.Translate((Vector3d)Dir.PointAt(0, BB.Min.Y-BBC.Max.Y, BB.Max.Z - BBC.Min.Z+0.1));
            // create enough curves to guarantee covering surface
            for (double width = 0; width <= BB.Max.Y-BB.Min.Y + BBC.Max.Y -BBC.Min.Y; width = width+stepOver*MT.toolWidth)
            {
                TempC.Translate((Vector3d)Dir.PointAt(0, stepOver * MT.toolWidth, 0));
                Paths.Add(TempC.DuplicateCurve());
            }

            SurfacePath SP = new SurfacePath(Paths, -Dir.ZAxis, STD);
            DA.SetData(0, SP);
            DA.SetDataList(1, Paths);

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