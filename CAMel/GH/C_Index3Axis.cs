using System;
using System.Collections.Generic;
using CAMel.Types;
using CAMel.Types.MaterialForm;
using Grasshopper.Kernel;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.GH
{
    [UsedImplicitly]
    public class C_Index3Axis : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_Index3Axis()
            : base("Index 3 Axis", "Index",
                "Create a machine operation from a collection of paths, with 3 Axis operation with the tool in a single orientation.",
                "CAMel", " Operations")
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddCurveParameter("Curves", "C", "The curves for the tip of the tool to follow", GH_ParamAccess.list);
            pManager.AddVectorParameter("Direction", "D", "Direction of the tool.", GH_ParamAccess.item, new Vector3d(0,0,1));
            GH_ToolPathAdditionsPar tPaPar = new GH_ToolPathAdditionsPar();
            tPaPar.SetPersistentData(new GH_ToolPathAdditions(ToolPathAdditions.basicDefault));
            pManager.AddParameter(tPaPar, "Additions", "TPA", "Additional operations to apply to the path before cutting. \n" +
                "Left click and choose \"Manage ToolPathAdditions Collection\" to create.", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[2].Optional = true; // ToolPathAdditions
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[3].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddParameter(new GH_MaterialFormPar(), "Material Form", "MF", "The MaterialForm giving the position of the material", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[4].WireDisplay = GH_ParamWireDisplay.faint;
            pManager[4].Optional = true; // MaterialForm
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_MachineOperationPar(), "Operation", "O", "Machine Operation", GH_ParamAccess.item);
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }
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

            MachineOperation mO = Operations.opIndex3Axis(c, dir, tPa, mT, mF, out int invalidCurves);

            if( invalidCurves > 1)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "A total of " + invalidCurves + " invalid curves (probably nulls) were ignored."); }
            else if (invalidCurves > 0)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "An invalid curve (probably a null) was ignored."); }

            da.SetData(0, mO.Count > 0 ? new GH_MachineOperation(mO) : null);
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.index3axis;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{81951E44-9F56-4A4C-BCD5-5B1976B335E2}");
    }
}