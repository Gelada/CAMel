namespace CAMel.GH
{
    using System;
    using System.Collections.Generic;

    using CAMel.Types;
    using CAMel.Types.MaterialForm;

    using Grasshopper.Kernel;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    /// <inheritdoc />
    /// <summary>TODO The c_ create tool path.</summary>
    [UsedImplicitly]
    public class C_CreateToolPath : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the CreateToolPath class.
        /// </summary>
        public C_CreateToolPath()
            : base(
                "Create Tool Path", "ToolPath",
                "Create a toolpath from lists of points and directions (if > 3axis)",
                "CAMel", " ToolPaths") { }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddPlaneParameter("Path", "P", "List of positions and orientations for tool, as a Plane with origin at tool position and Z-Axis for tool direction. ", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Mat", "M", "List of Material Orientations (for machines that can move material and tool separately).", GH_ParamAccess.list, Plane.WorldXY);
            pManager.AddVectorParameter("Speed and Feed", "SF", "List of vectors giving speed (X) and feed (Y) at each toolpoint.", GH_ParamAccess.list, new Vector3d(-1, -1, 0));
            pManager.AddTextParameter("pre Code", "prC", "List of additional CNC codes to run before the points. The code will run on the same line. Use a newline at end to run on the previous line.", GH_ParamAccess.list, string.Empty);
            pManager.AddTextParameter("post Code", "poC", "List of additional CNC codes to run after the points. The code will run on the same line. Use a newline at start to run on the next line.", GH_ParamAccess.list, string.Empty);
            pManager.AddTextParameter("Name", "N", "Name of path", GH_ParamAccess.item, string.Empty);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[6].Optional = true; // MatTool
            pManager[6].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddParameter(new GH_MaterialFormPar(), "Material Form", "MF", "The MaterialForm giving the position of the material", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[7].Optional = true; // MatForm
            pManager[7].WireDisplay = GH_ParamWireDisplay.faint;
            GH_ToolPathAdditionsPar tPaPar = new GH_ToolPathAdditionsPar();
            tPaPar.SetPersistentData(new GH_ToolPathAdditions(ToolPathAdditions.basicDefault));
            pManager.AddParameter(tPaPar, "Additions", "TPA", "Additional operations to apply to the path before cutting. \n" + "Left click and choose \"Manage ToolPathAdditions Collection\" to create.", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[8].Optional = true; // ToolPathAdditions
            pManager.AddTextParameter("Code", "C", "Addition CNC code to run before this path.", GH_ParamAccess.item, string.Empty);
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_ToolPathPar(), "ToolPath", "TP", "Complete ToolPath", GH_ParamAccess.item);
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }
            List<Plane> pts = new List<Plane>();
            List<Plane> mat = new List<Plane>();
            List<Vector3d> sF = new List<Vector3d>();
            List<string> preCode = new List<string>();
            List<string> postCode = new List<string>();
            string name = string.Empty;
            MaterialTool mT = null;
            IMaterialForm mF = null;
            string co = string.Empty;
            ToolPathAdditions tPa = ToolPathAdditions.temp;

            if (!da.GetDataList("Path", pts)) { return; }
            if (!da.GetDataList("Mat", mat)) { return; }
            if (!da.GetDataList(2, sF)) { return; }
            if (!da.GetDataList(3, preCode)) { return; }
            if (!da.GetDataList(4, postCode)) { return; }
            if (!da.GetData(5, ref name)) { return; }
            da.GetData(6, ref mT);
            da.GetData(7, ref mF);
            da.GetData(8, ref tPa);
            if (!da.GetData(9, ref co)) { return; }

            ToolPath tP = new ToolPath(name, mT, mF, tPa);

            if ((mat.Count == 1 || mat.Count == pts.Count) &&
                (sF.Count == 1 || sF.Count == pts.Count) &&
                (preCode.Count == 1 || preCode.Count == pts.Count) &&
                (postCode.Count == 1 || postCode.Count == pts.Count))
            {
                for (int i = 0; i < pts.Count; i++)
                {
                    Plane useMat = mat.Count == 1 ? mat[0] : mat[i];

                    Vector3d useSF = sF.Count == 1 ? sF[0] : sF[i];

                    string usePreCo = preCode.Count == 1 ? preCode[0] : preCode[i];

                    string usePostCo = postCode.Count == 1 ? postCode[0] : postCode[i];

                    tP.Add(new ToolPoint(pts[i], useMat, usePreCo, usePostCo, useSF.X, useSF.Y));
                }
            }
            else
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The lists of material position and speeds/feeds must be a single item or the same length as the list of points.");
            }

            da.SetData(0, new GH_ToolPath(tP));
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.createtoolpath;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{bd9a76f0-e75a-44e3-ad24-4bc6168ffe8f}");
    }
}