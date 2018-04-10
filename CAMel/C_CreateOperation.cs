﻿using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using CAMel.Types;

namespace CAMel
{
    public class C_CreateOperation : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateToolPath class.
        /// </summary>
        public C_CreateOperation()
            : base("Create Operation ", "Operation",
                "Create an operation from a list of toolpaths",
                "CAMel", " ToolPaths")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            List<double> TPAdef = new List<double>();
            for (int i = 0; i < 7; i++) TPAdef.Add(0);

            pManager.AddTextParameter("Name", "N", "Name of operation", GH_ParamAccess.item,"");
            pManager.AddGenericParameter("Toolpaths", "TP", "The list of toolpaths to use for the operation.", GH_ParamAccess.list); 

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Machine Operation", "MO", "A machine operation.", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<ToolPath> TPs = new List<ToolPath>();
            string name = "";

            if (!DA.GetData(0, ref name)) return;
            if (!DA.GetDataList(1, TPs)) return;

            MachineOperation MO = new MachineOperation(name, TPs);

            DA.SetData(0, MO);
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
                return Properties.Resources.createoperations;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{D72DFE0B-7D61-4130-B564-6EEE2A8BDA99}"); }
        }
    }
}