namespace CAMel.GH
{
    using System;

    using CAMel.Types;
    using CAMel.Types.MaterialForm;

    using Grasshopper.Kernel;

    using JetBrains.Annotations;

    using Rhino.Geometry;
    using Rhino.Geometry.Intersect;

    /// <inheritdoc />
    /// <summary>TODO The c_ index 2 d cut.</summary>
    [UsedImplicitly]
    public class C_Index2DCut : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_Index2DCut()
            : base(
                "2D Cut", "2D",
                "Create a Machine Operations cutting out 2D shapes.",
                "CAMel", " Operations") { }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddCurveParameter("Cut Path", "C", "Outline of object to cut out.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Offset", "O", "Offset Curve, 0 for none, -1 for interior, 1 for exterior. Actually uses half tool thickness scaled by this value, so variation by toolpath is possible.", GH_ParamAccess.item, 0);
            pManager.AddVectorParameter("Direction", "D", "Direction of the tool.", GH_ParamAccess.item, new Vector3d(0, 0, 1));
            GH_ToolPathAdditionsPar tPaPar = new GH_ToolPathAdditionsPar();
            tPaPar.SetPersistentData(new GH_ToolPathAdditions(ToolPathAdditions.basicDefault));
            pManager.AddParameter(tPaPar, "Additions", "TPA", "Additional Processing for the path, note some options like step down might be ignored by some machines.", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[4].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddParameter(new GH_MaterialFormPar(), "Material Form", "MF", "The MaterialForm giving the position of the material", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[5].WireDisplay = GH_ParamWireDisplay.faint;
            pManager[5].Optional = true;
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_MachineOperationPar(), "Operation", "O", "2d cut Operation", GH_ParamAccess.item);
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }

            Curve c = null;
            Vector3d dir = new Vector3d();
            double os = 0;
            ToolPathAdditions tPa = null;

            MaterialTool mT = null;
            IMaterialForm mF = null;

            if (!da.GetData(0, ref c)) {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter C failed to collect data");
                return;
            }
            if (!da.GetData(2, ref dir))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter D failed to collect data");
                return;
            }
            if (!da.GetData(1, ref os))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter O failed to collect data");
                return;
            }
            if (!da.GetData(3, ref tPa) || tPa == null)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter TPA failed to collect data");
                return;
            }
            if (!da.GetData(4, ref mT))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter mT failed to collect data");
                return;
            }
            if (!da.GetData(5, ref mF))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter mF failed to collect data");
                return;
            }

            CurveIntersections cI = Intersection.CurveSelf(c, 0.00000001);
            if (cI != null && cI.Count > 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Self-intersecting Curve");
                return;
            }

            tPa = tPa.deepClone();

            if (Math.Abs(os) < CAMel_Goo.Tolerance) { tPa.leadCurvature = string.Empty; }

            MachineOperation mO = Operations.opIndex2DCut(c, dir, os, tPa, mT, mF);

            da.SetData(0, new GH_MachineOperation(mO));
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.index2dcutting;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{D5C2A2B0-D57C-45D6-BF96-90536C726A04}");
    }
}