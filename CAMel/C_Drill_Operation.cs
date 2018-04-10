using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types;
using CAMel.Types.MaterialForm;

namespace CAMel
{
    public class C_DrillOperation : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_DrillOperation()
            : base("Drill Operation", "Drill",
                "Create a Machine Operations drilling at a certain point.",
                "CAMel", " Operations")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCircleParameter("Drill Points", "D", "A list of circles. The centre of each circle gives the position to drill, the orientation of the circle gives the tool direction and the radius gives the depth.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Peck Depth", "P", "Depth of Peck", GH_ParamAccess.item, 0);
            pManager.AddGenericParameter("Material/Tool", "MT", "The material to cut and the tool to do it", GH_ParamAccess.item);
            pManager.AddGenericParameter("MaterialForm", "MF","The shape of the material to cut", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Operation", "O", "Machine Operation", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Circle D = new Circle();
            double peck = 0;
            MaterialTool MT = null;
            IMaterialForm MF = null;


            if (!DA.GetData(0, ref D))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter D failed to collect data");
                return;
            }
            if (!DA.GetData(1, ref peck)) return;
            if (!DA.GetData(2, ref MT)) return;
            if (!DA.GetData(3, ref MF)) return;

            MachineOperation Op = new MachineOperation();

            Op.name = "Drilling depth " + D.Radius.ToString("0.000") + " at (" + D.Center.X.ToString("0.000") + "," + D.Center.Y.ToString("0.000") + "," + D.Center.Z.ToString("0.000") + ").";

            ToolPath TP = new ToolPath("",MT,MF);

            // Additions for toolpath
            TP.Additions.insert = true;
            TP.Additions.retract = true;
            TP.Additions.stepDown = false; // we will handle this with peck
            TP.Additions.sdDropStart = false;
            TP.Additions.sdDropMiddle = 0;
            TP.Additions.sdDropEnd = false;
            TP.Additions.threeAxisHeightOffset = false;

            if (D.Normal.Length == 0) AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Cannot process a circle who's normal is given as the zero vector. Check for null inputs.");

            TP.Add(new ToolPoint(D.Center, D.Normal,-1,MT.feedPlunge));

            // calculate the number of pecks we need to do

            int steps;
            if (peck > 0) steps = (int)Math.Ceiling(D.Radius / peck);
            else steps = 1;

            for (int j = 1; j <= steps; j++)
            {
                TP.Add(new ToolPoint(D.Center - ((double)j / (double)steps) * D.Radius * D.Normal, D.Normal,-1,MT.feedPlunge));
                TP.Add(new ToolPoint(D.Center, D.Normal,-1,MT.feedPlunge));
            }

            Op.Add(TP);

            DA.SetData(0, Op);


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
                return Properties.Resources.drilloperations;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{9AC2C9BE-AFF2-4644-9A4F-E5D2E4A5EA65}"); }
        }
    }
}