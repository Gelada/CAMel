using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Display;
using CAMel.Types;

namespace CAMel
{
    public class C_Timer : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the C_Timer class.
        /// </summary>
        public C_Timer()
          : base("C_Timer", "Timer",
              "Times",
              "CAMel", "Utilities")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Machine Instruction", "MI", "Machine Instruction to be estimated.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Time Estimation", "T", "An estimate of the cut time of the supplied machine operation.", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Double timeEstimate = 0;
            MachineInstruction MI = new MachineInstruction();

            if (!DA.GetData(0, ref MI)) return;
            timeEstimate = 0;
            //for each machine operation in the Machine Instruction
            foreach (MachineOperation MO in MI.MOs)
            {
                //for each toolpath in the Machine operation
                foreach (ToolPath TP in MO.TPs)
                {
                    for(int i = 0; i < TP.Pts.Count-1; i++)
                    {
                        Vector3d nextDirection = new Vector3d((TP.Pts[i + 1].Pt.X - TP.Pts[i].Pt.X), (TP.Pts[i + 1].Pt.Y - TP.Pts[i].Pt.Y), (TP.Pts[i + 1].Pt.Z - TP.Pts[i].Pt.Z));
                        //if plunging do plunge feed
                        if (nextDirection.X == 0 && nextDirection.Y == 0)
                        {
                            timeEstimate += nextDirection.Length / TP.MatTool.feedPlunge;
                        }

                        //else cutting so do cut feed
                        else
                        {
                            timeEstimate += nextDirection.Length / TP.MatTool.feedCut;
                        }
                    }
                }
            }

            if (timeEstimate < 0) DA.SetData(0, "Time not computed");
            else if (timeEstimate >= 0) DA.SetData(0, timeEstimate.ToString());
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
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{030b2705-7aa0-47e8-840e-bdd02056e8fc}"); }
        }
    }
}