using System;

using Rhino.Geometry;

using Grasshopper.Kernel;

using CAMel.Types;
using CAMel.Types.MaterialForm;

namespace CAMel
{
    enum SurfaceType
	{
	   Brep, Mesh
	}
    public class C_Surfacing : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_Surfacing()
            : base("Surfacing Operation", "Surface",
                "Create a Machine Operations to create a surface.",
                "CAMel", " Operations")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Surface", "S","The surface, brep or mesh to carve", GH_ParamAccess.item);
            pManager.AddParameter(new GH_SurfacePathPar(),"Rough Path", "R", "Information to create roughing path", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool Rough", "MTR", "The material to cut and the tool to do it for roughing", GH_ParamAccess.item);
            pManager[2].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddParameter(new GH_SurfacePathPar(), "Finish Path", "F", "Information to create finishing path", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool Finish", "MTF", "The material to cut and the tool to do it for finishing", GH_ParamAccess.item);
            pManager[4].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddParameter(new GH_MaterialFormPar(), "Material Form", "MF", "The MaterialForm giving the position of the material", GH_ParamAccess.item);
            pManager[5].WireDisplay = GH_ParamWireDisplay.faint;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_MachineOperationPar(), "Rough", "R", "Roughing Operation", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MachineOperationPar(), "Finish", "F", "Finishing Operation", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            SurfaceType sT;
            GeometryBase geom = null;
            Mesh m = null;
            Brep b = null;
            SurfacePath roughP = null;
            SurfacePath finalP = null;
            MaterialTool mTr = null;
            MaterialTool mTf = null;
            IMaterialForm mF = null;

            if (!da.GetData(0, ref geom)) { return; }
            if(geom.GetType() == typeof(Mesh))
            {
                sT = SurfaceType.Mesh;
                m = (Mesh)geom;
            } else if(geom.GetType() == typeof(Brep))
            {
                b = (Brep)geom;
                sT = SurfaceType.Brep;
            } else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The surface parameter must be a Brep, Surface or Mesh");
                return;
            }
            if (!da.GetData(1, ref roughP)) { return; }
            if (!da.GetData(2, ref mTr)) { return; }
            if (!da.GetData(3, ref finalP)) { return; }
            if (!da.GetData(4, ref mTf)) { return; }
            if (!da.GetData(5, ref mF)) { return; }

            MachineOperation roughO = new MachineOperation();
            MachineOperation finishO = new MachineOperation();

            ToolPathAdditions addRough = new ToolPathAdditions
            {
                // Additions for Roughing toolpath
                insert = true,
                retract = true,
                stepDown = true,
                sdDropStart = true,
                sdDropMiddle = 8 * mF.safeDistance,
                sdDropEnd = true,
                threeAxisHeightOffset = false
            };
            mTr = MaterialTool.changeFinishDepth(mTr, mTr.cutDepth); // ignore finish depth for roughing

            ToolPathAdditions addFinish = new ToolPathAdditions
            {
                // Additions for Finishing toolpath
                insert = true,
                retract = true,
                stepDown = false,
                sdDropStart = false,
                sdDropMiddle = 0.0,
                sdDropEnd = false,
                threeAxisHeightOffset = false
            };

            switch (sT)
            {
                case SurfaceType.Brep:
                    roughO = roughP.generateOperation(b, mTf.finishDepth, mTr, mF, addRough);
                    finishO = finalP.generateOperation(b, 0.0, mTf, mF, addFinish);
                    break;
                case SurfaceType.Mesh:
                    roughO = roughP.generateOperation(m, mTf.finishDepth, mTr, mF, addRough);
                    finishO = finalP.generateOperation(m, 0.0, mTf, mF, addFinish);
                    break;
            }
            roughO.name = "Rough " + roughO.name;
            finishO.name = "Finish " + finishO.name;

            da.SetData(0, new GH_MachineOperation(roughO));
            da.SetData(1, new GH_MachineOperation(finishO));
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
                return Properties.Resources.pathsurfacing;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{993EE7E5-6D31-4DFD-B4C4-813098BCB628}"); }
        }
    }
}