using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types.Machine;

namespace CAMel.Types
{
    // Different end mill shapes to consider
    public enum EndShape
    {
        Ball, Square, V, Other, Error
    }
    // Settings for a particular material and tool
    public class MaterialTool : ICAMel_Base 
    {
        public string matName    { get; set; } // Name of the material
        public string toolName   { get; set; } // Name of the tool 
        public int toolNumber    { get; set; } // Number of the tool
        public double speed       { get; set; } // speed of spindle (assumed unset for negative values)
        public double feedCut     { get; set; } // feed rate for cutting (assumed unset for negative values)
        public double feedPlunge  { get; set; } // feed rate for plunging (assumed unset for negative values)
        public double cutDepth    { get; set; } // maximum material to cut away (assumed unset for negative values)
        public double finishDepth { get; set; } // thickness to cut in a finish pass
        public double toolWidth   { get; set; } // width of tool (assumed unset for negative values)
        public double toolLength  { get; set; } // length from the tip of the tool to the spindle
        public EndShape shape     { get; set; } // End shape of the tool

        // settings for curve approximation

        public double tolerance { get; set; } // The maximum permitted distance of approximation from curve
        public double minStep { get; set; } // shortest path permitted

        // Default Constructor make everything blank.
        public MaterialTool()
        {
            this.matName = "";
            this.toolName = "";
            this.toolNumber = 1;
            this.speed = -1;
            this.feedCut = -1;
            this.feedPlunge = -1;
            this.cutDepth = -1;
            this.toolWidth = -1;
            this.toolLength = 0;
            this.tolerance = 0;
            this.minStep = 0;
            this.shape = EndShape.Ball;
        }
        // Just names.
        public MaterialTool(string Mat, string Tool)
        {
            this.matName = Mat;
            this.toolName = Tool;
            this.toolNumber = 1;
            this.speed = -1;
            this.feedCut = -1;
            this.feedPlunge = -1;
            this.cutDepth = -1;
            this.finishDepth = 0;
            this.toolWidth = -1;
            this.toolLength = 0;
            this.tolerance = 0;
            this.minStep = 0;
            this.shape = EndShape.Ball;
        }
        // Everything, with defaults
        public MaterialTool(string Mat, string Tool, int ToolN, double speed, double feedCut, double feedPlunge, double cutDepth, double finishDepth =0, double width = -1, double tL = 0, EndShape ES = EndShape.Ball,double tol = 0, double mS = 0)
        {
            this.matName = Mat;
            this.toolName = Tool;
            this.toolNumber = ToolN;
            this.speed = speed;
            this.feedCut = feedCut;
            this.feedPlunge = feedPlunge;
            this.finishDepth = finishDepth;
            this.cutDepth = cutDepth;
            this.toolWidth = width;
            this.toolLength = tL;
            this.tolerance = tol;
            this.minStep = mS;
            this.shape = ES;
        }
        // Copy Constructor
        public MaterialTool(MaterialTool MT)
        {
            this.matName = MT.matName;
            this.toolName = MT.toolName;
            this.toolNumber = MT.toolNumber;
            this.speed = MT.speed;
            this.feedCut = MT.feedCut;
            this.feedPlunge = MT.feedPlunge;
            this.cutDepth = MT.cutDepth;
            this.finishDepth = MT.finishDepth;
            this.toolWidth = MT.toolWidth;
            this.toolLength = MT.toolLength;
            this.tolerance = MT.tolerance;
            this.minStep = MT.minStep;
            this.shape = MT.shape;
        }
        // Duplicate
        public ICAMel_Base Duplicate()
        {
            return new MaterialTool(this);
        }

        public string TypeDescription
        {
            get { return "Details of a Material and Tool"; }
        }

        public string TypeName
        {
            get { return "MaterialTool"; }
        }

        public override string ToString()
        {
            string outp = "Tool " + this.toolNumber + " " + this.toolName + " into " + this.matName + " Plunge feed: " + this.feedPlunge.ToString() + " Cut Depth: " + this.cutDepth.ToString();
            return outp;
        }

        public bool IsValid
        {
            get
            {
                return (this.speed > 0 && this.feedCut > 0 && this.feedPlunge > 0 && this.cutDepth > 0
                     && this.finishDepth > 0 && this.toolWidth > 0 && this.toolLength > 0);
            }
        }

        /// <summary>
        /// Offset toolpoint so that it does not gouge an angled path. 
        /// </summary>
        public ToolPoint threeAxisHeightOffset(IMachine M, ToolPoint tP, Vector3d travel, Vector3d orth)
        {
            // TODO at the moment this offset assumes a round end mill.
            // CutOffset does most of the work. 

            Vector3d os = travel;
            os.Unitize();
            os.Transform(Transform.Rotation(Math.PI / 2, orth,new Point3d(0,0,0)));
            double testd = os * M.toolDir(tP);
            if (testd < 0) { os = -1 * os; }

            ToolPoint osTp = new ToolPoint(tP);   

            // move tool so that it cuts at the toolpoint location and does not gouge.
            osTp.Pt = osTp.Pt + this.toolWidth*(os - M.toolDir(tP))/2;
        
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
            switch (this.shape)
            {
                case EndShape.Ball: // find correct position of ball centre and then push to tip. 
                    os = this.toolWidth * (uNorm - uDir) / 2;
                    break;
                case EndShape.Square: // Cut with corner if the angle is greater than .01 radians
                    if(Vector3d.VectorAngle(uDir,uNorm) < .01)
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
    public class GH_MaterialTool : CAMel_Goo<MaterialTool>
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
        public GH_MaterialTool(string Mat, string Tool, int ToolNumber, double speed, double feedCut, double feedPlunge, double cutDepth, double finishDepth = 0, double width = 0, double length = 0, EndShape ES = EndShape.Ball,double tolerance = 0, double minStep = 0)
        {
            this.Value = new MaterialTool(Mat, Tool, ToolNumber, speed, feedCut, feedPlunge, cutDepth, finishDepth, width, length, ES, tolerance, minStep);
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