using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace CAMel.Types
{
    // Different end mill shapes to consider
    public enum EndShape
    {
        Ball, Square, V, Other
    }
    // Settings for a particular material and tool
    public class MaterialTool : CA_base 
    {
        public string Mat_name;    // Name of the material
        public string Tool_name;   // Name of the tool 
        public double speed;       // speed of spindle (assumed unset for negative values)
        public double feedCut;     // feed rate for cutting (assumed unset for negative values)
        public double feedPlunge;  // feed rate for plunging (assumed unset for negative values)
        public double cutDepth;    // maximum material to cut away (assumed unset for negative values)
        public double finishDepth; // thickness to cut in a finish pass
        public double toolWidth;   // width of tool (assumed unset for negative values)
        public double toolLength;  // length from the tip of the tool to the spindle
        private EndShape ES;        // End shape of the tool

        // settings for curve approximation

        public double tolerance; // The maximum permitted distance of approximation from curve
        public double minStep; // shortest path permitted

        // Default Constructor make everything blank.
        public MaterialTool()
        {
            this.Mat_name = "";
            this.Tool_name = "";
            this.speed = -1;
            this.feedCut = -1;
            this.feedPlunge = -1;
            this.cutDepth = -1;
            this.toolWidth = -1;
            this.toolLength = 0;
            this.tolerance = 0;
            this.minStep = 0;
            this.ES = EndShape.Ball;
        }
        // Just names.
        public MaterialTool(string Mat, string Tool)
        {
            this.Mat_name = Mat;
            this.Tool_name = Tool;
            this.speed = -1;
            this.feedCut = -1;
            this.feedPlunge = -1;
            this.cutDepth = -1;
            this.finishDepth = 0;
            this.toolWidth = -1;
            this.toolLength = 0;
            this.tolerance = 0;
            this.minStep = 0;
            this.ES = EndShape.Ball;
        }
        // Everything, with defaults
        public MaterialTool(string Mat, string Tool, double speed, double feedCut, double feedPlunge, double cutDepth, double finishDepth =0, double width = -1, double tL = 0, EndShape ES = EndShape.Ball,double tol = 0, double mS = 0)
        {
            this.Mat_name = Mat;
            this.Tool_name = Tool;
            this.speed = speed;
            this.feedCut = feedCut;
            this.feedPlunge = feedPlunge;
            this.finishDepth = finishDepth;
            this.cutDepth = cutDepth;
            this.toolWidth = width;
            this.toolLength = tL;
            this.tolerance = tol;
            this.minStep = mS;
            this.ES = ES;
        }
        // Copy Constructor
        public MaterialTool(MaterialTool MT)
        {
            this.Mat_name = MT.Mat_name;
            this.Tool_name = MT.Tool_name;
            this.speed = MT.speed;
            this.feedCut = MT.feedCut;
            this.feedPlunge = MT.feedPlunge;
            this.cutDepth = MT.cutDepth;
            this.finishDepth = MT.finishDepth;
            this.toolWidth = MT.toolWidth;
            this.toolLength = MT.toolLength;
            this.tolerance = MT.tolerance;
            this.minStep = MT.minStep;
            this.ES = MT.ES;
        }
        // Duplicate
        public MaterialTool Duplicate()
        {
            return new MaterialTool(this);
        }

        public override string TypeDescription
        {
            get { return "Details of a Material and Tool"; }
        }

        public override string TypeName
        {
            get { return "MaterialTool"; }
        }

        public override string ToString()
        {
            string outp = this.Tool_name + " into " + this.Mat_name + "\n"
                + "Speed: " + this.speed.ToString() + " Cut Feed: " + this.feedCut.ToString()
                + " Plunge feed: " + this.feedPlunge.ToString() + " Cut Depth: " + this.cutDepth.ToString();
            if (this.finishDepth > 0) outp = outp + " Finish Depth: " + this.toolLength.ToString();
            if (this.tolerance > 0) outp = outp + " Tolerance: " + this.toolLength.ToString();
            if (this.minStep > 0) outp = outp + " Min Step: " + this.toolLength.ToString();
            if (this.toolWidth > 0) outp = outp + " Tool Width: " + this.toolWidth.ToString();
            if (this.toolLength > 0) outp = outp + " Tool Length: " + this.toolLength.ToString();
            return outp;
        }

        /// <summary>
        /// Offset toolpoint so that it does not gouge an angled path. 
        /// </summary>
        public ToolPoint threeAxisHeightOffset(ToolPoint toolPoint, Vector3d travel, Vector3d orth, Machine M)
        {
            // TODO at the moment this offset assumes a round end mill.

            Vector3d os = travel;
            os.Unitize();
            os.Transform(Transform.Rotation(Math.PI / 2, orth,new Point3d(0,0,0)));
            double testd = os * toolPoint.Dir;
            if (testd < 0) os = -1*os;

            ToolPoint osTp = new ToolPoint(toolPoint);   

            // move tool so that it cuts at the toolpoint location and does not gouge.
            osTp.Pt = osTp.Pt + this.toolWidth*(os - toolPoint.Dir)/2;

            //osTp.Pt = osTp.Pt + 1*(os - toolPoint.Dir)/2;
        
            return osTp;
        }
        // Find the path offset so the cutting surface of the tool is on the path
        public Vector3d CutOffset(Vector3d Dir, Vector3d Norm)
        {
            Vector3d uDir = Dir;
            Vector3d uNorm = Norm;
            uDir.Unitize();
            uNorm.Unitize();
            Vector3d os;
            switch (this.ES)
            {
                case EndShape.Ball: // find correct position of ball centre and then push to tip. 
                    os = this.toolWidth * (uNorm + uDir) / 2;
                    break;
                case EndShape.Square: // Cut with corner if the angle is greate than .01 radians
                    if(Vector3d.VectorAngle(uDir,uNorm)-Math.PI < .01)
                    {
                        os = new Vector3d(0, 0, 0);
                    }
                    else
                    {
                        // find the normal to the plane give by the tool direction and the norm
                        Vector3d PlN = Vector3d.CrossProduct(uNorm, uDir);
                        // Now want a vector on that plane orthogonal to tool direction
                        os = this.toolWidth * Vector3d.CrossProduct(uDir, PlN) / 2;
                    }
                    break;
                case EndShape.V: // Just use the tip. Beyond a certain angle this will not work
                                 // TODO store angle of V end mill and use that and width 
                    os = new Vector3d(0, 0, 0);
                    break;
                case EndShape.Other:
                    os = new Vector3d(0, 0, 0);
                    break;
                default:
                    os = new Vector3d(0, 0, 0);
                    break;
            }
            return os;
        }
    }

    // Grasshopper Type Wrapper
    public class GH_MaterialTool : CA_Goo<MaterialTool>
    {
        // Default constructor
        public GH_MaterialTool()
        {
            this.Value = new MaterialTool();
        }
        // Just names.
        public GH_MaterialTool(string Mat, string Tool)
        {
            this.Value = new MaterialTool(Mat, Tool);
        }
        // Name, speed, feed and cut
        public GH_MaterialTool(string Mat, string Tool, double speed, double feedCut, double feedPlunge, double cutDepth, double finishDepth = 0, double width = 0, double length = 0, EndShape ES = EndShape.Ball,double tolerance = 0, double minStep = 0)
        {
            this.Value = new MaterialTool(Mat, Tool, speed, feedCut, feedPlunge, cutDepth, finishDepth, width, length, ES, tolerance, minStep);
        }
        // construct from unwrapped type
        public GH_MaterialTool(MaterialTool MT)
        {
            this.Value = new MaterialTool(MT);
        }
        // Copy Constructor
        public GH_MaterialTool(GH_MaterialTool MT)
        {
            this.Value = new MaterialTool(MT.Value);
        }
        // Duplicate
        public override IGH_Goo Duplicate()
        {
            return new GH_MaterialTool(this);
        }

        // Valid if speed, feeds, and cut depth are set
        public override bool IsValid
        {
            get 
            { 
                if(this.Value.speed >=0 && this.Value.feedCut >=0 && this.Value.feedPlunge >= 0 && this.Value.cutDepth >= 0)
                {
                    return true;
                } 
                else 
                {
                    return false;
                }
            
            }
        }

        public override bool CastTo<Q>( ref Q target)
        {
            if(typeof(Q).IsAssignableFrom(typeof(MaterialTool)))
            {
                object ptr = this.Value;
                target = (Q)ptr;
                return true;
            }
            return false;
        }

        public override bool CastFrom( object source )
        {
            if( source is MaterialTool)
            {
                this.Value = new MaterialTool((MaterialTool)source);
                return true;
            }
            return false;
        }
    }

    // Grasshopper Parameter Wrapper
    public class GH_MaterialToolPar : GH_Param<GH_MaterialTool>
    {
        public GH_MaterialToolPar() : 
            base("Material/Tool","MatTool","Contains a collection of Material Tool Pairs","CAMel","  Params",GH_ParamAccess.item) {}
        public override Guid ComponentGuid
        {
            get { return new Guid("147c0724-2d2b-4316-a889-d59fbe748b58"); }
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
                return Properties.Resources.materialtool;
            }
        }
    }

}