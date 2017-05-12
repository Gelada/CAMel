using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types;

namespace CAMel
{
    enum SurfaceType
	{
	   Brep, Mesh   
	}
    public class C_Surfacing : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_Surfacing()
            : base("Surfacing Operation", "Surface",
                "Create a Machine Operations to create a surface.",
                "CAMel", " Operations")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Surface", "S","The surface, brep or mesh to carve", GH_ParamAccess.item);
            pManager.AddGenericParameter("Rough Path", "R", "Information to create roughing path", GH_ParamAccess.item);
            pManager.AddGenericParameter("Material/Tool", "MTR", "The material to cut and the tool to do it for roughing", GH_ParamAccess.item);
            pManager.AddGenericParameter("Finish Path", "F", "Information to create finishing path", GH_ParamAccess.item);
            pManager.AddGenericParameter("Material/Tool", "MTF", "The material to cut and the tool to do it for finishing", GH_ParamAccess.item);
            pManager.AddGenericParameter("MaterialForm", "MF", "The shape of the material to cut", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Rough", "R", "Roughing Operation", GH_ParamAccess.item);
            pManager.AddGenericParameter("Finish", "F", "Finishing Operation", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            SurfaceType ST;
            Mesh M = new Mesh();
            Brep B = new Brep();
            SurfacePath R = new SurfacePath();
            SurfacePath F = new SurfacePath();
            MaterialTool MTR = new MaterialTool();
            MaterialTool MTF = new MaterialTool();
            MaterialForm MF = new MaterialForm();


            if (DA.GetData(0, ref M))
            {
                ST = SurfaceType.Mesh;
            } else if (DA.GetData(0, ref B))
            {
                ST = SurfaceType.Brep;
            } else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter D failed to collect data");
                return;
            }
            if (!DA.GetData(1, ref R)) return;
            if (!DA.GetData(2, ref MTR)) return;
            if (!DA.GetData(3, ref F)) return;
            if (!DA.GetData(4, ref MTF)) return;
            if (!DA.GetData(5, ref MF)) return;

            MachineOperation Rough = new MachineOperation();
            MachineOperation Finish = new MachineOperation();

            ToolPathAdditions AddRough = new ToolPathAdditions();
            // Additions for Roughing toolpath
            AddRough.insert = true;
            AddRough.retract = true;
            AddRough.stepDown = true;
            AddRough.sdDropStart = true;
            AddRough.sdDropMiddle = 8*MF.safeDistance;
            AddRough.sdDropEnd = true;
            AddRough.threeAxisHeightOffset = false;

            ToolPathAdditions AddFinish = new ToolPathAdditions();
            // Additions for Finishing toolpath
            AddFinish.insert = true;
            AddFinish.retract = true;
            AddFinish.stepDown = false;
            AddFinish.sdDropStart = false;
            AddFinish.sdDropMiddle = 0.0;
            AddFinish.sdDropEnd = false;
            AddFinish.threeAxisHeightOffset = false;

                    switch (ST)
                    {
                        case SurfaceType.Brep:
                            Rough=R.GenerateOperation(B, MTF.finishDepth,MTR,MF,AddRough);
                            Finish=F.GenerateOperation(B,0.0,MTF,MF,AddFinish);
                            break;
                        case SurfaceType.Mesh:
                            Rough=R.GenerateOperation(M, MTF.finishDepth,MTR,MF,AddRough);
                            Finish=F.GenerateOperation(M,0.0,MTF,MF,AddFinish);
                            break;
                    }

            DA.SetData(0, Rough);
            DA.SetData(1, Finish);
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
                return Properties.Resources.pathsurfacing;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{993EE7E5-6D31-4DFD-B4C4-813098BCB628}"); }
        }
    }
}