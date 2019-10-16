namespace CAMel.GH
{
    using System;

    using CAMel.Types.MaterialForm;

    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Types;

    using JetBrains.Annotations;

    /// <inheritdoc />
    /// <summary>TODO The c_ create material form.</summary>
    [UsedImplicitly]
    public class C_CreateMaterialForm : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_CreateMaterialForm()
            : base(
                "Create Material Form", "MaterialForm",
                "Give details of the position of material to cut",
                "CAMel", " Hardware") { }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }

            /* TODO This needs to be replaced with the new material form accepting either a list of boxes,
            // a plane, or a list of box unions (need good name) all into one parameter
            // Edit: 19/5/17 or does it? Needs more thought.*/

            pManager.AddGenericParameter("Geometry", "G", "Object containing material, can be a plane (material on negative side) a box or a Cylinder.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Safe Distance", "SD", "Safe distance away from material", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("Tolerance", "T", "Tolerance of material positioning", GH_ParamAccess.item, .1);
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_MaterialFormPar(), "MaterialForm", "MF", "Details of material position", GH_ParamAccess.item);
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }

            IGH_Goo geom = null;
            double sd = 0, t = 0;

            if (!da.GetData(0, ref geom)) { return; }
            if (!da.GetData(1, ref sd)) { return; }
            if (!da.GetData(2, ref t)) { return; }

            if (MaterialForm.create(geom, t, sd, out IMaterialForm mf))
            {
                da.SetData(0, new GH_MaterialForm(mf));
            }
            else
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Material Form can currently only work with a Box or a Cylinder. ");
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.creatematerialform;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{91182D6D-3BE6-4B46-AFE7-3DFDD947CBCE}");
    }
}