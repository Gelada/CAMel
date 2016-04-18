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
    //        pManager.AddGenericParameter("Machine Instruction", "MI", "placeholder", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Step Number", "SN", "The iteration step of that machining animation to view", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Path Division", "PD", "The number of divisions to seperate the path into for the animation.", GH_ParamAccess.item, 1000);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Material Shape base", "MS", "The shape of the material", GH_ParamAccess.list);
            pManager.AddMeshParameter("ToolShape", "TS", "The mesh that will form the shape of the tool.", GH_ParamAccess.list);
            pManager.AddMeshParameter("Render", "E", "Shape post machining", GH_ParamAccess.list);
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

            int stepNumber = 0;
            int pathDivision = 0;

            //retrieve inputs, if nothing is retrieved return
            if (!DA.GetData(0, ref D)) return;
            if (!DA.GetData(1, ref MT)) return;
            if (!DA.GetData(2, ref MF)) return;
            if (!DA.GetData(3, ref TP)) return;
            if (!DA.GetData(4, ref stepNumber)) return;
            if (!DA.GetData(5, ref pathDivision)) return;

            //%%%%%%%% Will require TP to add get methods for matTool and MatForm
            //Retrieve directions, tool, and material form information from the tool path
            //D = TP.GetDirs();
            //MT = TP.MatTool();
            //MF = TP.MatForm();

            //stepNumber = pathDivision - 1;

            //If the tool width is negative it hasn't been set and we should exit and warn the user
            if (MT.toolWidth < 0)
            {
                //output a warning message
                return;
            }

            int meshDivisions = 50;

            //Starting point of the tool
            Point3d toolCenterPoint = new Point3d(0, 0, 1);

            //plane orthogonal to the tool direction
            Plane toolOPlane = new Plane(toolCenterPoint, D);

            //circle to be made into the tool shape
            Circle toolCircle = new Circle(toolOPlane, (MT.toolWidth / 2));

            //cylinder to create a mesh from
            Cylinder toolCylinder = new Cylinder(toolCircle, MT.toolLength);

            //Mesh representation of the tool
            Mesh toolMeshBase = Mesh.CreateFromCylinder(toolCylinder, meshDivisions, meshDivisions);


            //Temporary material form for testing

            Box tempBox = new Box(MF.Pl, new Interval(-5, 5), new Interval(-5, 5), new Interval(-5, 0));
            Mesh[] matMeshSet = new Mesh[1];
            matMeshSet[0] = Mesh.CreateFromBox(tempBox, meshDivisions, meshDivisions, meshDivisions);

            Mesh[] toolMeshSet = new Mesh[1];
            toolMeshSet[0] = new Mesh();
            //toolMeshSet[1] = new Mesh();
            if(toolMeshBase != null) toolMeshSet[0].CopyFrom(toolMeshBase);
            Mesh[] temp = new Mesh[0];
            Mesh[] tempMat = new Mesh[0];
            for (int i = 0; i < TP.Pts.Count; i++)
            {
                double x = TP.Pts[0].Pt.X;
                double y = TP.Pts[0].Pt.Y;
                double z = TP.Pts[0].Pt.Z;

                //total distance to cover to the next tool point
                double dx = x - toolCenterPoint.X;
                double dy = y - toolCenterPoint.Y;
                double dz = z - toolCenterPoint.Z;

                //incremental distance to cover at each step
                dx = dx / pathDivision;
                dy = dy / pathDivision;
                dz = dz / pathDivision;

                for (int j = 0; j < stepNumber; j++)
                {
                    toolMeshSet[0].Translate(new Vector3d(dx, dy, dz));
                    //toolMeshBase.Translate(new Vector3d(dx, dy, dz));
                    //toolMeshSet[0].CopyFrom(toolMeshBase);
                    //temp = Mesh.CreateBooleanUnion(toolMeshSet);
                    //toolMeshSet[0].CopyFrom(temp[0]);
                    matMeshSet = Mesh.CreateBooleanDifference(matMeshSet, toolMeshSet);
                    //matMeshSet = Mesh.CreateBooleanDifference(matMeshSet, toolMeshSet);
                    //toolMeshSet.Clear();
                }

            }

            //Mesh[] tempMat = Mesh.CreateBooleanDifference(matMeshSet, toolMeshSet);
            //matMeshSet = Mesh.CreateBooleanDifference(matMeshSet, toolMeshSet);

            //Set the output to be the tool mesh
            if (matMeshSet != null) DA.SetDataList(0, matMeshSet);
            if (toolMeshSet != null) DA.SetDataList(1, toolMeshSet);
            if (tempMat != null) DA.SetDataList(2, tempMat);

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