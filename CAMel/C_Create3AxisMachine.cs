using System;
using System.Collections.Generic;

using Grasshopper.Kernel;

using CAMel.Types;
using CAMel.Types.Machine;

namespace CAMel
{
    public class C_Create3AxisMachine : GH_Component
    {
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

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Name", GH_ParamAccess.item, string.Empty);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material Tools", "MTs", "Material Tool pairs used by the machine", GH_ParamAccess.list);
            pManager[1].WireDisplay = GH_ParamWireDisplay.faint;
            pManager[1].Optional = true;
            pManager.AddTextParameter("Header", "H", "Code Header", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Footer", "F", "Code Footer", GH_ParamAccess.item, string.Empty); List<string> ccDefault = new List<string> { GCode.defaultCommentStart, GCode.defaultCommentEnd, GCode.defaultSectionBreak };
            pManager.AddTextParameter("Comment", "C", "String for start and end of comments, as well as section breaks.", GH_ParamAccess.list, ccDefault);
            pManager[4].Optional = true;
            List<string> irDefault = new List<string> { GCode.defaultSpeedChangeCommand };
            pManager.AddTextParameter("Speed/ToolChange", "ST", "Command to change speed and change tool", GH_ParamAccess.list);
            pManager[5].Optional = true;
            List<string> fileDefault = new List<string> { GCode.defaultFileStart, GCode.defaultFileEnd };
            pManager.AddTextParameter("File Start and End", "SE", "Strings for start and end of file.", GH_ParamAccess.list, fileDefault);
            pManager[6].Optional = true;
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
            if (!DA.GetData(2, ref head)) { return; }
            if (!DA.GetData(3, ref foot)) { return; }
            if (!DA.GetData(7, ref PJ)) { return; }

            List<string> CC = new List<string>();
            DA.GetDataList(4, CC);

            string commentStart = (CC.Count > 0) ? CC[0] : GCode.defaultCommentStart;
            string commentEnd = (CC.Count > 1) ? CC[1] : GCode.defaultCommentEnd;
            string sectionBreak = (CC.Count > 2) ? CC[2] : GCode.defaultSectionBreak;

            List<string> SIR = new List<string>();
            DA.GetDataList(5, SIR);

            string speed = (SIR.Count > 0) ? SIR[0] : GCode.defaultSpeedChangeCommand;
            string tool = (SIR.Count > 1) ? SIR[1] : GCode.defaultToolChangeCommand;

            List<string> SE = new List<string>();
            DA.GetDataList(6, SE);

            string fileStart = (SIR.Count > 0) ? SE[0] : GCode.defaultFileStart;
            string fileEnd = (SIR.Count > 1) ? SE[1] : GCode.defaultFileEnd;

            List<MaterialTool> MTs = new List<MaterialTool>();
            DA.GetDataList(1, MTs);

            IGCodeMachine M = new ThreeAxis(name, MTs, PJ, head, foot, speed, tool, commentStart, commentEnd, sectionBreak, fileStart, fileEnd);

            DA.SetData(0, new GH_Machine(M));
            
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
                return Properties.Resources.create3axis;
            }
        }

       

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{C01AEAE3-73F0-4DAB-8080-420E1FAC01D3}"); }
        }
    }
}