using System;
using System.Collections.Generic;
using CAMel.Types;
using CAMel.Types.Machine;
using Grasshopper.Kernel;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.GH
{
    [UsedImplicitly]
    public class C_ReadCode : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_ReadCode()
            : base("Read CNC Code", "Read",
                "Read CNC Code",
                "CAMel", "CNC Code") { }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddTextParameter("Code", "C", "CNC code file", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MachinePar(), "Machine", "M", "Machine to read code", GH_ParamAccess.item);
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_ToolPathPar(), "ToolPath", "TP", "Full toolpath described by the file", GH_ParamAccess.item);
            pManager.AddPointParameter("Points", "Pts", "Position of the machine", GH_ParamAccess.list);
            pManager.AddVectorParameter("Directions", "Dirs", "Direction of the tool", GH_ParamAccess.list);
            pManager.AddVectorParameter("Speeds and Feeds", "SF", "Vectors with speeds (X) and feeds (Y).", GH_ParamAccess.list);
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }

            string code = string.Empty;
            IMachine m = null;

            if (!da.GetData(0, ref code)) { return; }
            if (!da.GetData(1, ref m)) { return; }

            MachineInstruction mI = m.readCode(code);

            List<Point3d> selPt = new List<Point3d>();
            List<Vector3d> selDir = new List<Vector3d>();

            foreach (ToolPoint tp in mI.getSinglePath())
            {
                selPt.Add(tp.pt);
                selDir.Add(tp.dir);
            }

            da.SetData(0, new GH_MachineInstruction(mI));
            da.SetDataList(1, selPt);
            da.SetDataList(2, selDir);
            da.SetDataList(3, mI.getSinglePath().getSpeedFeed());
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.cncreader;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{A297F91D-2BE1-4666-8CE9-D6580B0F9B10}");
    }
}