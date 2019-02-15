using System;
using System.Collections.Generic;

using Rhino.Geometry;

using Grasshopper.Kernel;

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
        protected override void RegisterInputParams(GH_InputParamManager pManager)
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
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_MachineOperationPar(), "Operation", "O", "Machine Operation", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            List<Curve> c = new List<Curve>();
            Vector3d dir = new Vector3d();
            ToolPathAdditions tPa = null;
            MaterialTool mT = null;
            IMaterialForm mF = null;


            if (!da.GetDataList(0, c)) { return;}
            if (!da.GetData(1, ref dir)) { return;}
            da.GetData(2, ref tPa);
            if (!da.GetData(3, ref mT)) { return; }
            da.GetData(4, ref mF);

            int invalidCurves;

            MachineOperation mO = Operations.opIndex3Axis(c, dir, tPa, mT, mF, out invalidCurves);

            if( invalidCurves > 1)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "A total of " + invalidCurves.ToString() + " invalid curves (probably nulls) were ignored."); }
            else if (invalidCurves > 0)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "An invalid curve (probably a null) was ignored."); }

            if (mO.Count > 0) { da.SetData(0, new GH_MachineOperation(mO)); }
            else { da.SetData(0, null); }
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