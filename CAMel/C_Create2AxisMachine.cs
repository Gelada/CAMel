using System;
using System.Collections.Generic;

using Grasshopper.Kernel;

using CAMel.Types;
using CAMel.Types.Machine;

namespace CAMel
{
    public class C_Create2AxisMachine : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_Create2AxisMachine()
            : base("Create 2 Axis Machine", "2Axis",
                "Create 2 Axis Machine",
                "CAMel", " Hardware")
        {
        }

        // put this item in the second batch (Machines)
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Name", GH_ParamAccess.item,string.Empty);
            var GTPAP = new GH_ToolPathAdditionsPar();
            GTPAP.SetPersistentData(new GH_ToolPathAdditions(TwoAxis._defaultImplents));
            pManager.AddParameter(GTPAP, "Available ToolPath Additions", "TPA", "ToolPath Additions to be implements by the machine", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material Tools", "MTs", "Material Tool pairs used by the machine", GH_ParamAccess.list);
            pManager[2].WireDisplay = GH_ParamWireDisplay.faint;
            pManager[2].Optional = true;
            pManager.AddTextParameter("Header", "H", "Code Header", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Footer", "F", "Code Footer", GH_ParamAccess.item, string.Empty); List<string> ccDefault = new List<string> { GCode.defaultCommentStart, GCode.defaultCommentEnd, GCode.defaultSectionBreak };
            pManager.AddTextParameter("Comment", "C", "String for start and end of comments, as well as section breaks.", GH_ParamAccess.list,ccDefault);
            pManager[5].Optional = true;
            List<string> irDefault = new List<string> { GCode.defaultSpeedChangeCommand, GCode.defaultInsertCommand, GCode.defaultRetractCommand };
            pManager.AddTextParameter("Speed/Insert/Retract/ToolChange", "SIRT", "Commands to change speed, insert and retract tool, and change tool", GH_ParamAccess.list);
            pManager[6].Optional = true;
            List<string> fileDefault = new List<string> { GCode.defaultFileStart, GCode.defaultFileEnd };
            pManager.AddTextParameter("File Start and End", "SE", "Strings for start and end of file.", GH_ParamAccess.list, fileDefault);
            pManager[7].Optional = true;
            pManager.AddNumberParameter("Path Jump", "PJ", "Maximum allowed distance between paths in material", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_MachinePar(), "Machine", "M", "Details for a CNC machine", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            string name = string.Empty;
            string head = string.Empty;
            string foot = string.Empty;
            double PJ = 0;

            if (!DA.GetData(0, ref name)) { return; }
            if (!DA.GetData(8, ref PJ)) { return; }
            if (!DA.GetData(3, ref head)) { return; }
            if (!DA.GetData(4, ref foot)) { return; }


            List<string> CC = new List<string>();
            DA.GetDataList(5, CC);

            string commentStart = (CC.Count > 0) ? CC[0] : GCode.defaultCommentStart;
            string commentEnd = (CC.Count > 1) ? CC[1] : GCode.defaultCommentEnd;
            string sectionBreak = (CC.Count > 2) ? CC[2] : GCode.defaultSectionBreak;

            List<string> SIR = new List<string>();
            DA.GetDataList(6, SIR);

            string speed = (SIR.Count > 0) ? SIR[0] : GCode.defaultSpeedChangeCommand;
            string insert = (SIR.Count > 1) ? SIR[1] : GCode.defaultInsertCommand;
            string retract = (SIR.Count > 2) ? SIR[2] : GCode.defaultRetractCommand;
            string tool = (SIR.Count > 3) ? SIR[3] : GCode.defaultToolChangeCommand;

            List<string> SE = new List<string>();
            DA.GetDataList(7, SE);

            string fileStart = (SIR.Count > 0) ? SE[0] : GCode.defaultFileStart;
            string fileEnd = (SIR.Count > 1) ? SE[1] : GCode.defaultFileEnd;

            ToolPathAdditions TPA = null;
            if(!DA.GetData(1,ref TPA)) { return; }

            List<MaterialTool> MTs = new List<MaterialTool>();
            DA.GetDataList(2, MTs);

            IGCodeMachine M = new TwoAxis(name, TPA, MTs, PJ, head, foot, speed, insert, retract, tool,commentStart,commentEnd,sectionBreak, fileStart, fileEnd);

            DA.SetData(0, M);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Properties.Resources.create2axis;
            }
        }

       

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{BB5520F1-93DC-42AA-A1FD-FF892EFF3D8B}"); }
        }
    }
}