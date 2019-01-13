using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types;

namespace CAMel
{
    public class C_HelixSurfacePath : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateToolPath class.
        /// </summary>
        public C_HelixSurfacePath()
            : base("Create Helix Surfacing Path", "SurfacePath",
                "Create a helical surfacing recipe",
                "CAMel", " ToolPaths")
        {
        }

        // put this item in the second batch (surfacing strategies)
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Bounding Box", "BB", "Region to Mill as a bounding box oriented by Dir, will be calulated if you add the Mesh or Brep to Mill.", GH_ParamAccess.item);
            pManager.AddCurveParameter("Curve", "C", "Curve to run parallel to", GH_ParamAccess.item);
            pManager[1].Optional = true; // Curve
            pManager.AddPlaneParameter("Direction", "Dir", "Plane to use, Helix around Z.", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.item);
            pManager[3].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddIntegerParameter("Tool Direction", "TD", "Method used to calculate tool direction for 5-Axis\n 0: Projection\n 1: Path Tangent\n 2: Path Normal\n 3: Normal", GH_ParamAccess.item,0);
            pManager.AddNumberParameter("Step over", "SO", "Stepover as a mutliple of tool width. Default to Tools side load (for negavtive values).", GH_ParamAccess.item, -1);
            pManager.AddBooleanParameter("Clockwise", "CW", "Run clockwise as you rise around the piece. For a clockwise bit this gives conventional cutting. ", GH_ParamAccess.item, true);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_SurfacePathPar(), "SurfacePath", "SP", "Surfacing Path", GH_ParamAccess.item);
            //pManager.AddCurveParameter("Paths", "P", "Paths", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            IGH_Goo G = null; 
            BoundingBox BB = new BoundingBox(); // region to mill
            Curve C = null; // path to move parallel to 
            Plane Dir = Plane.WorldXY; // Plane to rotate in as you rise.
            MaterialTool MT = null; // The materialtool, mainly for tool width
            int TD=0;
            double stepOver = 0;
            bool CW = true; // Go up clockwise if true.
            bool createcurve = false; // was a curve passed in or do we go to default/

            if (!DA.GetData(0, ref G)) { return; }
            if (!DA.GetData(1, ref C)) { createcurve = true; }
            if (!DA.GetData(2, ref Dir)) { return; }
            if (!DA.GetData(3, ref MT)) { return; }
            if (!DA.GetData(4, ref TD)) { return; }
            if (!DA.GetData(5, ref stepOver)) { return; }
            if (!DA.GetData(6, ref CW)) { return; }

            if (stepOver <0) { stepOver = MT.sideLoad; }
            if (stepOver > MT.sideLoad) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Stepover exceeds suggested sideLoad for the material/tool."); }

            // process the bounding box

            if (!G.CastTo<BoundingBox>(out BB))
            {
                if (G.CastTo<Surface>(out Surface S))
                {
                    BB = S.GetBoundingBox(Dir);// extents of S in the coordinate system
                    Dir.Origin = Dir.PointAt(BB.Center.X, BB.Center.Y, BB.Center.Z); // Centre everything
                    BB = S.GetBoundingBox(Dir); // extents of S in the coordinate system
                }
                else if (G.CastTo<Brep>(out Brep B))
                {
                    BB = B.GetBoundingBox(Dir);// extents of S in the coordinate system
                    Dir.Origin = Dir.PointAt(BB.Center.X, BB.Center.Y, BB.Center.Z); // Centre everything
                    BB = B.GetBoundingBox(Dir); // extents of S in the coordinate system
                }
                else if (G.CastTo<Mesh>(out Mesh M))
                {
                    BB = M.GetBoundingBox(Dir);// extents of S in the coordinate system
                    Dir.Origin = Dir.PointAt(BB.Center.X, BB.Center.Y, BB.Center.Z); // Centre everything
                    BB = M.GetBoundingBox(Dir); // extents of S in the coordinate system
                }
                else
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The region to mill (BB) must be a bounding box, surface, mesh or brep.");
                }

                BB.Inflate(MT.toolWidth);
            }

            // set Surfacing direction
            SurfToolDir STD;
            switch (TD)
            {
                case 0:
                    STD = SurfToolDir.Projection;
                    break;
                case 1:
                    STD = SurfToolDir.PathTangent;
                    break;
                case 2:
                    STD = SurfToolDir.PathNormal;
                    break;
                case 3:
                    STD = SurfToolDir.Normal;
                    break;
                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input parameter TD can only have values 0,1,2 or 3");
                    return;
            }

            double outerradius = (new Vector3d(BB.Max.X-BB.Min.X,BB.Max.Y-BB.Min.Y,0)).Length/2;
            Cylinder Cy = new Cylinder(new Circle(Dir, outerradius))
            {
                Height1 = BB.Min.Z,
                Height2 = BB.Max.Z
            };

            // Use Toolpath so we standardise Curve convertion
            ToolPath CTP = new ToolPath(MT);

            double Zmin=0, Zmax=0;
            int i;
            double addangle = 90;
            if (createcurve)
            {
                for (i = 0; i < addangle; i++)
                {
                    CTP.Add(new ToolPoint(new Point3d(outerradius, 2 * Math.PI * i / addangle, 0)));
                }
                Zmin = Zmax = 0;
            }
            else
            {
                CTP.convertCurve(C, new Vector3d(0, 0, 1));
                Point3d CylPt = new Point3d();
                bool first = true;
                double turns = 0;
                double angle = 0;
                // convert to cylindrical coordinates
                foreach (ToolPoint tp in CTP)
                {
                    Dir.RemapToPlaneSpace(tp.pt, out CylPt);
                    Point3d temp = toCyl(CylPt);
                    temp.X = outerradius;
                    tp.pt = temp;
                    if( first )
                    {
                        Zmin = tp.pt.Z;
                        Zmax = tp.pt.Z;
                        angle = tp.pt.Y;
                        first = false;
                    }
                    else if (tp.pt.Z < Zmin) { Zmin = tp.pt.Z; }
                    else if (tp.pt.Z > Zmax) { Zmax = tp.pt.Z; }

                    if (angle > 3.0 * Math.PI / 2.0 && tp.pt.Y < Math.PI / 2.0)
                    {
                        turns = turns + 2.0 * Math.PI;
                    }
                    else if (angle < Math.PI / 2.0 && tp.pt.Y > 3.0 * Math.PI / 2.0)
                    {
                        turns = turns - 2.0 * Math.PI;
                    }
                    angle = tp.pt.Y;
                    temp = tp.pt;
                    temp.Y = temp.Y + turns;
                    tp.pt = temp;
                }

                // complete loop by adding points going from
                // the end point to the start point
                Point3d startPt = CTP.firstP.pt;
                Point3d endPt = CTP.lastP.pt;
                if (endPt.Y > 0)
                { startPt.Y = startPt.Y + turns + 2.0 * Math.PI; }
                else
                { startPt.Y = startPt.Y + turns - 2.0 * Math.PI; }


                int shiftl = (int)Math.Ceiling(addangle*Math.Abs((startPt.Y - endPt.Y)/(2.0*Math.PI)));
                for (i = 1; i < shiftl; i++)
                {
                    CTP.Add(new ToolPoint(
                        new Point3d(outerradius,
                            (i * startPt.Y + (shiftl - i) * endPt.Y) / shiftl,
                            (i * startPt.Z + (shiftl - i) * endPt.Z) / shiftl)
                        ));
                }
                
            }

            // Create spiral from the loop
            double winding = (CTP.lastP.pt.Y - CTP.firstP.pt.Y)/(2.0*Math.PI);
            double raisePer =(stepOver * MT.toolWidth); // height dealt with by each loop
            double rot =
                ((BB.Max.Z - BB.Min.Z) // eight of surface
                + (Zmax - Zmin) // height variation in path
                )
                / (winding*raisePer);
            
            raisePer = raisePer / (2.0 * Math.PI);      // convert to per radian

            List<Point3d> SpiralPath = new List<Point3d>();

            Point3d tempPt;
            for(i=-1;i<=Math.Abs(rot);i++) // strange limits to make sure we go top to bottom
            {
                for (int j = 0; j < CTP.Count; j++)
                {
                    tempPt = fromCyl(new Point3d(
                        outerradius,
                        -CTP[j].pt.Y, 
                        BB.Min.Z - Zmax + CTP[j].pt.Z + (2.0 * Math.PI * winding * i + CTP[j].pt.Y) * raisePer));
                    tempPt = Dir.PointAt(tempPt.X, tempPt.Y, tempPt.Z);
                    SpiralPath.Add(tempPt);
                }
            }

            List<Curve> Paths = new List<Curve>
            {
                Curve.CreateInterpolatedCurve(SpiralPath, 3)
            };

            LineCurve CC = new LineCurve(
                Dir.PointAt(BB.Center.X, BB.Center.Y, BB.Min.Z),
                Dir.PointAt(BB.Center.X, BB.Center.Y, BB.Max.Z));

            SurfacePath SP = new SurfacePath(Paths, Dir.ZAxis, CC, STD);
            DA.SetData(0, new GH_SurfacePath(SP));
            DA.SetDataList(1, Paths);

        }

        // convert to cylindrical coordinate
        Point3d toCyl(Point3d Pt)
        {
            Vector3d PlPt = new Vector3d(Pt.X, Pt.Y, 0);
            double angle = Math.Atan2(Pt.Y, Pt.X);
            if (angle < 0) { angle = angle + Math.PI * 2.0; }
            return new Point3d(PlPt.Length,angle,Pt.Z);
        }
        // convert from cylindrical coordinate
        Point3d fromCyl(Point3d Pt)
        {
            return new Point3d(Pt.X*Math.Cos(Pt.Y), Pt.X*Math.Sin(Pt.Y), Pt.Z);
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
                return Properties.Resources.surfacinghelix;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{504D62AA-7B6A-486E-8499-7D4BFB43AEFA}"); }
        }
    }
}