using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types;
using CAMel.Types.MaterialForm;

namespace CAMel
{
    public class C_Index3Axis : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_Index3Axis()
            : base("Index 3 Axis", "Index",
                "Create a machine operation from a collection of paths, with 3 Axis operation with the tool in a single orientation.",
                "CAMel", " Operations")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "The curves for the tip of the tool to follow", GH_ParamAccess.list);
            pManager.AddVectorParameter("Direction", "D", "Direction of the tool.", GH_ParamAccess.item, new Vector3d(0,0,1));
            var tPApar = new GH_ToolPathAdditionsPar();
            tPApar.SetPersistentData(new GH_ToolPathAdditions(ToolPathAdditions.basicDefault));
            pManager.AddParameter(tPApar, "Additions", "TPA", "Additional operations to apply to the path before cutting. \n" +
                "Left click and choose \"Manage ToolPathAdditions Collection\" to create.", GH_ParamAccess.item);
            pManager[2].Optional = true; // ToolPathAdditions
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.item);
            pManager[3].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddParameter(new GH_MaterialFormPar(), "Material Form", "MF", "The MaterialForm giving the position of the material", GH_ParamAccess.item);
            pManager[4].WireDisplay = GH_ParamWireDisplay.faint;
            pManager[4].Optional = true; // MaterialForm
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
            List<Curve> C = new List<Curve>();
            Vector3d Dir = new Vector3d();
            ToolPathAdditions tPA = null;
            MaterialTool MT = null;
            IMaterialForm MF = null;


            if (!DA.GetDataList(0, C)) { return;}
            if (!DA.GetData(1, ref Dir)) { return;}
            DA.GetData(2, ref tPA);
            if (!DA.GetData(3, ref MT)) { return; }
            DA.GetData(4, ref MF);

            int invalidCurves = 0;

            MachineOperation Op = Operations.opIndex3Axis(C, Dir, tPA, MT, MF, out invalidCurves);

            if( invalidCurves > 1)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "A total of " + invalidCurves.ToString() + " invalid curves (probably nulls) were ignored."); }
            else if (invalidCurves > 0)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "An invalid curve (probably a null) was ignored."); }

            if (Op.Count > 0) { DA.SetData(0, new GH_MachineOperation(Op)); }
            else { DA.SetData(0, null); }
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
                return Properties.Resources.index3axis;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{81951E44-9F56-4A4C-BCD5-5B1976B335E2}"); }
        }
    }
}