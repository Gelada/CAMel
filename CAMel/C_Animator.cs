using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using CAMel.Types;

namespace CAMel
{
    public class C_Animator : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the C_Animator class.
        /// </summary>
        public C_Animator()
          : base("Animator", "Animator",
              "Animates the paths of G-Code input to this component.",
              "CAMel", "Utilities")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddVectorParameter("Direction", "D", "Direction of the tool.", GH_ParamAccess.item, new Vector3d(0, 0, 1));
            pManager.AddGenericParameter("Tool", "T", "The tool that will be used to cut the material.", GH_ParamAccess.item);
            pManager.AddGenericParameter("MaterialForm", "MF", "The shape of the material to cut.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Tool Path", "TP", "The path the tool will follow.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("ToolShape", "TS", "The mesh that will form the shape of the tool.", GH_ParamAccess.item);

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //containers for inputs
            Vector3d D = new Vector3d();
            MaterialTool MT = new MaterialTool();
            MaterialForm MF = new MaterialForm();
            ToolPath TP = new ToolPath();

            //retrieve inputs, if nothing is retrieved return
            if (!DA.GetData(0, ref D)) return;
            if (!DA.GetData(1, ref MT)) return;
            if (!DA.GetData(2, ref MF)) return;
            if (!DA.GetData(3, ref TP)) return;


            //%%%%%%%% Will require TP to add get methods for matTool and MatForm
            //Retrieve directions, tool, and material form information from the tool path
            //D = TP.GetDirs();
            //MT = TP.MatTool();
            //MF = TP.MatForm();


            //If the tool width is negative it hasn't been set and we should exit and warn the user
            if (MT.toolWidth < 0)
            {
                //output a warning message
                return;
            }

            //plane orthogonal to the tool direction
            Plane toolOPlane = new Plane(new Point3d(0.0, 0.0, 5.0), D);

            //circle to be made into the tool shape
            Circle toolCircle = new Circle(toolOPlane, (MT.toolWidth / 2));

            //cylinder to create a mesh from
            Cylinder toolCylinder = new Cylinder(toolCircle, MT.toolLength);

            //Mesh representation of the tool
            Mesh toolMesh = Mesh.CreateFromCylinder(toolCylinder, 50, 50);

            //Set the output to be the tool mesh
            DA.SetData(0, toolMesh);
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
            get { return new Guid("{15db275c-1d75-439d-a9c0-f602d028334f}"); }
        }
    }
}