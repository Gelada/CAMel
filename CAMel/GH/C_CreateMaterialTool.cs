namespace CAMel.GH
{
    using System;

    using CAMel.Types;

    using Grasshopper.Kernel;

    using JetBrains.Annotations;

    [UsedImplicitly]
    public class C_CreateMaterialTool : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_CreateMaterialTool()
            : base(
                "Create Material Tool", "MaterialTool",
                "Give details of the material to cut and the tool cutting it",
                "CAMel", " Hardware") { }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddTextParameter("Material Name", "MN", "Name of the material", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Tool Name", "TN", "Name of the tool", GH_ParamAccess.item, string.Empty);
            pManager.AddIntegerParameter("Tool Number", "T", "Number of the tool", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("Speed", "S", "Speed of Spindle", GH_ParamAccess.item, -1);
            pManager.AddNumberParameter("Cut Feed", "CF", "Feed rate when cutting", GH_ParamAccess.item);
            pManager.AddNumberParameter("Plunge Feed", "PF", "Feed rate when plunging into material", GH_ParamAccess.item);
            pManager.AddNumberParameter("Cut Depth", "CD", "Maximum depth of material to cut (negative values will allow cuts as deep as needed).", GH_ParamAccess.item, -1);
            pManager.AddNumberParameter("Finish Depth", "FD", "Maximum depth of material in final cut (negative values will allow cuts as deep as needed).", GH_ParamAccess.item, -1);
            pManager.AddNumberParameter("Tolerance", "To", "Tolerance when converting curves to toolpaths", GH_ParamAccess.item, 0.005);
            pManager.AddNumberParameter("minStep", "mS", "Minimum distance between machine positions", GH_ParamAccess.item, .05);
            pManager.AddNumberParameter("Tool Width", "TW", "Width of tool", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Insert Width", "IW", "Width needed for tool insertion (for example for plasma);", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Tool Length", "TL", "Length of tool from last pivot (not needed for 3 Axis).", GH_ParamAccess.item, 0);
            pManager.AddTextParameter("Tool Shape", "TS", "End shape of tool (Ball, Square, V, Other).", GH_ParamAccess.item, "Other");
            pManager.AddNumberParameter("Side Load", "SL", "Fraction of the tool to engage with the material when surfacing.", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("Path Jump", "PJ", "Maximum distance allowed between toolPaths in material.", GH_ParamAccess.item, -1);
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_MaterialToolPar(), "MaterialTool", "MT", "Details of material and the tool cutting it", GH_ParamAccess.item);
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }
            string matName = string.Empty;
            string toolName = string.Empty;

            int T = 1;
            double s = 0, cf = 0, pf = 0, cd = 0, fd = 0, to = 0, mS = 0, tW = 0, iW = 0, tL = 0, sL = 1, pJ = -1;

            string toolShape = string.Empty;

            if (!da.GetData("Material Name", ref matName)) { return; }
            if (!da.GetData("Tool Name", ref toolName)) { return; }
            if (!da.GetData("Tool Number", ref T)) { return; }
            if (!da.GetData("Speed", ref s)) { return; }
            if (!da.GetData("Cut Feed", ref cf)) { return; }
            if (!da.GetData("Plunge Feed", ref pf)) { return; }
            if (!da.GetData("Cut Depth", ref cd)) { return; }
            if (!da.GetData("Finish Depth", ref fd)) { return; }
            if (!da.GetData("Tolerance", ref to)) { return; }
            if (!da.GetData("minStep", ref mS)) { return; }
            if (!da.GetData("Tool Width", ref tW)) { return; }
            if (!da.GetData("Insert Width", ref iW)) { return; }
            if (!da.GetData("Tool Length", ref tL)) { return; }
            if (!da.GetData("Tool Shape", ref toolShape)) { return; }
            if (!da.GetData("Side Load", ref sL)) { return; }
            if (!da.GetData("Path Jump", ref pJ)) { return; }

            EndShape eS = MaterialTool.getToolShape(toolShape);

            if (eS == EndShape.Error)
            {
                AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Warning,
                    "End Shape not recognised. Options are \"Ball\", \"Square\", \"V\" use \"Other\" to avoid warning.");
                eS = EndShape.Other;
            }

            MaterialTool mT = new MaterialTool(matName, toolName, T, s, cf, pf, cd, fd, tW, iW, tL, eS, to, mS, sL, pJ);

            da.SetData(0, new GH_MaterialTool(mT));
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.creatematerialtool;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{C67DDF9A-A893-4393-B9C9-FC6CB5F304DA}");
    }
}