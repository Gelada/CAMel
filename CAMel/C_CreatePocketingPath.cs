using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace CAMel
{
    public class C_CreatePocketingPath : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the C_CreatePocketingPath class.
        /// </summary>
        public C_CreatePocketingPath()
          : base("Create Pocketing Path", "PocketPath",
              "Create a pocket from any closed curve",
              "CAMel", " ToolPaths")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Pocket Boundary", "C", "Closed boundary curve", GH_ParamAccess.list);
            pManager.AddNumberParameter("Depth", "D", "Depth of the pocket to be formed", GH_ParamAccess.item, 1.0);
            pManager.AddVectorParameter("Direction", "V", "Direction of the pocket to be formed", GH_ParamAccess.item, new Vector3d(0.0, 0.0, -1.0));
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("PocketPath", "PP", "Pocketing Path", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
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
                return Properties.Resources.pocketing;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{b9654cee-01d0-4429-aaab-bd472fbb29a1}"); }
        }
    }
}