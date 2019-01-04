using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types;

namespace CAMel
{
    public class C_CreateMaterialTool : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_CreateMaterialTool()
            : base("Create Material Tool", "MaterialTool",
                "Give details of the material to cut and the tool cutting it",
                "CAMel", " Utilities")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Material Name", "MN", "Name of the material", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Tool Name", "TN", "Name of the tool", GH_ParamAccess.item, string.Empty);
            pManager.AddIntegerParameter("Tool Number", "T", "Number of the tool", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("Speed", "S", "Speed of Spindle", GH_ParamAccess.item);
            pManager.AddNumberParameter("Cut Feed", "CF", "Feed rate when cutting", GH_ParamAccess.item);
            pManager.AddNumberParameter("Plunge Feed", "PF", "Feed rate when plunging into material", GH_ParamAccess.item);
            pManager.AddNumberParameter("Cut Depth", "CD", "Maximum depth of material to cut", GH_ParamAccess.item);
            pManager.AddNumberParameter("Finish Depth", "FD", "Maximum depth of material in final cut", GH_ParamAccess.item);
            pManager.AddNumberParameter("Tolerance", "To", "Tolerance when converting curves to toolpaths", GH_ParamAccess.item);
            pManager.AddNumberParameter("minStep", "mS", "Minimum distance between machine positions", GH_ParamAccess.item);
            pManager.AddNumberParameter("Tool Width", "TW", "Width of tool", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Tool Length", "TL", "Length of tool from last pivot (not needed for 3 Axis)", GH_ParamAccess.item, 0);
            pManager.AddTextParameter("Tool Shape", "TS", "End shape of tool", GH_ParamAccess.item, "Ball");
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("MaterialTool", "MT", "Details of material and the tool cutting it", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string matName = string.Empty;
            string toolName = string.Empty;

            int T = 1;
            double S = 0, CF = 0, PF = 0, CD = 0, FD = 0, To = 0, mS = 0, TW = 0, TL = 0;

            string toolShape = string.Empty;
            EndShape ES;

            if (!DA.GetData(0, ref matName)) { return; }
            if (!DA.GetData(1, ref toolName)) { return; }
            if (!DA.GetData(2, ref T)) { return; }
            if (!DA.GetData(3, ref S)) { return; }
            if (!DA.GetData(4, ref CF)) { return; }
            if (!DA.GetData(5, ref PF)) { return; }
            if (!DA.GetData(6, ref CD)) { return; }
            if (!DA.GetData(7, ref FD)) { return; }
            if (!DA.GetData(8, ref To)) { return; }
            if (!DA.GetData(9, ref mS)) { return; }
            if (!DA.GetData(10, ref TW)) { return; }
            if (!DA.GetData(11, ref TL)) { return; }
            if (!DA.GetData(12, ref toolShape))
            {
                return;
            }
            else
            {
                switch (toolShape)
                {
                    case "Ball":
                        ES = EndShape.Ball;
                        break;
                    case "Square":
                        ES = EndShape.Square;
                        break;
                    case "V":
                        ES = EndShape.V;
                        break;
                    case "Other":
                        ES = EndShape.Other;
                        break;
                    default:
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "End Shape not recognised. Options are \"Ball\", \"Square\", \"V\" use \"Other\" to avoid warning.");
                        ES = EndShape.Other;
                        break;
                }
            }

            MaterialTool MT = new MaterialTool(matName, toolName, T, S, CF, PF, CD, FD, TW, TL, ES, To, mS);

            DA.SetData(0, new GH_MaterialTool(MT));
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
                return Properties.Resources.creatematerialtool;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{C67DDF9A-A893-4393-B9C9-FC6CB5F304DA}"); }
        }
    }
}