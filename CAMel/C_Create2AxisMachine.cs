using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types;

namespace CAMel
{
    public class C_Create3AxisMachine : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_Create3AxisMachine()
            : base("Create 3 Axis Machine", "3 Axis",
                "Create 3 Axis Machine",
                "CAMel", " Utilities")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Name", GH_ParamAccess.item,string.Empty);
            pManager.AddTextParameter("Header", "H", "Code Header", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Footer", "F", "Code Footer", GH_ParamAccess.item, string.Empty);
            pManager.AddNumberParameter("Path Jump", "PJ", "Maximum allowed distance between paths in material", GH_ParamAccess.item, 0);
            pManager.AddTextParameter("Comment", "C", "Characters for start and end of comments", GH_ParamAccess.list,string.Empty);
            pManager.AddTextParameter("Features", "O", "Other features of the machine\n"+
                "{2d, lead, Insert Code, Retract Code\n}" +
                "2d is a Boolean (0 or 1), that specifies a 2d machine like a Plasma cutter. \n"+
                "lead gives the distance for the lead in or out. \n"+
                "Insert and Retract Codes are added before an insert and after a rectract move.", 
                GH_ParamAccess.list, string.Empty);
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
            List<string> CC = new List<string>();
            List<string> Fe = new List<string>();

            double PJ = 0;

            if (!DA.GetData(0, ref name)) return;
            if (!DA.GetData(1, ref head)) return;
            if (!DA.GetData(2, ref foot)) return;
            if (!DA.GetData(3, ref PJ)) return;

            Machine M = new Machine(name, MachineTypes.ThreeAxis, head, foot,string.Empty,string.Empty);

            if (DA.GetDataList(4, CC) && CC[0] != string.Empty)
            {
                M.CommentChar = CC[0];
                if (CC.Count > 1) { M.endCommentChar = CC[1]; }
                if (CC.Count > 2) { M.SectionBreak = CC[2]; }
            }
            if (DA.GetDataList(5, Fe) && (Fe.Count > 1 || Fe[0] != string.Empty))
            {
                int dim = 0;
                if(int.TryParse(Fe[0], out dim)){ if(dim!=0) { M.type = MachineTypes.TwoAxisXY; } }
                if (Fe.Count > 1) { M.leads = Convert.ToDouble(Fe[1]); }
                if (Fe.Count > 2) { M.InsertCode = Fe[2]; }
                if (Fe.Count > 3) { M.RetractCode = Fe[3]; }
            }

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