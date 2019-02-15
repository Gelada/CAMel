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
        protected override void RegisterInputParams(GH_InputParamManager pManager)
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
            pManager.AddTextParameter("Speed/ToolChange", "ST", "Command to change speed and change tool", GH_ParamAccess.list, irDefault);
            pManager[5].Optional = true;
            List<string> fileDefault = new List<string> { GCode.defaultFileStart, GCode.defaultFileEnd };
            pManager.AddTextParameter("File Start and End", "SE", "Strings for start and end of file.", GH_ParamAccess.list, fileDefault);
            pManager[6].Optional = true;
            pManager.AddNumberParameter("Path Jump", "PJ", "Maximum allowed distance between paths in material", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_MachinePar(), "Machine", "M", "Details for a CNC machine", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {

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

            string commentStart = (cc.Count > 0) ? cc[0] : GCode.defaultCommentStart;
            string commentEnd = (cc.Count > 1) ? cc[1] : GCode.defaultCommentEnd;
            string sectionBreak = (cc.Count > 2) ? cc[2] : GCode.defaultSectionBreak;

            List<string> sir = new List<string>();
            da.GetDataList(5, sir);

            string speed = (sir.Count > 0) ? sir[0] : GCode.defaultSpeedChangeCommand;
            string tool = (sir.Count > 1) ? sir[1] : GCode.defaultToolChangeCommand;

            List<string> se = new List<string>();
            da.GetDataList(6, se);

            string fileStart = (sir.Count > 0) ? se[0] : GCode.defaultFileStart;
            string fileEnd = (sir.Count > 1) ? se[1] : GCode.defaultFileEnd;

            List<MaterialTool> mTs = new List<MaterialTool>();
            da.GetDataList(1, mTs);

            IGCodeMachine m = new ThreeAxis(name, mTs, pj, false, head, foot, speed, tool, commentStart, commentEnd, sectionBreak, fileStart, fileEnd);

            da.SetData(0, new GH_Machine(m));

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