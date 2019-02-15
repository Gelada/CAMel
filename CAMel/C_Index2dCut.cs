using System;

using Rhino.Geometry;

using Grasshopper.Kernel;

using CAMel.Types;
using CAMel.Types.MaterialForm;

namespace CAMel
{
    public class C_Index2DCut : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_Index2DCut()
            : base("2D Cut", "2D",
                "Create a Machine Operations cutting out 2D shapes.",
                "CAMel", " Operations")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Cut Path", "C", "Outline of object to cut out.", GH_ParamAccess.item);
            pManager.AddVectorParameter("Direction", "D", "Direction of the tool.", GH_ParamAccess.item, new Vector3d(0, 0, 1));
            pManager.AddNumberParameter("Offset", "O", "Offset Curve, 0 for none, -1 for interior, 1 for exterior. Actually uses half tool thickness scaled by this value, so variation by toolpath is possible.", GH_ParamAccess.item,0);
            var tPApar = new GH_ToolPathAdditionsPar();
            tPApar.SetPersistentData(new GH_ToolPathAdditions(ToolPathAdditions.basicDefault));
            pManager.AddParameter(tPApar,"Additions","TPA", "Additional Processing for the path, note some options like step down might be ignored by some machines.",GH_ParamAccess.item);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.item);
            pManager[4].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddParameter(new GH_MaterialFormPar(), "Material Form", "MF", "The MaterialForm giving the position of the material", GH_ParamAccess.item);
            pManager[5].WireDisplay = GH_ParamWireDisplay.faint;
            pManager[5].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_MachineOperationPar(), "Operation", "O", "2d cut Operation", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            Curve c = null;
            Vector3d dir = new Vector3d();
            double os = 0;
            ToolPathAdditions tPa = null;

            MaterialTool mT = null;
            IMaterialForm mF = null;

            if (!da.GetData(0, ref c)) { return; }
            if (!da.GetData(1, ref dir)) { return; }
            if (!da.GetData(2, ref os)) { return; }
            if (!da.GetData(3, ref tPa)) { return; }
            if (!da.GetData(4, ref mT)) { return; }
            da.GetData(5, ref mF);
            
            if (Rhino.Geometry.Intersect.Intersection.CurveSelf(c, 0.00000001).Count > 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Self-intersecting Curve");
                return;
            }
            
            tPa =tPa.deepClone();

            if (!c.IsClosed && Math.Abs(os) > CAMel_Goo.tolerance)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Curves that are not closed will not be offset.");
                os = 0;
            }

            // Note multiplication will give negative only if one is positive and the other negative.
            if (os < 0) {tPa.leadCurvature = -tPa.leadCurvature; }
            MachineOperation mO = Operations.opIndex2DCut(c, dir, os, tPa, mT, mF);

            da.SetData(0, new GH_MachineOperation(mO));
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
                return Properties.Resources.index2dcutting;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{D5C2A2B0-D57C-45D6-BF96-90536C726A04}"); }
        }
    }
}