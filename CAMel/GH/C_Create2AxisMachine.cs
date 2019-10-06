using System;
using System.Collections.Generic;
using CAMel.Types;
using CAMel.Types.Machine;
using Grasshopper.Kernel;
using JetBrains.Annotations;

namespace CAMel.GH
{
    [UsedImplicitly]
    public class C_Create2AxisMachine : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_Create2AxisMachine()
            : base("Create 2 Axis Machine", "2Axis",
                "Create 2 Axis Machine",
                "CAMel", " Hardware") { }

        // put this item in the second batch (Machines)
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Name", GH_ParamAccess.item, string.Empty);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material Tools", "MTs", "Material Tool pairs used by the machine", GH_ParamAccess.list);
            // ReSharper disable once PossibleNullReferenceException
            pManager[1].WireDisplay = GH_ParamWireDisplay.faint;
            pManager[1].Optional = true;
            pManager.AddTextParameter("Header", "H", "Code Header", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Footer", "F", "Code Footer", GH_ParamAccess.item, string.Empty);
            List<string> ccDefault = new List<string> {GCode.DefaultCommentStart, GCode.DefaultCommentEnd, GCode.DefaultSectionBreak};
            pManager.AddTextParameter("Comment", "C", "String for start and end of comments, as well as section breaks.", GH_ParamAccess.list, ccDefault);
            // ReSharper disable once PossibleNullReferenceException
            pManager[4].Optional = true;
            List<string> irDefault = new List<string> {GCode.DefaultSpeedChangeCommand, GCode.DefaultActivateCommand, GCode.DefaultDeActivateCommand};
            pManager.AddTextParameter("Speed/Insert/Retract/ToolChange", "SIRT", "Commands to change speed, insert and retract tool, and change tool", GH_ParamAccess.list, irDefault);
            // ReSharper disable once PossibleNullReferenceException
            pManager[5].Optional = true;
            List<string> fileDefault = new List<string> {GCode.DefaultFileStart, GCode.DefaultFileEnd};
            pManager.AddTextParameter("File Start and End", "SE", "Strings for start and end of file.", GH_ParamAccess.list, fileDefault);
            // ReSharper disable once PossibleNullReferenceException
            pManager[6].Optional = true;
            pManager.AddTextParameter("Extension", "E", "Filename extension", GH_ParamAccess.item, GCode.DefaultExtension);
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
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

            string uName = string.Empty;
            string ext = string.Empty;
            string head = string.Empty;
            string foot = string.Empty;

            if (!da.GetData("Name", ref uName)) { return; }
            if (!da.GetData("Extension", ref ext)) { return; }
            if (!da.GetData("Header", ref head)) { return; }
            if (!da.GetData("Footer", ref foot)) { return; }

            List<string> cc = new List<string>();
            da.GetDataList("Comment", cc);

            string uCommentStart = cc.Count > 0 ? cc[0] ?? string.Empty : GCode.DefaultCommentStart;
            string uCommentEnd = cc.Count > 1 ? cc[1] ?? string.Empty : GCode.DefaultCommentEnd;
            string uSectionBreak = cc.Count > 2 ? cc[2] ?? string.Empty : GCode.DefaultSectionBreak;

            List<string> sir = new List<string>();
            da.GetDataList(5, sir);

            string speed = sir.Count > 0 ? sir[0] ?? string.Empty : GCode.DefaultSpeedChangeCommand;
            string uInsert = sir.Count > 1 ? sir[1] ?? string.Empty : GCode.DefaultActivateCommand;
            string uRetract = sir.Count > 2 ? sir[2] ?? string.Empty : GCode.DefaultDeActivateCommand;
            string tool = sir.Count > 3 ? sir[3] ?? string.Empty : GCode.DefaultToolChangeCommand;

            List<string> se = new List<string>();
            da.GetDataList("File Start and End", se);

            string uFileStart = se.Count > 0 ? se[0] ?? string.Empty : GCode.DefaultFileStart;
            string uFileEnd = se.Count > 1 ? se[1] ?? string.Empty : GCode.DefaultFileEnd;

            List<MaterialTool> uMTs = new List<MaterialTool>();
            da.GetDataList("Material Tools", uMTs);

            TwoAxisFactory twoAxis = new TwoAxisFactory
            {
                name = uName,
                extension = ext,
                mTs = uMTs,
                header = head,
                footer = foot,
                speedChangeCommand = speed,
                toolActivate = uInsert,
                toolDeActivate = uRetract,
                toolChangeCommand = tool,
                commentStart = uCommentStart,
                commentEnd = uCommentEnd,
                sectionBreak = uSectionBreak,
                fileStart = uFileStart,
                fileEnd = uFileEnd
            };

            IGCodeMachine m = new TwoAxis(twoAxis);
            da.SetData(0, m);
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.create2axis;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{BB5520F1-93DC-42AA-A1FD-FF892EFF3D8B}");
    }
}