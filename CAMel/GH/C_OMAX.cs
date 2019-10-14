namespace CAMel.GH
{
    using System;
    using System.Collections.Generic;

    using CAMel.Types;
    using CAMel.Types.Machine;

    using Grasshopper.Kernel;

    using JetBrains.Annotations;

    [UsedImplicitly]
    public class C_OMAX : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_OMAX()
            : base(
                "OMAX Waterjet", "OMAX",
                "Waterjet cutter from OMAX corporation",
                "CAMel", " Hardware") { }

        // put this item in the second batch (Machines)
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddNumberParameter("Tilt Max", "TMax", "Maximum value (+ or -) that the tool can tilt from vertical", GH_ParamAccess.item, 59);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material Tools", "MTs", "Material Tool pairs used by the machine", GH_ParamAccess.list);
            // ReSharper disable once PossibleNullReferenceException
            pManager[1].WireDisplay = GH_ParamWireDisplay.faint;
            pManager[1].Optional = true;
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_MachinePar(), "Machine", "M", "Details for a PocketNC 5-axis machine", GH_ParamAccess.item);
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }
            double tiltMax = 0;
            List<MaterialTool> mTs = new List<MaterialTool>();

            if (!da.GetData("Tilt Max", ref tiltMax)) { return; }
            da.GetDataList("Material Tools", mTs);

            Omax5 m = new Omax5("OMAX machine", mTs, tiltMax * Math.PI / 180.0);

            da.SetData(0, new GH_Machine(m));
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.create5axis;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{245E3C42-31A3-4787-872A-BFC40BFE05AF}");
    }
}