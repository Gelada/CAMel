using System;

using Grasshopper.Kernel;

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
                "CAMel", " Hardware")
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
            pManager.AddNumberParameter("Speed", "S", "Speed of Spindle", GH_ParamAccess.item, -1);
            pManager.AddNumberParameter("Cut Feed", "CF", "Feed rate when cutting", GH_ParamAccess.item);
            pManager.AddNumberParameter("Plunge Feed", "PF", "Feed rate when plunging into material", GH_ParamAccess.item);
            pManager.AddNumberParameter("Cut Depth", "CD", "Maximum depth of material to cut (negative values will allow cuts as deep as needed).", GH_ParamAccess.item, -1);
            pManager.AddNumberParameter("Finish Depth", "FD", "Maximum depth of material in final cut (negative values will allow cuts as deep as needed).", GH_ParamAccess.item, -1);
            pManager.AddNumberParameter("Tolerance", "To", "Tolerance when converting curves to toolpaths", GH_ParamAccess.item, 0.005);
            pManager.AddNumberParameter("minStep", "mS", "Minimum distance between machine positions", GH_ParamAccess.item,.05);
            pManager.AddNumberParameter("Tool Width", "TW", "Width of tool", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Insert Width", "IW", "Width needed for tool insertion (for example for plasma);", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Tool Length", "TL", "Length of tool from last pivot (not needed for 3 Axis).", GH_ParamAccess.item, 0);
            pManager.AddTextParameter("Tool Shape", "TS", "End shape of tool (Ball, Square, V, Other).", GH_ParamAccess.item, "Other");
            pManager.AddNumberParameter("Side Load", "SL", "Fraction of the tool to engage with the material when surfacing.", GH_ParamAccess.item, 1);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_MaterialToolPar(),"MaterialTool", "MT", "Details of material and the tool cutting it", GH_ParamAccess.item);
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
            double S = 0, CF = 0, PF = 0, CD = 0, FD = 0, To = 0, mS = 0, TW = 0, IW=0, TL = 0, SL =1;

            string toolShape = string.Empty;
            EndShape ES;

            if (!DA.GetData("Material Name", ref matName)) { return; }
            if (!DA.GetData("Tool Name", ref toolName)) { return; }
            if (!DA.GetData("Tool Number", ref T)) { return; }
            if (!DA.GetData("Speed", ref S)) { return; }
            if (!DA.GetData("Cut Feed", ref CF)) { return; }
            if (!DA.GetData("Plunge Feed", ref PF)) { return; }
            if (!DA.GetData("Cut Depth", ref CD)) { return; }
            if (!DA.GetData("Finish Depth", ref FD)) { return; }
            if (!DA.GetData("Tolerance", ref To)) { return; }
            if (!DA.GetData("minStep", ref mS)) { return; }
            if (!DA.GetData("Tool Width", ref TW)) { return; }
            if (!DA.GetData("Insert Width", ref IW)) { return; }
            if (!DA.GetData("Tool Length", ref TL)) { return; }
            if (!DA.GetData("Tool Shape", ref toolShape)) { return; }
            if (!DA.GetData("Side Load", ref SL)) { return; }

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

            MaterialTool MT = new MaterialTool(matName, toolName, T, S, CF, PF, CD, FD, TW, IW, TL, ES, To, mS, SL);

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