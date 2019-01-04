using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types;
using CAMel.Types.Machine;

namespace CAMel
{
    public class C_ReadCode : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_ReadCode()
            : base("Read CNC Code", "Read",
                "Read CNC Code",
                "CAMel", "CNC Code")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Code", "C", "CNC code file", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MachinePar(),"Machine", "M", "Machine to read code", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_ToolPathPar(), "ToolPath", "TP", "Full toolpath described by the file", GH_ParamAccess.item);
            pManager.AddPointParameter("Points", "Pts", "Position of the machine", GH_ParamAccess.list);
            pManager.AddVectorParameter("Directions", "Dirs", "Direction of the tool", GH_ParamAccess.list);
            pManager.AddVectorParameter("Speeds and Feeds", "SF", "Vectors with speeds (X) and feeds (Y).", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string Code = string.Empty;
            IMachine M = null;
            List<MaterialTool> MTs = new List<MaterialTool>();

            if (!DA.GetData(0, ref Code)) { return; }
            if (!DA.GetData(1, ref M)) { return; }
            if (!DA.GetDataList(2, MTs)) { return; }


            ToolPath TP = M.readCode(MTs, Code);

            List<Point3d> selPt = new List<Point3d>();
            List<Vector3d> selDir = new List<Vector3d>();

            foreach(ToolPoint tp in TP)
            {
                if(true)
                {
                    selPt.Add(tp.pt);
                    selDir.Add(tp.dir);
                }
            }

            DA.SetData(0,new GH_ToolPath(TP));
            DA.SetDataList(1, selPt);
            DA.SetDataList(2, selDir);
            DA.SetDataList(3, TP.getSpeedFeed());
            
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
                return Properties.Resources.cncreader;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{A297F91D-2BE1-4666-8CE9-D6580B0F9B10}"); }
        }
    }
}