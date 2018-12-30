using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types;
using CAMel.Types.MaterialForm;

namespace CAMel
{
    public class C_Index3Axis : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_Index3Axis()
            : base("Index 3 Axis", "Index",
                "Create a machine operation from a collection of paths, with 3 Axis operation with the tool in a single orientation.",
                "CAMel", " Operations")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "The curves for the tip of the tool to follow", GH_ParamAccess.list);
            pManager.AddVectorParameter("Direction", "D", "Direction of the tool.", GH_ParamAccess.item, new Vector3d(0,0,1));
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
            List<Curve> C = new List<Curve>();
            Vector3d Dir = new Vector3d();
            MaterialTool MT = null;
            IMaterialForm MF = null;


            if (!DA.GetDataList(0, C)) return;
            if (!DA.GetData(1, ref Dir)) return;
            if (!DA.GetData(2, ref MT)) return;
            if (!DA.GetData(3, ref MF)) return;

            MachineOperation Op = new MachineOperation();

            Op.name = "Index 3-Axis Cutting with " + C.Count.ToString() + " path";
            if (C.Count > 1) Op.name = Op.name + "s";

            ToolPath TP;
            int i = 1;

            int InvalidCurves = 0; // Keep track of any invalid curves.

            foreach(Curve c in C)
            {
                // Create and add name, material/tool and material form
                TP = new ToolPath("Index 3-Axis Path", MT, MF);
                if(C.Count > 1) { TP.name = TP.name + " " + i.ToString(); }

                // Additions for toolpath
                TP.Additions.insert = true;
                TP.Additions.retract = true;
                TP.Additions.stepDown = true;
                TP.Additions.sdDropStart = true;
                TP.Additions.sdDropMiddle = 8*MF.safeDistance;
                TP.Additions.sdDropEnd = true;
                TP.Additions.threeAxisHeightOffset = true;

                // Turn Curve into path

                if (TP.convertCurve(c, Dir))
                    Op.Add(TP);
                else
                    InvalidCurves++;
                i++;
            }

            if (InvalidCurves > 1) 
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "A total of "+InvalidCurves.ToString()+" invalid curves (probably nulls) were ignored.");
            else if (InvalidCurves > 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "An invalid curve (probably a null) was ignored.");


            if (Op.Count > 0) { DA.SetData(0, Op); }
            else { DA.SetData(0, null); }
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
                return Properties.Resources.index3axis;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{81951E44-9F56-4A4C-BCD5-5B1976B335E2}"); }
        }
    }
}