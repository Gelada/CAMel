using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types;
using CAMel.Types.MaterialForm;
using static CAMel.Types.Operations;

namespace CAMel
{
    public class C_DrillOperation : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_DrillOperation()
            : base("Drill Operation", "Drill",
                "Create a Machine Operation drilling at a certain point.",
                "CAMel", " Operations")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCircleParameter("Drill Points", "D", "A list of circles. The centre of each circle gives the position to drill, the orientation of the circle gives the tool direction and the radius gives the depth.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Peck Depth", "P", "Depth of Peck", GH_ParamAccess.item, 0);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.item);
            pManager[2].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddParameter(new GH_MaterialFormPar(), "Material Form", "MF", "The MaterialForm giving the position of the material", GH_ParamAccess.item);
            pManager[3].WireDisplay = GH_ParamWireDisplay.faint;
            pManager[3].Optional = true; // MatForm
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_MachineOperationPar(), "Operation", "O", "Machine Operation", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Circle D = new Circle();
            double peck = 0;
            MaterialTool MT = null;
            IMaterialForm MF = null;

            if (!DA.GetData(0, ref D)) { return; }
            if (!DA.GetData(1, ref peck)) { return; }
            if (!DA.GetData(2, ref MT)) { return; }
            DA.GetData(3, ref MF);

            if (D.Normal.Length == 0)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Cannot process a circle who's normal is given as the zero vector. Check for null inputs."); }

            MachineOperation Op = drillOperation(D, peck, MT, MF);

            DA.SetData(0, new GH_MachineOperation(Op));
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
                return Properties.Resources.drilloperations;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{9AC2C9BE-AFF2-4644-9A4F-E5D2E4A5EA65}"); }
        }
    }
}