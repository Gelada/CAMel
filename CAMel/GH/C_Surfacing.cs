using System;
using CAMel.Types;
using CAMel.Types.MaterialForm;
using Grasshopper.Kernel;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.GH
{
    [UsedImplicitly]
    public class C_Surfacing : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_Surfacing()
            : base("Surfacing Operation", "Surface",
                "Create a Machine Operations to create a surface.",
                "CAMel", " Operations") { }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddGeometryParameter("Surface", "S", "The surface, brep or mesh to carve", GH_ParamAccess.item);
            pManager.AddParameter(new GH_SurfacePathPar(), "Rough Path", "R", "Information to create roughing path", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool Rough", "MTR", "The material to cut and the tool to do it for roughing", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[2].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddParameter(new GH_SurfacePathPar(), "Finish Path", "F", "Information to create finishing path", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool Finish", "MTF", "The material to cut and the tool to do it for finishing", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[4].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddParameter(new GH_MaterialFormPar(), "Material Form", "MF", "The MaterialForm giving the position of the material", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[5].WireDisplay = GH_ParamWireDisplay.faint;
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_MachineOperationPar(), "Rough", "R", "Roughing Operation", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MachineOperationPar(), "Finish", "F", "Finishing Operation", GH_ParamAccess.item);
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }
            GeometryBase geom = null;
            SurfacePath roughP = null;
            SurfacePath finalP = null;
            MaterialTool mTr = null;
            MaterialTool mTf = null;
            IMaterialForm mF = null;

            if (!da.GetData(0, ref geom)) { return; }
            if (!da.GetData(1, ref roughP)) { return; }
            if (!da.GetData(2, ref mTr)) { return; }
            if (!da.GetData(3, ref finalP)) { return; }
            if (!da.GetData(4, ref mTf)) { return; }
            if (!da.GetData(5, ref mF)) { return; }

            ToolPathAdditions addRough = new ToolPathAdditions
            {
                // Additions for Roughing toolpath
                insert = true,
                retract = true,
                stepDown = true,
                sdDropStart = true,
                sdDropMiddle = -1,
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

            MachineOperation roughO;
            MachineOperation finishO;
            if (geom.GetType() == typeof(Mesh))
            {
                Mesh m = (Mesh) geom;
                roughO = roughP.generateOperation(m, mTf.finishDepth, mTr, mF, addRough);
                finishO = finalP.generateOperation(m, 0.0, mTf, mF, addFinish);
            }
            else if (geom.GetType() == typeof(Brep))
            {
                Brep b = (Brep) geom;
                roughO = roughP.generateOperation(b, mTf.finishDepth, mTr, mF, addRough);
                finishO = finalP.generateOperation(b, 0.0, mTf, mF, addFinish);
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The surface parameter must be a Brep, Surface or Mesh");
                return;
            }

            roughO.name = "Rough " + roughO.name;
            finishO.name = "Finish " + finishO.name;

            da.SetData(0, new GH_MachineOperation(roughO));
            da.SetData(1, new GH_MachineOperation(finishO));
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.pathsurfacing;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{993EE7E5-6D31-4DFD-B4C4-813098BCB628}");
    }
}