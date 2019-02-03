using System;
using System.Collections.Generic;

using Rhino.Geometry;

using Grasshopper.Kernel;

using CAMel.Types;

namespace CAMel
{
    public class C_CreateSurfacePath : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateToolPath class.
        /// </summary>
        public C_CreateSurfacePath()
            : base("Create Surfacing Path", "SurfacePath",
                "Create a surfacing recipe",
                "CAMel", " ToolPaths")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Paths", "P", "Paths to project onto surface", GH_ParamAccess.list);
            pManager.AddNumberParameter("Projection", "Pr", "Type of projection to use.\n 0: Parallel\n 1: Cylindrical\n 2: Spherical", GH_ParamAccess.item,0);
            pManager.AddCurveParameter("Centre Curve", "CC", "Central Curve for cylindrical projection", GH_ParamAccess.item);
            pManager.AddVectorParameter("Direction", "Dir", "Direction for parallel projection or orthogonal direction for cylindrical", GH_ParamAccess.item, new Vector3d(0, 0, -1));
            pManager.AddPointParameter("Centre", "C", "Centre for spherical projection", GH_ParamAccess.item, new Point3d(0, 0, 0));
            pManager.AddNumberParameter("Tool Direction", "TD", "Method used to calculate tool direction for 5-Axis\n 0: Projection\n 1: Path Tangent\n 2: Path Normal\n 3: Normal", GH_ParamAccess.item,0);
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
            List<Curve> Paths = new List<Curve>();
            int Pr = 0;
            double Prd = 0;
            Curve CC = null;
            Vector3d Dir = new Vector3d(0, 0, -1);
            Point3d Cen = new Point3d(0, 0, 0);
            int TD = 0;
            double TDd = 0;

            SurfacePath SP;

            if (!DA.GetDataList(0, Paths)) { return; }
            if (!DA.GetData(5, ref TDd)) { return; }
            TD = (int)TDd;
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

            // find the projection type (will effect the information we wish to use)
            if (!DA.GetData(1, ref Prd)) { return; }
            Pr = (int)Prd;
            switch (Pr)
            {
                case 0: // Parallel
                    if (!DA.GetData(3, ref Dir)) { return; }
                    SP = new SurfacePath(Paths, Dir, STD);
                    break;
                case 1: // Cylindrical
                    if (!DA.GetData(2, ref CC)) { return; }
                    if (!DA.GetData(3, ref Dir)) { return; }
                    SP = new SurfacePath(Paths,Dir,CC,STD);
                    break;
                case 2: // Spherical
                    if (!DA.GetData(4, ref Cen)) { return; }
                    SP = new SurfacePath(Paths, Cen, STD);
                    break;
                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input parameter Pr can only have values 0,1 or 2");
                return;
            }

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
                return Properties.Resources.createsurfacepath;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{B68B11F6-3E9E-461A-B677-AFD890015BD3}"); }
        }
    }
}