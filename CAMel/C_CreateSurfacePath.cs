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
        protected override void RegisterInputParams(GH_InputParamManager pManager)
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
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_SurfacePathPar(), "SurfacePath", "SP", "Surfacing Path", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            List<Curve> paths = new List<Curve>();
            int prT;
            double prD = 0;
            Curve cc = null;
            Vector3d dir = new Vector3d(0, 0, -1);
            Point3d cen = new Point3d(0, 0, 0);
            int tD;
            double tDd = 0;

            SurfacePath sP;

            if (!da.GetDataList(0, paths)) { return; }
            if (!da.GetData(5, ref tDd)) { return; }
            tD = (int)tDd;
            // set Surfacing direction
            SurfToolDir sTD;
            switch (tD)
            {
                case 0:
                    sTD = SurfToolDir.Projection;
                    break;
                case 1:
                    sTD = SurfToolDir.PathTangent;
                    break;
                case 2:
                    sTD = SurfToolDir.PathNormal;
                    break;
                case 3:
                    sTD = SurfToolDir.Normal;
                    break;
                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input parameter TD can only have values 0,1,2 or 3");
                return;
            }

            // find the projection type (will effect the information we wish to use)
            if (!da.GetData(1, ref prD)) { return; }
            prT = (int)prD;
            switch (prT)
            {
                case 0: // Parallel
                    if (!da.GetData(3, ref dir)) { return; }
                    sP = new SurfacePath(paths, dir, sTD);
                    break;
                case 1: // Cylindrical
                    if (!da.GetData(2, ref cc)) { return; }
                    if (!da.GetData(3, ref dir)) { return; }
                    sP = new SurfacePath(paths,dir,cc,sTD);
                    break;
                case 2: // Spherical
                    if (!da.GetData(4, ref cen)) { return; }
                    sP = new SurfacePath(paths, cen, sTD);
                    break;
                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input parameter Pr can only have values 0,1 or 2");
                return;
            }

            da.SetData(0, new GH_SurfacePath(sP));
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