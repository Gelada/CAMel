using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Display;
using CAMel.Types;

using System.Drawing;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel.Attributes;

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
        /// Variables used to calculate the rendering
        /// </summary>

        


        

        

        //this is the variable the stores the cached meshes, in the future the size of this should probably be user defined
        private Mesh[] storedMeshes;

        //Variables for the play button, run checks to see if the component should run solve instance again, hasRun checks if anything has changed, hasCache might be used to give better user friendliness
        private bool run = true;
        private bool hasRun = false;
        private bool hasCache = false;


        public override void CreateAttributes()
        {
            m_attributes = new AnimatorCustomAttributes(this);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Machine Instruction", "MI", "placeholder", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Machining step", "MS", "The step of the way through the machining process", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Render", "R", "Shape post machining", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (run)
            {
                //Set the size of the cached meshes
                storedMeshes = new Mesh[200];
                MachineInstruction MI = new MachineInstruction();

                int machiningPercentage = 0;
                int meshDivisions = 15;

                //retrieve inputs, if nothing is retrieved return
                if (!DA.GetData(0, ref MI)) return;
                if (!DA.GetData(1, ref machiningPercentage)) return;


             

                //Temporary material form for testing, TODO: Take in a user defined material shape

                Mesh[] matMeshSet = new Mesh[1];
                matMeshSet[0] = Mesh.CreateFromBox(new BoundingBox(-6, -6, -6, 6, 6, 0), meshDivisions, meshDivisions, meshDivisions);

                //List of the Machine operations to iterate through
                List<MachineOperation> MOs = MI.MOs;

                //Meshees to be grouped together, thsi is a list because boolean operations take in an enumerable 
                List<Mesh> toolUnionSet = new List<Mesh>();
                Mesh[] a = new Mesh[1];

                int totalSteps = 0;
                int j = 0;
                foreach (MachineOperation MO in MOs)
                {
                    foreach (ToolPath TP in MO.TPs)
                    {

                        //If tool isn't set don't run
                        if (TP.MatTool.toolWidth < 0) return;


                        //-------------------------------------------------------------------------------------
                        //Construct the tool  Mesh, to be replaced by user input
                        //-------------------------------------------------------------------------------------
                        //Starting point of the tool
                        Point3d toolCenterPoint = new Point3d(TP.Pts[0].Pt.X, TP.Pts[0].Pt.Y, TP.Pts[0].Pt.Z);

                        //plane orthogonal to the tool direction
                        Plane toolOPlane = new Plane(toolCenterPoint, new Vector3d(0, 0, 1));

                        //circle to be made into the tool shape
                        Circle toolCircle = new Circle(toolOPlane, (TP.MatTool.toolWidth / 2));

                        //cylinder to create a mesh from
                        Cylinder toolCylinder = new Cylinder(toolCircle, TP.MatTool.toolLength);

                        //Mesh representation of the tool
                        Mesh toolMeshBase = Mesh.CreateFromCylinder(toolCylinder, meshDivisions, meshDivisions);
                        //if (toolMeshBase != null) DA.SetData(4, toolMeshBase);
                        //--------------------------------------------------------------------------------------



                        //Set to be used with boolean differences
                        Mesh[] toolMeshSet = new Mesh[1];


                            
                        //toolMeshSet[0] = toolMeshBase;
                        a[0] = toolMeshBase;
                        //if (a != null) DA.SetDataList(1, a);
                        Mesh[] tempMatSet = Mesh.CreateBooleanDifference(matMeshSet, a);

                        if (tempMatSet != null)
                        {
                            matMeshSet = tempMatSet;
                        }

                        //This would be used in an implementation where we want the total "negative space"
                        toolUnionSet.Add(toolMeshSet[0]);

                        

                        
                        Mesh[] tempMat = new Mesh[0];
                          

                        int i = 1;
                        totalSteps++;
                        int tempPercent = machiningPercentage - totalSteps;
                        if (tempPercent < 0) tempPercent = 1;
                        if (tempPercent >= TP.Pts.Count)
                        {
                            tempPercent = TP.Pts.Count - 1;
                        }
                            
                        while (i < TP.Pts.Count-1)
                        {


                            //-----------------------------------------------------
                            //Method to call for generating an extrusion
                            //-----------------------------------------------------
                           
                            //a[1] = (generateExtrusion(toolMeshBase, TP.Pts[i - 1].Pt, TP.Pts[i].Pt));
                            
                           
                            //Mesh[] tempToolSet = Mesh.CreateBooleanUnion(toolUnionSet, 0.01);
                            //if (tempToolSet != null)
                            //    toolUnionSet.Add(tempToolSet[0]);






                            //Update toolMeshBase loccation for the next round
                            toolMeshBase.Translate(new Vector3d(TP.Pts[i].Pt.X - TP.Pts[i - 1].Pt.X, TP.Pts[i].Pt.Y - TP.Pts[i - 1].Pt.Y, TP.Pts[i].Pt.Z - TP.Pts[i - 1].Pt.Z));

                            //Remove the Tool shape from the next toolpoint

                            //if(i%3 == 1)
                            a[0] = toolMeshBase;
                            //else
                            //    a[0].Append(toolMeshBase);
                            //if (a != null) DA.SetDataList(1, a);

                            //remove the tool from the material
                            tempMatSet = Mesh.CreateBooleanDifference(matMeshSet, a);

                            //if the removal didn't result in an error then set the result, otherwise ignore the result
                            if (tempMatSet != null)
                            {
                                matMeshSet = tempMatSet;

                                //Caching stuff, every third mesh we save to the set of stored meshes
                                if ((j) % 3 ==0)
                                {
                                    double c = i;
                                    int k =(int) Math.Floor((c) / 3);
                                    storedMeshes[k] = tempMatSet[0];
                                        
                                }
                            }


                            j++;
                            i++;
                            totalSteps++;
                        }
                    }
                }




               //set the output render
                if (matMeshSet != null) DA.SetDataList(0, matMeshSet);


                //set the control variables to know that we have computed the solution
                run = false;
                hasRun = true;
                hasCache = true;
            }

            else if (hasCache)
            {
                int machiningPercentage = 0;
                if (!DA.GetData(1, ref machiningPercentage)) return;

                //int outputNum = 0;
                //while (outputNum * 5 <= machiningPercentage) outputNum++;

                //This implementation will take the cached mesh that is closest to the machining percentage without going past it

                //This will make sure that if the user inputs a number larger than the machining percentage, it will still output something
                //This may not be necessary when the switch is made to an actual percentage
                while (storedMeshes[machiningPercentage] == null) machiningPercentage--;

                Mesh[] matMeshSet = new Mesh[1];

                //set the output to be the desired cached mesh number
                matMeshSet[0] = storedMeshes[machiningPercentage];











                //-----------------------------------------------------------------------------------------------------------
                //This implementation will need to be edited but it is the foundation for computing to a machine percentage,
                //rather than the first cache before the target
                //This also still utilizes breps
                //-----------------------------------------------------------------------------------------------------------
                /*

                MachineInstruction MI = new MachineInstruction();

                
                int meshDivisions = 15;

                //retrieve inputs, if nothing is retrieved return
                if (!DA.GetData(0, ref MI)) return;
                

                //Temporary material form for testing

               
                Brep[] matBrepSet = new Brep[1];
                matBrepSet[0] = Brep.CreateFromBox(new BoundingBox(-0.5, -0.5, -0.75, 6.5, 6.5, 0));
                if (matBrepSet != null) DA.SetDataList(5, matBrepSet);
                int totalSteps = 0;
                List<MachineOperation> MOs = MI.MOs;
                foreach (MachineOperation MO in MOs)
                {
                    foreach (ToolPath TP in MO.TPs)
                    {
                        if (totalSteps < machiningPercentage)
                        {
                            //If tool isn't set don't run
                            if (TP.MatTool.toolWidth < 0) return;


                            //-------------------------------------------------------------------------------------
                            //Construct the tool  Mesh, to be replaced by user input
                            //-------------------------------------------------------------------------------------
                            //Starting point of the tool
                            Point3d toolCenterPoint = new Point3d(TP.Pts[0].Pt.X, TP.Pts[0].Pt.Y, TP.Pts[0].Pt.Z);

                            //plane orthogonal to the tool direction
                            Plane toolOPlane = new Plane(toolCenterPoint, new Vector3d(0, 0, 1));

                            //circle to be made into the tool shape
                            Circle toolCircle = new Circle(toolOPlane, (TP.MatTool.toolWidth / 2));

                            //cylinder to create a mesh from
                            Cylinder toolCylinder = new Cylinder(toolCircle, TP.MatTool.toolLength);

                            //Mesh representation of the tool
                            Mesh toolMeshBase = Mesh.CreateFromCylinder(toolCylinder, meshDivisions, meshDivisions);
                            if (toolMeshBase != null) DA.SetData(4, toolMeshBase);
                            //--------------------------------------------------------------------------------------




                            Brep[] toolBrepSet = new Brep[1];


                            toolBrepSet[0] = Brep.CreateFromMesh(toolMeshBase, true);
                            matBrepSet = Brep.CreateBooleanDifference(matBrepSet, toolBrepSet, 0.01);
                            if (matBrepSet != null) DA.SetDataList(2, matBrepSet);
                            if (toolBrepSet != null) DA.SetDataList(1, toolBrepSet);

                            //toolBrepSet[1] = new Mesh();
                            //if(toolMeshBase != null) toolBrepSet[0].CopyFrom(toolMeshBase);
                            //Mesh[] temp = new Mesh[0];
                            Brep[] tempMat = new Brep[0];
                            List<Brep> extrusionTest = new List<Brep>();

                            Extrusion extruder = new Extrusion();
                            List<Line> debugginLines = new List<Line>();
                            Polyline[] outlines = new Polyline[0];

                            //Surface cuttingExtrusion = new Surface();
                            int i = 1;
                            //Increment here for the default difference
                            totalSteps++;

                            //TODO: Add proper checks for boundary cases on percentage (like negative and whatnot)
                            //TODO: Refactor to allow the followthrough to work properly when percentage has been exceeded
                            int tempPercent = machiningPercentage - totalSteps;
                            if (tempPercent < 0) tempPercent = 1;
                            if (tempPercent >= TP.Pts.Count)
                            {
                                tempPercent = TP.Pts.Count - 1;
                            }
                            while (i < tempPercent)
                            {


                                //-----------------------------------------------------
                                //Method to call for differencing between two points
                                //-----------------------------------------------------
                                matBrepSet = removeLine(matBrepSet, toolMeshBase, TP.Pts[i - 1].Pt, TP.Pts[i].Pt);
                                if (matBrepSet != null)
                                    matBrepSet = removeLine(matBrepSet, toolMeshBase, TP.Pts[i - 1].Pt, TP.Pts[i].Pt); ;


                                //debugging, looking at all the toolPointDirections
                                debugginLines.Add(new Line(TP.Pts[i - 1].Pt, TP.Pts[i].Pt));



                                //outlines for debugging
                                //if (outlines != null) DA.SetDataList(3, outlines);

                                //Old extrusion technique
                                //Surface cuttingExtrusion = Surface.CreateExtrusion(toolOutline.ToNurbsCurve(), toolPointDirection);
                                //extruder.SetOuterProfile(outlines[0].ToNurbsCurve(), true);




                                //Update stuff for the next round
                                toolMeshBase.Translate(new Vector3d(TP.Pts[i].Pt.X - TP.Pts[i - 1].Pt.X, TP.Pts[i].Pt.Y - TP.Pts[i - 1].Pt.Y, TP.Pts[i].Pt.Z - TP.Pts[i - 1].Pt.Z));

                                //Remove the Tool shape from the next toolpoint
                                toolBrepSet[0] = Brep.CreateFromMesh(toolMeshBase, true);
                                tempMat = Brep.CreateBooleanDifference(matBrepSet, toolBrepSet, 0.005);
                                if (tempMat != null)
                                    matBrepSet = tempMat;
                                //if (matBrepSet != null) DA.SetDataList(2, matBrepSet);
                                if (toolBrepSet != null) DA.SetDataList(1, toolBrepSet);
                                i++;
                                totalSteps++;


                            }

                            if (debugginLines != null) DA.SetDataList(0, debugginLines);
                            //if (toolBrepSet != null) DA.SetDataList(1, toolBrepSet);
                            if (extrusionTest != null) DA.SetDataList(1, extrusionTest);
                        }
                    }
                }

                 */

                if (matMeshSet != null) DA.SetDataList(0, matMeshSet);
            }

            else
                hasRun = false;

        }



        //--------------------------------------------------------------------------------------------------------------------------------------
        //Method to take some extrusion of a tool and remove it from the material, and then return the result
        //if the removal results in a null value for the material, then return the original thing? consider other ways of handling this error
        //--------------------------------------------------------------------------------------------------------------------------------------
        private Mesh[] removeLine(Mesh[] inputMaterial, Mesh toolShape, Point3d point1, Point3d point2)
        {
            //--------------------------------------------------------------
            //Building the vector that goes from point 1 to point 2
            //--------------------------------------------------------------
            double dx = point2.X - point1.X;
            double dy = point2.Y - point1.Y;
            double dz = point2.Z - point1.Z;
            double totalD = (Math.Sqrt(dx * dx + dy * dy + dz * dz));
            Vector3d toolPointDirection = new Vector3d(dx, dy, dz);


            //Plane orthogonal to next movemant, this will be used to project the toolshape at the proper orientation, and it will be centered around point 1
            Plane toolPointOrientation = new Plane(point1, toolPointDirection);


            //Outline of the tool from the plane's perspective
            Polyline[] outlines = toolShape.GetOutlines(toolPointOrientation);

            //Extrusion of the outline that is the length of the vector between point1 and point2
            //This extrusion uses the direction of the plane used to create the outlines
            Extrusion extruder = Extrusion.Create(outlines[0].ToNurbsCurve(), totalD, true);


            //------------------------------------------------------------------------------
            //container to be used for boolean operations
            //set the object in the container to be a Mesh representation of the extrusion
            //------------------------------------------------------------------------------
            Mesh[] toolMeshSet = new Mesh[1];
            //toolMeshSet[0] = extruder.ToMesh();


            //Output used for testing
            //extrusionTest.Add(toolMeshSet[0]);


            //Remove the extrusion from the input material
            //Mesh[] tempMat = Mesh.CreateBooleanDifference(inputMaterial, toolMeshSet, 0.005);

            //Errors have been occuring with this feature, for now, if the boolean difference returns a null object you should return the material that was input
           // if (tempMat != null)
           //    return tempMat;
          //  else
                return inputMaterial;
        }





        //--------------------------------------------------------------------------------------------------------------------------------------
        //Method to take some extrusion of a tool and remove it from the material, and then return the result
        //if the removal results in a null value for the material, then return the original thing? consider other ways of handling this error
        //--------------------------------------------------------------------------------------------------------------------------------------
        private Mesh generateExtrusion(Mesh toolShape, Point3d point1, Point3d point2)
        {
            //--------------------------------------------------------------
            //Building the vector that goes from point 1 to point 2
            //--------------------------------------------------------------
            double dx = point2.X - point1.X;
            double dy = point2.Y - point1.Y;
            double dz = point2.Z - point1.Z;
            double totalD = (Math.Sqrt(dx * dx + dy * dy + dz * dz));
            Vector3d toolPointDirection = new Vector3d(dx, dy, dz);


            //Plane orthogonal to next movemant, this will be used to project the toolshape at the proper orientation, and it will be centered around point 1
            Plane toolPointOrientation = new Plane(point1, toolPointDirection);


            //Outline of the tool from the plane's perspective
            Polyline[] outlines = toolShape.GetOutlines(toolPointOrientation);

            //Extrusion of the outline that is the length of the vector between point1 and point2
            //This extrusion uses the direction of the plane used to create the outlines
            Extrusion extruder = Extrusion.Create(outlines[0].ToNurbsCurve(), totalD, true);


            //------------------------------------------------------------------------------
            //container to be used for boolean operations
            //set the object in the container to be a Mesh representation of the extrusion
            //------------------------------------------------------------------------------
            Mesh[] temp = Mesh.CreateFromBrep(extruder.ToBrep());
            return(temp[0]);
            //return null;
        }







        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                 return Properties.Resources.animator;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{15db275c-1d75-439d-a9c0-f602d028334f}"); }
        }

        public class AnimatorCustomAttributes : GH_ComponentAttributes
        {
            public AnimatorCustomAttributes(C_Animator owner) : base(owner) { }

            #region Custom layout logic
            private RectangleF PlayBounds { get; set; }
            private RectangleF SamplesSomeBounds { get; set; }
            //          private RectangleF SamplesManyBounds { get; set; }

            protected override void Layout()
            {
                base.Layout();
                //We'll extend the basic layout by adding three regions to the bottom of this component,
                PlayBounds = new RectangleF(Bounds.X + 2, Bounds.Bottom - 2, Bounds.Width - 4, 20);
                // SamplesSomeBounds = new RectangleF(Bounds.X, Bounds.Bottom + 20, Bounds.Width, 20);
                //              SamplesManyBounds = new RectangleF(Bounds.X, Bounds.Bottom + 40, Bounds.Width, 20);
                Bounds = new RectangleF(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height + 20);
            }
            #endregion

            #region Custom Mouse handling
            public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    C_Animator comp = Owner as C_Animator;

                    if (PlayBounds.Contains(e.CanvasLocation))
                    {
                        //if (comp.SampleCount == 10) return GH_ObjectResponse.Handled;
                        comp.RecordUndoEvent("Play");
                        //comp.SampleCount = 10;
                        comp.run = true;
                        comp.ExpireSolution(true);
                        //        comp.hasRun = false;
                        return GH_ObjectResponse.Handled;
                    }


                    //                  if (SamplesManyBounds.Contains(e.CanvasLocation))
                    //               {
                    //                  if (comp.SampleCount == 1000) return GH_ObjectResponse.Handled;
                    //                   comp.RecordUndoEvent("Many Samples");
                    //                 comp.SampleCount = 1000;
                    //               comp.ExpireSolution(true);
                    //             return GH_ObjectResponse.Handled;
                    //       }
                }
                return base.RespondToMouseDown(sender, e);
            }
            #endregion

            #region Custom Render logic
            protected override void Render(GH_Canvas canvas, System.Drawing.Graphics graphics, GH_CanvasChannel channel)
            {
                switch (channel)
                {
                    case GH_CanvasChannel.Objects:
                        //We need to draw everything outselves.
                        base.RenderComponentCapsule(canvas, graphics, true, false, false, true, true, true);

                        C_Animator comp = Owner as C_Animator;

                        GH_Capsule buttonFew = GH_Capsule.CreateCapsule(PlayBounds, comp.hasRun == true ? GH_Palette.Blue : GH_Palette.Error);
                        buttonFew.Render(graphics, this.Selected, Owner.Locked, Owner.Hidden);
                        buttonFew.Dispose();

                        //    GH_Capsule buttonSome = GH_Capsule.CreateCapsule(SamplesSomeBounds, comp.SampleCount == 100 ? GH_Palette.Black : GH_Palette.White);
                        //   buttonSome.Render(graphics, this.Selected, Owner.Locked, Owner.Hidden);
                        //    buttonSome.Dispose();

                        //         GH_Capsule buttonMany = GH_Capsule.CreateCapsule(SamplesManyBounds, comp.SampleCount == 1000 ? GH_Palette.Black : GH_Palette.White);
                        //       buttonMany.Render(graphics, this.Selected, Owner.Locked, Owner.Hidden);
                        //     buttonMany.Dispose();

                        graphics.DrawString("▶", GH_FontServer.Standard, Brushes.Black, PlayBounds, GH_TextRenderingConstants.CenterCenter);
                        //  graphics.DrawString("Some", GH_FontServer.Standard, comp.SampleCount == 100 ? Brushes.White : Brushes.Black, SamplesSomeBounds, GH_TextRenderingConstants.CenterCenter);
                        //       graphics.DrawString("Many", GH_FontServer.Standard, comp.SampleCount == 1000 ? Brushes.White : Brushes.Black, SamplesManyBounds, GH_TextRenderingConstants.CenterCenter);

                        break;
                    default:
                        base.Render(canvas, graphics, channel);
                        break;
                }
            }
            #endregion

        }
    }
}