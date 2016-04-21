using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Display;
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
            pManager.AddIntegerParameter("Machining percentage", "MP", "The percentage of the way through the machining process", GH_ParamAccess.item, 0);
           // pManager.AddNumberParameter("Step Size", "SS", "The maximum step size for the tool path stepping", GH_ParamAccess.item, 0.5f);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Material Shape base", "MS", "The shape of the material", GH_ParamAccess.list);
            pManager.AddMeshParameter("ToolShape", "TS", "The mesh that will form the shape of the tool.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Render", "R", "Shape post machining", GH_ParamAccess.list);
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

            int machiningPercentage = 0;
            float stepSize = 0;

            //retrieve inputs, if nothing is retrieved return
            if (!DA.GetData(0, ref D)) return;
            if (!DA.GetData(1, ref MT)) return;
            if (!DA.GetData(2, ref MF)) return;
            if (!DA.GetData(3, ref TP)) return;
            if (!DA.GetData(4, ref machiningPercentage)) return;
            //if (!DA.GetData(5, ref stepSize)) return;

            //%%%%%%%% Will require TP to add get methods for matTool and MatForm
            //Retrieve directions, tool, and material form information from the tool path
            //D = TP.GetDirs();
            //MT = TP.MatTool();
            //MF = TP.MatForm();

            //machiningPercentage = stepSize - 1;

            //If the tool width is negative it hasn't been set and we should exit and warn the user
            if (MT.toolWidth < 0)
            {
                //output a warning message
                return;
            }

            int meshDivisions = 25;

            //Starting point of the tool
            Point3d toolCenterPoint = new Point3d(TP.Pts[0].Pt.X, TP.Pts[0].Pt.Y, TP.Pts[0].Pt.Z);

            //plane orthogonal to the tool direction
            Plane toolOPlane = new Plane(toolCenterPoint, D);

            //circle to be made into the tool shape
            Circle toolCircle = new Circle(toolOPlane, (MT.toolWidth / 2));

            //cylinder to create a mesh from
            Cylinder toolCylinder = new Cylinder(toolCircle, MT.toolLength);

            //Mesh representation of the tool
            Mesh toolMeshBase = Mesh.CreateFromCylinder(toolCylinder, meshDivisions, meshDivisions);


            //Temporary material form for testing

            //Box tempBox = new Box(MF.Pl, new Interval(-5, 5), new Interval(-5, 5), new Interval(-5, 0));
            Brep[] matMeshSet = new Brep[1];
            matMeshSet[0] = Brep.CreateFromBox(new BoundingBox(-5,-5,-5,5,5,0));
            if (matMeshSet != null) DA.SetDataList(0, matMeshSet);


            Brep[] toolMeshSet = new Brep[1];
            toolMeshSet[0] = new Brep();
            //toolMeshSet[1] = new Mesh();
            //if(toolMeshBase != null) toolMeshSet[0].CopyFrom(toolMeshBase);
            Mesh[] temp = new Mesh[0];
            //Mesh[] tempMat = new Mesh[0];
            Brep extrusionTest = new Brep();
            Extrusion extruder = new Extrusion();
            List<Curve> debugginCurves = new List<Curve>();

            //Surface cuttingExtrusion = new Surface();
            int i = 1;
            do
            {









                double x = TP.Pts[i].Pt.X;
                double y = TP.Pts[i].Pt.Y;
                double z = TP.Pts[i].Pt.Z;

                //total distance to cover to the next tool point
                double dx = x - TP.Pts[i - 1].Pt.X;
                double dy = y - TP.Pts[i - 1].Pt.Y;
                double dz = z - TP.Pts[i - 1].Pt.Z;
                double totalD = (Math.Sqrt(dx * dx + dy * dy + dz * dz));
                //double numDivisions = 1;



                Vector3d toolPointDirection = new Vector3d(dx, dy, dz);


                //debugging, looking at all the toolPointDirections
                debugginCurves.Add((toolPointDirection));


                Plane toolPointOrientation = new Plane(toolCenterPoint, toolPointDirection);
                RhinoViewport viewPort = new RhinoViewport();
                viewPort.SetCameraLocation(TP.Pts[i].Pt, false);
                viewPort.SetCameraDirection(toolPointDirection, false);
                //Rectangle3d toolOutline = new Rectangle3d(new Plane(new Point3d(0,0,0), toolPointDirection),MT.toolWidth, MT.toolLength);
                //cuttingExtrusion.create
                Polyline[] outlines = toolMeshBase.GetOutlines(toolPointOrientation);
                //Surface cuttingExtrusion = Surface.CreateExtrusion(toolOutline.ToNurbsCurve(), toolPointDirection);
                //extruder.SetOuterProfile(outlines[0].ToNurbsCurve(), true);
                extruder = Extrusion.Create(outlines[0].ToNurbsCurve(), totalD, true);
                toolMeshSet[0] = extruder.ToBrep();
                extrusionTest = toolMeshSet[0];
                matMeshSet = Brep.CreateBooleanDifference(matMeshSet, toolMeshSet, 0.01);

                //Update stuff for the next round
                toolMeshBase.Translate(new Vector3d(dx, dy, dz));
                i++;
                /*//incremental distance to cover at each step
                while(totalD > stepSize){
                    dx = dx / 2;
                    dy = dy / 2;
                    dz = dz / 2;
                    totalD = (Math.Sqrt(dx * dx + dy * dy + dz * dz));
                    numDivisions *= 2;
                }

                for (int j = 0; j < numDivisions; j++)
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
                */

            } while (i < TP.Pts.Count);

            //Mesh[] tempMat = Mesh.CreateBooleanDifference(matMeshSet, toolMeshSet);
            //matMeshSet = Mesh.CreateBooleanDifference(matMeshSet, toolMeshSet);

            //Set the output to be the tool mesh
            if (matMeshSet != null) DA.SetDataList(0, matMeshSet);
            //if (toolMeshSet != null) DA.SetDataList(1, toolMeshSet);
            if (matMeshSet != null) DA.SetDataList(2, matMeshSet);
            if (extrusionTest != null) DA.SetData(1, extrusionTest);

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