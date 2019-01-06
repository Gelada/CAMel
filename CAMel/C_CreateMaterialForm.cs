using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types;
using CAMel.Types.MaterialForm;

namespace CAMel
{
    public class C_CreateMaterialForm : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_CreateMaterialForm()
            : base("Create Material Form", "MaterialForm",
                "Give details of the position of material to cut",
                "CAMel", " Hardware")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // TODO This needs to be replaced with the new material form accepting either a list of boxes, 
            // a plane, or a list of box unions (need good name) all into one parameter
            // Edit: 19/5/17 or does it? Needs more thought.
            
            pManager.AddGenericParameter("Geometry", "G", "Object containing material, can be a plane (material on negative side) a box or a Cylinder.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Safe Distance", "SD", "Safe distance away from material", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("Tolerance", "T", "Tolerance of material positioning", GH_ParamAccess.item, .1);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_MaterialFormPar(),"MaterialForm", "MF", "Details of material position", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            IGH_Goo G = null;
            double SD = 0, T=0;

            if (!DA.GetData(0, ref G)) { return; }
            if (!DA.GetData(1, ref SD)) { return; }
            if (!DA.GetData(2, ref T)) { return; }
            IMaterialForm MF = null;

            if (MaterialForm.create(G, T, SD, out MF))
            {
                DA.SetData(0, new GH_MaterialForm(MF));
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Material Form can currently only work with a Box or a Cylinder. ");
            }

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
                return Properties.Resources.creatematerialform;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{91182D6D-3BE6-4B46-AFE7-3DFDD947CBCE}"); }
        }
    }
}