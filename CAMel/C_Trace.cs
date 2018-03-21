using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using ImageMagick;

namespace CAMel
{
    public class C_Trace : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateToolPath class.
        /// </summary>
        public C_Trace()
            : base("Trace hand drawn path ", "Trace",
                "Trace a path from a photo of a hand drawn image",
                "CAMel", " ToolPaths")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File", "F", "Name of image file", GH_ParamAccess.item,"");
            pManager.AddNumberParameter("P1", "P1", "First Utility Parameter", GH_ParamAccess.item,0);
            pManager.AddNumberParameter("P2", "P2", "Second Utility Parameter", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curve", "C", "Traced Curves", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string file = "";
            double P1 = 0, P2 = 0;

            if (!DA.GetData(0, ref file)) return;
            if (!DA.GetData(1, ref P1)) return;
            if (!DA.GetData(2, ref P2)) return;

            List<Curve> curves = new List<Curve>();

            MagickImage image = new MagickImage(file);

            MagickImage edge = (MagickImage) image.Clone();

            edge.CannyEdge();

            edge.Write("Edges.jpg");

            DA.SetData(0, curves);
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
                return Properties.Resources.createoperations;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{7270759F-B5DA-46BB-B459-C98250ABB995}"); }
        }
    }
}