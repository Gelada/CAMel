namespace CAMel.GH
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using CAMel.Types;

    using Grasshopper.Kernel;

    using JetBrains.Annotations;

    /// <inheritdoc />
    /// <summary>TODO The c_ to tool points.</summary>
    [UsedImplicitly]
    public class C_ToToolPoints : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the CreateInstructions class.
        /// </summary>
        public C_ToToolPoints()
            : base(
                "To ToolPoints", "ToolPoints",
                "Extract a list of toolpoints from a ToolPath, Machine Operation or Machine Instructions.",
                "CAMel", " ToolPaths") { }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddGenericParameter("ToolPoint Container", "T", "Objects containing ToolPoints", GH_ParamAccess.item);
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_ToolPointPar(), "ToolPoints", "TP", "ToolPoints contained in input", GH_ParamAccess.list);
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }
            IToolPointContainer tP = null;

            if (!da.GetData(0, ref tP))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Only objects containing ToolPoints can be converted.");
                return;
            }

            List<GH_ToolPoint> tPtPs = tP.getSinglePath().Select(tPt => new GH_ToolPoint(tPt)).ToList();

            da.SetDataList(0, tPtPs);
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.totoolpoints;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{33AC9946-1940-47FC-8DD5-21CAA650CD18}");
    }
}