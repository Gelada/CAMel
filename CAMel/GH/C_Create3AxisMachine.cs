using System;
using System.Collections.Generic;
using CAMel.Types;
using CAMel.Types.Machine;
using Grasshopper.Kernel;
using JetBrains.Annotations;

namespace CAMel.GH
{
    [UsedImplicitly]
    public class C_Create3AxisMachine : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_Create3AxisMachine()
            : base("Create 3 Axis Machine", "3Axis",
                "Create 3 Axis Machine",
                "CAMel", " Hardware")
        {
        }

        // put this item in the second batch (Machines)
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddTextParameter("Name", "N", "Name", GH_ParamAccess.item, string.Empty);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material Tools", "MTs", "Material Tool pairs used by the machine", GH_ParamAccess.list);
            // ReSharper disable once PossibleNullReferenceException
            pManager[1].WireDisplay = GH_ParamWireDisplay.faint;
            pManager[1].Optional = true;
            pManager.AddTextParameter("Header", "H", "Code Header", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Footer", "F", "Code Footer", GH_ParamAccess.item, string.Empty); List<string> ccDefault = new List<string> { GCode.DefaultCommentStart, GCode.DefaultCommentEnd, GCode.DefaultSectionBreak };
            pManager.AddTextParameter("Comment", "C", "String for start and end of comments, as well as section breaks.", GH_ParamAccess.list, ccDefault);
            // ReSharper disable once PossibleNullReferenceException
            pManager[4].Optional = true;
            List<string> irDefault = new List<string> { GCode.DefaultSpeedChangeCommand };
            pManager.AddTextParameter("Speed/ToolChange", "ST", "Command to change speed and change tool", GH_ParamAccess.list, irDefault);
            // ReSharper disable once PossibleNullReferenceException
            pManager[5].Optional = true;
            List<string> fileDefault = new List<string> { GCode.DefaultFileStart, GCode.DefaultFileEnd };
            pManager.AddTextParameter("File Start and End", "SE", "Strings for start and end of file.", GH_ParamAccess.list, fileDefault);
            // ReSharper disable once PossibleNullReferenceException
            pManager[6].Optional = true;
            pManager.AddNumberParameter("Path Jump", "PJ", "Maximum allowed distance between paths in material", GH_ParamAccess.item, 0);
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_MachinePar(), "Machine", "M", "Details for a CNC machine", GH_ParamAccess.item);
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }

            string name = string.Empty;
            string head = string.Empty;
            string foot = string.Empty;
            double pj = 0;

            if (!da.GetData(0, ref name)) { return; }
            if (!da.GetData(2, ref head)) { return; }
            if (!da.GetData(3, ref foot)) { return; }
            if (!da.GetData(7, ref pj)) { return; }

            List<string> cc = new List<string>();
            da.GetDataList(4, cc);

            string commentStart = cc.Count > 0 ? cc[0] ?? string.Empty : GCode.DefaultCommentStart;
            string commentEnd = cc.Count > 1 ? cc[1] ?? string.Empty : GCode.DefaultCommentEnd;
            string sectionBreak = cc.Count > 2 ? cc[2] ?? string.Empty : GCode.DefaultSectionBreak;

            List<string> sir = new List<string>();
            da.GetDataList(5, sir);

            string speed = sir.Count > 0 ? sir[0] ?? string.Empty : GCode.DefaultSpeedChangeCommand;
            string tool = sir.Count > 1 ? sir[1] ?? string.Empty : GCode.DefaultToolChangeCommand;

            List<string> se = new List<string>();
            da.GetDataList(6, se);

            string fileStart = sir.Count > 0 ? se[0] ?? string.Empty : GCode.DefaultFileStart;
            string fileEnd = sir.Count > 1 ? se[1] ?? string.Empty : GCode.DefaultFileEnd;

            List<MaterialTool> mTs = new List<MaterialTool>();
            da.GetDataList(1, mTs);

            IGCodeMachine m = new ThreeAxis(name, mTs, pj, head, foot, speed, tool, commentStart, commentEnd, sectionBreak, fileStart, fileEnd);

            da.SetData(0, new GH_Machine(m));

        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.create3axis;


        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{C01AEAE3-73F0-4DAB-8080-420E1FAC01D3}");
    }
}