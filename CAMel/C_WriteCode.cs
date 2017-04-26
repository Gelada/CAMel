using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types;

namespace CAMel
{
    public class C_WriteCode : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_WriteCode()
            : base("Write CNC Code", "Write",
                "Write CNC Code",
                "CAMel", "CNC Code")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            List<String> Ig = new List<string>();
            Ig.Add("Nothing to Ignore.");
            pManager.AddGenericParameter("Machine Instructions", "MI", "Complete set of machine instructions to convert to Code for the machine", GH_ParamAccess.item);
            pManager.AddTextParameter("Ignore", "Ig", "List of strings giving errors to turn into warnings", GH_ParamAccess.list,Ig);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Code", "Code", "Code for the machine", GH_ParamAccess.item);
            pManager.AddTextParameter("Ranges", "R", "Ranges of movement", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            MachineInstruction MI = new MachineInstruction();
            if (!DA.GetData(0, ref MI))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter MI failed to collect data.");
                return;
            }

            List<String> ignore = new List<string>();

            if (!DA.GetDataList(1, ignore)) return;

            CodeInfo Code = new CodeInfo(MI.Mach);

            MachineInstruction procMI = MI.ProcessAdditions();

            procMI.WriteCode(ref Code);

            // Detect Errors and warnings

            // TODO report errors and warnings in an output parameter

            if (Code.HasErrors(ignore))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, Code.GetErrors(ignore));
            if (Code.HasWarnings(ignore))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, Code.GetWarnings(ignore));

            // Extract Ranges

            Dictionary<String, Interval> Ranges = Code.GetRanges();
            string rOut = ""; 

            foreach(string k in Ranges.Keys)
            {
                rOut = rOut + "\n" + k + ": " + Ranges[k].T0.ToString("0.00") + " to " + Ranges[k].T1.ToString("0.00");
            }

            DA.SetData(0,Code.ToString());
            DA.SetData(1,rOut);
            
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Properties.Resources.cncwriter;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{908e79f0-4698-4642-9158-b90c8d9df83a}"); }
        }
    }
}