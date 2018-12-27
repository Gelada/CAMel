using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types.Machine;

namespace CAMel
{
    public class C_PocketNC : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_PocketNC()
            : base("PocketNC Machine", "PocketNC",
                "PocketNC Axis Machine",
                "CAMel", " Utilities")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Version", "V", "Machine version, 1 or 2", GH_ParamAccess.item, 2);
            pManager.AddTextParameter("Header", "H", "Code Header", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Footer", "F", "Code Footer", GH_ParamAccess.item, "");
            pManager.AddNumberParameter("Path Jump", "PJ", "Maximum allowed distance between paths in material", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Machine", "M", "Details for a PocketNC 5-axis machine", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string head = "";
            string foot = "";
            double PJ = 0;
            int V = 0;

            if (!DA.GetData(0, ref V)) { return; }
            if(V!=1 && V!=2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Only two version of the PocketNC known.");
                return;
            }
            if (V == 1) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "All testing done on a V2 machine, please be careful."); }
            if (!DA.GetData(1, ref head)) { return; }
            if (!DA.GetData(2, ref foot)) { return; }
            if (!DA.GetData(3, ref PJ)) { return; }

            double Amin = 0, Amax = Math.PI/2.0;
            if(V==1)
            {
                Amin = -5 * Math.PI / 180.0;
                Amax = 95 * Math.PI / 180.0;
            }
            if(V==2)
            {
                Amin = -5 * Math.PI / 180.0;
                Amax = 95 * Math.PI / 180.0;
            }

            PocketNC M = new PocketNC("PocketNC V" + V.ToString(), head, foot, Amin, Amax);
            
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
                return Properties.Resources.create5axis;
            }
        }

       

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{A6E20644-AA34-4400-B87E-EEBA8BDF3720}"); }
        }
    }
}