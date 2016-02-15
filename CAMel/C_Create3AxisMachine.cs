﻿using System;
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
            pManager.AddTextParameter("Name", "N", "Name", GH_ParamAccess.item,"");
            pManager.AddTextParameter("Header", "H", "Code Header", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Footer", "F", "Code Footer", GH_ParamAccess.item, "");
            pManager.AddNumberParameter("Path Jump", "PJ", "Maximum allowed distance between paths in material", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Machine", "M", "Details for a CNC machine", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<MachineOperation> MO = new List<MachineOperation>();

            string name = "";
            string head = "";
            string foot = "";
            double PJ = 0;

            if (!DA.GetData(0, ref name)) return;
            if (!DA.GetData(1, ref head)) return;
            if (!DA.GetData(2, ref foot)) return;
            if (!DA.GetData(3, ref PJ)) return;

            Machine M = new Machine(name, MachineTypes.ThreeAxis, head, foot);

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