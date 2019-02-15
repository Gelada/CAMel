using System;
using System.Collections.Generic;

using Grasshopper.Kernel;

using CAMel.Types;

namespace CAMel
{
    public class C_ToToolPoints : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateInstructions class.
        /// </summary>
        public C_ToToolPoints()
            : base("To ToolPoints", "ToolPoints",
                "Extract a list of toolpoints from a ToolPath, Machine Operation or Machine Instructions.",
                "CAMel", " ToolPaths")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("ToolPoint Container", "T", "Objects containing ToolPoints", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_ToolPointPar(),"ToolPoints", "TP", "ToolPoints contained in input", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            IToolPointContainer tP = null;

            if (!da.GetData(0, ref tP))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Only objects containing ToolPoints can be converted.");
                return;
            }

            List<GH_ToolPoint> tPtPs = new List<GH_ToolPoint>();
            foreach(ToolPoint tPt in tP.getSinglePath())
            { tPtPs.Add(new GH_ToolPoint(tPt)); }

            da.SetDataList(0, tPtPs);
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
                return Properties.Resources.totoolpoints;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{33AC9946-1940-47FC-8DD5-21CAA650CD18}"); }
        }
    }
}