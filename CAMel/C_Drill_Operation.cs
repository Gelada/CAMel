using System;

using Rhino.Geometry;

using Grasshopper.Kernel;

using CAMel.Types;
using CAMel.Types.MaterialForm;
using JetBrains.Annotations;
using static CAMel.Types.Operations;

namespace CAMel
{
    [UsedImplicitly]
    public class C_DrillOperation : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_DrillOperation()
            : base("Drill Operation", "Drill",
                "Create a Machine Operation drilling at a certain point.",
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
            pManager.AddCircleParameter("Drill Points", "D", "A list of circles. The centre of each circle gives the position to drill, the orientation of the circle gives the tool direction and the radius gives the depth.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Peck Depth", "P", "Depth of Peck", GH_ParamAccess.item, 0);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[2].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddParameter(new GH_MaterialFormPar(), "Material Form", "MF", "The MaterialForm giving the position of the material", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[3].WireDisplay = GH_ParamWireDisplay.faint;
            pManager[3].Optional = true; // MatForm
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
            Circle circ = new Circle();
            double peck = 0;
            MaterialTool mT = null;
            IMaterialForm mF = null;

            if (!da.GetData(0, ref circ)) { return; }
            if (!da.GetData(1, ref peck)) { return; }
            if (!da.GetData(2, ref mT)) { return; }
            da.GetData(3, ref mF);

            if (Math.Abs(circ.Normal.Length) < CAMel_Goo.Tolerance)
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Cannot process a circle who's normal is given as the zero vector. Check for null inputs."); }

            MachineOperation mO = drillOperation(circ, peck, mT, mF);

            da.SetData(0, new GH_MachineOperation(mO));
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.drilloperations;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{9AC2C9BE-AFF2-4644-9A4F-E5D2E4A5EA65}");
    }
}