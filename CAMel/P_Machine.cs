using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace CAMel.Types 
{

    public enum MachineTypes {
        ThreeAxis,
        FiveAxisBCHead,
        FiveAxisACHead
    }

    // Settings for a machine (this is the POST!)
    // This is the only place we handle language
    // So other languages can be used.
    // 
    // TODO This is not the right way to do it. 
    // Should be reimplemented as an interface 
    // with each machine type being a subclass.
    public class Machine : CA_base
    {
        public string name;
        public MachineTypes type;
        public string header;
        public string footer;
        public char CommentChar; 
        public string SpeedChangeCommand;
        public double PathJump; // Max distance allowed between paths in material.
        public string SectionBreak { get; set; }

        // Default Constructor (un-named 3 Axis)
        public Machine()
        {
            this.name = "Unamed Machine";
            this.type = MachineTypes.ThreeAxis;
            this.header = "";
            this.footer = "";
            this.CommentChar = '%';
            this.SectionBreak = "%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%";
            this.SpeedChangeCommand = "M03 ";
            this.PathJump = 2;
        }
        // Just name.
        public Machine(string Name)
        {
            this.name = Name;
            this.type = MachineTypes.ThreeAxis;
            this.header = "";
            this.footer = "";
            this.CommentChar = '%';
            this.SectionBreak = "%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%";
            this.SpeedChangeCommand = "M03 ";
            this.PathJump = 2;
        }
        // All details
        public Machine(string Name, MachineTypes Type, string Header, string Footer)
        {
            this.name = Name;
            this.type = Type;
            this.header = Header;
            this.footer = Footer;
            this.CommentChar = '%';
            this.SectionBreak = "%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%";
            this.SpeedChangeCommand = "M03 ";
            this.PathJump = 2;
        }
        // Copy Constructor
        public Machine(Machine M)
        {
            this.name = M.name;
            this.type = M.type;
            this.header = M.header;
            this.footer = M.footer;
            this.CommentChar = M.CommentChar;
            this.SectionBreak = M.SectionBreak;
            this.SpeedChangeCommand = M.SpeedChangeCommand;
            this.PathJump = M.PathJump;
        }
        // Duplicate
        public Machine Duplicate()
        {
            return new Machine(this);
        }

        public override string TypeDescription
        {
            get { return "Details of a CNC Machine"; }
        }

        public override string TypeName
        {
            get { return "Machine"; }
        }

        public override string ToString()
        {
            return this.type.ToString("g") + " CNC Machine: " + this.name;
        }

        // Real functions

        // Call the correct IK function
        public string InverseKinematics (ToolPoint TP, MaterialTool MT)
        {
            string GPoint = "";
            switch (this.type)
            {
                case MachineTypes.ThreeAxis:
                    GPoint = this.IK_ThreeAxis(TP, MT);
                        break;
                case MachineTypes.FiveAxisBCHead:
                    GPoint = this.IK_FiveAxisBC(TP, MT);
                    break;
                default:
                    throw new System.NotImplementedException("Machine Type has not implemented Inverse Kinematics.");
            }
            return GPoint;
        }
        private string IK_ThreeAxis(ToolPoint TP, MaterialTool MT)
        {
            Point3d OP = TP.Pt;
            string GPoint = "";
            GPoint += "X" + OP.X.ToString("0.000") + " Y" + OP.Y.ToString("0.000") + " Z" + OP.Z.ToString("0.000");

            return GPoint;
        }

        private string IK_FiveAxisBC(ToolPoint TP, MaterialTool MT)
        {
            Point3d Point = TP.Pt;
            Vector3d UV = TP.Dir;
            double Tooltip = MT.toolLength;
            double Bo = Math.Acos(UV.Z);
            double Co = Math.Atan2(UV.Y, UV.X);

            if (Co > Math.PI) Co = Co - 2 * Math.PI;

            Vector3d Offset = new Vector3d();

            Offset = UV * Tooltip;

            Point3d OP = Point + Offset;

            String GPoint = "";
            GPoint += "X" + OP.X.ToString("0.000") + " Y" + OP.Y.ToString("0.000") + " Z" + OP.Z.ToString("0.000") + " ";
            GPoint += "B" + (180 * Bo / Math.PI).ToString("0.000") + " C" + (180 * Co / Math.PI).ToString("0.000");
            GPoint += "% " + "Vector: " + UV.ToString();

            return GPoint;
        }


        // Call the correct Code Writer

        public ToolPoint WriteCode(ref CodeInfo Co, ToolPath TP, ToolPoint beforePoint)
        {
            ToolPoint lastPoint;
            switch (this.type)
            {
                case MachineTypes.ThreeAxis:
                    lastPoint = this.WriteCode_ThreeAxis(ref Co,TP,beforePoint);
                        break;
                case MachineTypes.FiveAxisBCHead:
                    lastPoint = this.WriteCode_FiveAxisBC(ref Co,TP,beforePoint);
                    break;
                default:
                    throw new System.NotImplementedException("Machine Type has not implemented Code Writing");
            }
            return lastPoint;
        }

        // Logic of setting feed:
        //  if 0 use G0 move
        //  if > 0 use value
        //  if < 0 use Material Cut Value

        private ToolPoint WriteCode_ThreeAxis(ref CodeInfo Co, ToolPath TP, ToolPoint beforePoint)
        {
            // 3 Axis is just a list of instructions...
            // We will watch for speed and feed changes.

            // work out initial values of feed. 

            bool FChange = false;
            bool SChange = false;

            double feed;
            double speed;

            if (beforePoint == null) // There were no previous points
            {
                if (TP.Pts.Count > 0)
                {
                    feed = TP.Pts[0].feed;
                    speed = TP.Pts[0].speed;
                    FChange = true;
                    SChange = true;
                }
                else
                {
                    feed = -1;
                    speed = -1;
                }
            }
            else
            {
                feed = beforePoint.feed;
                speed = beforePoint.speed;
            }

            if (feed < 0) feed = TP.MatTool.feedCut;
            if (speed < 0) speed = TP.MatTool.speed;
            string PtCode;

            foreach(ToolPoint Pt in TP.Pts)
            {
                if(Pt.localCode != "")
                    Co.Append(Pt.localCode);
                foreach(string err in Pt.error)
                {
                    Co.AddError(err);
                    Co.AppendLine(this.CommentChar + err);
                }
                foreach (string warn in Pt.warning)
                {
                    Co.AddWarning(warn);
                    Co.AppendLine(this.CommentChar + warn);
                }
                    

                // Establish new feed value
                if(Pt.feed != feed)
                {
                    if(Pt.feed >= 0 )
                    {
                        FChange = true;
                        feed = Pt.feed;
                    } 
                    else if(feed != TP.MatTool.feedCut) // Default to the cut feed rate.
                    {
                        FChange = true;
                        feed = TP.MatTool.feedCut;
                    }
                }

                // Establish new speed value
                if (Pt.speed != speed)
                {
                    if (Pt.speed > 0)
                    {
                        SChange = true;
                        speed = Pt.speed;
                    }
                }

                // Add the position information
                PtCode =  IK_ThreeAxis(Pt, TP.MatTool);

                // Act if feed has changed
                if (FChange)
                {
                    if (feed == 0)
                        PtCode = "G00 " + PtCode;
                    else
                        PtCode = "G01 " + PtCode + " F" + feed.ToString("0");
                }
                FChange = false;

                // Act if speed has changed
                if (SChange)
                {
                    PtCode = this.SpeedChangeCommand + " S" + speed.ToString("0") + "\n" + PtCode;
                }
                SChange = false;

                if(Pt.name != "")
                {
                    PtCode = PtCode + "% " + Pt.name;
                }
                Co.AppendLine(PtCode);

                // Adjust ranges

                Co.GrowRange("X", Pt.Pt.X);
                Co.GrowRange("Y", Pt.Pt.Y);
                Co.GrowRange("Z", Pt.Pt.Z);

            }

            // return the last point or the beforePoint if the path had no elements
            ToolPoint PtOut;

            if(TP.Pts.Count > 0)
            {
                PtOut = new ToolPoint(TP.Pts[TP.Pts.Count-1]);
                PtOut.feed = feed;
                PtOut.speed = speed;
            }
            else PtOut = beforePoint;

            return PtOut;

        }

        private ToolPoint WriteCode_FiveAxisBC(ref CodeInfo Co, ToolPath TP, ToolPoint beforePoint)
        {
            //TODO: 5-Axis WriteCode
            throw new NotImplementedException("Cannot write 5-Axis Code yet. ");
        }

        public Vector3d ToolDir(ToolPoint TP)
        {
            switch (this.type)
            {
                case MachineTypes.ThreeAxis:
                    return Vector3d.ZAxis;
                    case MachineTypes.FiveAxisBCHead:
                    return TP.Dir;
                case MachineTypes.FiveAxisACHead:
                    return TP.Dir;
                default:
                    throw new System.NotImplementedException("Machine Type has not implemented Tool Direction.");
            }
        }

        public ToolPath ReadCode(string Code)
        {
            ToolPath TP = new ToolPath();

            double X=0, Y = 0, Z = 0, F = -1, S = -1;
            bool changed, found, Fchanged, feedfound, Schanged, speedfound;

            System.Text.StringBuilder OutputCode = new System.Text.StringBuilder();

            string Xpattern = @".*X([0-9\-.]+).*";
            string Ypattern = @".*Y([0-9\-.]+).*";
            string Zpattern = @".*Z([0-9\-.]+).*";
            string Fpattern = @".*F([0-9\-.]+).*";
            string Spattern = @".*S([0-9\-.]+).*";
            string G0pattern = @"G0.*";
            string LinePattern = @".*";

            System.Text.RegularExpressions.MatchCollection Lines;

            Lines = System.Text.RegularExpressions.Regex.Matches(Code, LinePattern);

            int i = 0;

            foreach( System.Text.RegularExpressions.Match line in Lines )
            {
                changed = false;
                Fchanged = false;
                Schanged = false;
                found = false;
                feedfound = false;
                speedfound = false;

                X = GetValue(line.ToString(), Xpattern, X, ref found, ref changed);
                Y = GetValue(line.ToString(), Ypattern, Y, ref found, ref changed);
                Z = GetValue(line.ToString(), Zpattern, Z, ref found, ref changed);
                F = GetValue(line.ToString(), Fpattern, F, ref feedfound, ref Fchanged);
                S = GetValue(line.ToString(), Spattern, S, ref speedfound, ref Schanged);

                //interpret a G0 command.
                if( System.Text.RegularExpressions.Regex.IsMatch(line.ToString(), G0pattern))
                {
                    feedfound = true;
                    if( F != 0 )
                    {
                        Fchanged = true;
                        F = 0;
                    }
                }

                // If A, C, X, Y or Z changed or Fchanged in a line containing a coordinate
                // add a new point. 
                if( changed || (found && Fchanged)) 
                {
                    TP.Pts.Add(new ToolPoint(new Point3d(X,Y,Z),new Vector3d(0,0,0),S,F));
                    i++;
                }
            }

            return TP;
        }

          
        private double GetValue(string line, string pattern, double old, ref bool found, ref bool changed)
        {
            double val = old;
            string monkey;
            if ( System.Text.RegularExpressions.Regex.IsMatch(line, pattern) ) {
                monkey = System.Text.RegularExpressions.Regex.Replace(line, pattern, "$1");
                val = Convert.ToDouble(monkey);
                found = true;
                if (val != old) changed = true;
            }
            return val;
        }

        // Give the machine position somewhere on the move from one toolPoint to another.
        public ToolPoint Interpolate(ToolPoint toolPoint1, ToolPoint toolPoint2, double par)
        {
            ToolPoint TPo = new ToolPoint(toolPoint1);
            switch (this.type)
            {
                case MachineTypes.ThreeAxis:
                    TPo.Pt = toolPoint1.Pt * par + toolPoint2.Pt * (1 - par);
                    break;
                case MachineTypes.FiveAxisBCHead:
                    TPo.Pt = toolPoint1.Pt * par + toolPoint2.Pt * (1 - par);

                    double B1 = Math.Acos(toolPoint1.Dir.Z);
                    double C1 = Math.Atan2(toolPoint1.Dir.Y, toolPoint1.Dir.X);
                    double B2 = Math.Acos(toolPoint2.Dir.Z);
                    double C2 = Math.Atan2(toolPoint2.Dir.Y, toolPoint2.Dir.X);

                    double Bo = B1 * par + B2 * (1 - par);
                    double Co = C1 * par + C2 * (1 - par);

                    TPo.Dir = new Vector3d(Math.Sin(Bo)*Math.Cos(Co), Math.Sin(Bo)*Math.Sin(Co), Math.Cos(Bo));
                    break;
                default:
                    throw new NotImplementedException("Machine Type has not implemented point interpolation.");
            }
            return TPo;
        }
        // Move from one path to another.
        // Tool changers could also be handled here
        // Assume that both points are in safe space
        // TODO get rid of SafeMove wrapper in MaterialForm it is just confusing. This should be called directly.
        public ToolPath SafeMove(ToolPath TPfrom, ToolPath TPto)
        {
            ToolPath Move = TPto.copyWithNewPoints(new List<ToolPoint>());
            Move.name = "";

            // Check ends are safe, or throw error
            // If the end is safe in one that is good enough.
            // Give a little wiggle as we just pull back to the safe distance.
            if ((TPfrom.MatForm.SafePoint(TPfrom.Pts[TPfrom.Pts.Count - 1]) < -0.001 && TPto.MatForm.SafePoint(TPfrom.Pts[TPfrom.Pts.Count - 1]) < -0.001)
                || (TPfrom.MatForm.SafePoint(TPto.Pts[0]) < -0.001 && TPto.MatForm.SafePoint(TPto.Pts[0]) < -0.001))
            {
                throw new ArgumentException("End points of a safe move are not in safe space.");
            }

            switch (type)
            {
                case MachineTypes.ThreeAxis:
                    // Start with a straight line, see how close it 
                    // comes to danger. If its too close add a new
                    // point and try again.

                    List<Point3d> route = new List<Point3d>();
                    route.Add(TPfrom.Pts[TPfrom.Pts.Count - 1].Pt);
                    route.Add(TPto.Pts[0].Pt);

                    Vector3d away;
                    Point3d cPt;
                    int i;
                    double dist = TPfrom.MatForm.closestDanger( route, TPto.MatForm, out cPt, out away, out i);
                    double safeD = Math.Max(TPfrom.MatForm.safeDistance, TPto.MatForm.safeDistance);

                    double dS,dE;
                    Point3d pS, pE;

                    // loop through adding points at problem places until we have 
                    // everything sorted!
                    // Warning this could race, it shouldn't though.

                    while(dist < safeD)
                    {
                        // add a new point to stay clear of danger
                        // DOC: How we find the new point so we do not get into an infinite loop
                        // Avoid a sphere of correct radius around cPt
                        // 
                        // Work in plane given by CPt, away and each end point of our path
                        // can now just avoid a circle as CPt is the center of the sphere

                        pS = this.missSphere(route[i], cPt, away, safeD, out dS);
                        pE = this.missSphere(route[i+1], cPt, away, safeD, out dE);

                        // add the point that is further along the vector away
                        // it might make more sense to add the closer one, this loop 
                        // will run more, the toolpath will be shorter but have 
                        // more points
                        if (dS > dE)
                            route.Insert(i + 1, pS);
                        else
                            route.Insert(i+1,pE);

                        dist = TPfrom.MatForm.closestDanger( route, TPto.MatForm, out cPt, out away, out i);
                    }

                    // get rid of start and end points that are already in the paths
                    route.RemoveAt(0);
                    route.RemoveAt(route.Count - 1);

                    foreach(Point3d Pt in route)
                    {
                        // add new point at speed 0 to describe rapid move.
                        Move.Pts.Add(new ToolPoint(Pt,new Vector3d(0,0,0),"",-1,0));
                    }

                    break;
                case MachineTypes.FiveAxisBCHead:
                    //TODO: 5-Axis SafeMove
                    // The method above will work, but we need to add 
                    // the length of the tool to the safe distance, and
                    // use the location of the pivot not the toolpoint
                    throw new System.NotImplementedException("Safe moves for five axis machines have not yet been implemented");
                default:
                    throw new System.NotImplementedException("Machine Type has not implemented safe moves between paths");
            }

            return Move;
        }

        private Point3d missSphere(Point3d pPt, Point3d cPt, Vector3d away, double safeD, out double d)
        {
            // find the tangent line from pPt to the sphere centered 
            // at cPt with radius safeD. The intersect it with the line 
            // of direction away from cPt.
            Vector3d pToc = (Vector3d) (pPt - cPt);

            // angle between the sphere normal at the tangent point and 
            // the vector away
            double angle = Vector3d.VectorAngle(away, pToc) - Math.Acos(safeD / pToc.Length);

            // distance we need to go along away
            d = safeD / Math.Cos(angle);

            // intersection point
            return cPt + d * away;
        }

    }

    // Grasshopper Type Wrapper
    public class GH_Machine : CA_Goo<Machine>
    {
        // Default constructor
        public GH_Machine()
        {
            this.Value = new Machine();
        }
        // Just name.
        public GH_Machine(string Name)
        {
            this.Value = new Machine(Name);
        }
        // All details
        public GH_Machine(string Name, MachineTypes Type, string Header, string Footer)
        {
            this.Value = new Machine(Name, Type, Header, Footer);
        }
        // Unwrapped type
        public GH_Machine(Machine M)
        {
            this.Value = new Machine(M);
        }
        // Copy Constructor
        public GH_Machine(GH_Machine M)
        {
            this.Value = new Machine(M.Value);
        }
        // Duplicate
        public override IGH_Goo Duplicate()
        {
            return new GH_Machine(this);
        }

        public override bool CastTo<Q>( ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(Machine)))
            {
                object ptr = this.Value;
                target = (Q)ptr;
                return true;
            }
            return false;
        }

        public override bool CastFrom( object source )
        {
            if (source == null)
            {
                return false;
            }
            if (source is Machine)
            {
                this.Value = new Machine((Machine)source);
                return true;
            }
            return false;
        }
    }

    // Grasshopper Parameter Wrapper
    public class GH_MachinePar : GH_Param<GH_Machine>
    {
        public GH_MachinePar() : 
            base("Machine","Machine","Contains a collection of information on CNC machines","CAMel","  Params",GH_ParamAccess.item) {}
        public override Guid ComponentGuid
        {
            get { return new Guid("df6dcfa2-510e-4613-bdae-3685b094e7d7"); }
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
                return Properties.Resources.machine;
            }
        }
    }

}