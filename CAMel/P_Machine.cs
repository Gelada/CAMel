using System;
using System.Collections.Generic;
using System.Text;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using CAMel.Types.MaterialForm;
using System.Text.RegularExpressions;

namespace CAMel.Types 
{

    public enum MachineTypes {
        ThreeAxis,
        FiveAxisBCHead,
        FiveAxisACHead,
        PocketNC
    }

    // Settings for a machine (this is the POST!)
    // This is the only place we handle language
    // So other languages can be used.
    // 
    // TODO This is not the right way to do it. 
    //  Should be reimplemented as an interface 
    //  with each machine type being a subclass.
    // TODO create a machine state class for each machine
    //  currently using CodeInfo to store a dictionary of values. 
    //  a bespoke version for each machine type would be better

    public class Machine : ICAMel_Base
    {
        public string name { get; set; }
        public MachineTypes type { get; set; }
        public string filestart { get; set; }
        public string header { get; set; }
        public string footer { get; set; }
        public string fileend { get; set; }
        public string CommentChar { get; set; }
        public string endCommentChar { get; set; }
        public string SpeedChangeCommand { get; set; }
        public double PathJump { get; set; } // Max distance allowed between paths in material.
        public string SectionBreak { get; set; }
        public Vector3d Pivot { get; set; }
        // TODO replace this flag with separate machine type for 2d vs 3d.
        // Really need to refactor to subclass machine types 
        public bool dim2 { get; internal set; } // True if machine is 2d
        public double leads { get; internal set; } // Apply lead in and out paths. 

        // Default Constructor (un-named 3 Axis)
        public Machine()
        {
            this.name = "Unamed Machine";
            this.type = MachineTypes.ThreeAxis;
            this.header = "";
            this.footer = "";
            this.filestart = "";
            this.fileend = "";
            this.CommentChar = "(";
            this.endCommentChar = ")";
            this.SectionBreak = "------------------------------------------";
            this.SpeedChangeCommand = "M03 ";
            this.PathJump = 2;
            this.Pivot = Vector3d.Zero;
            this.dim2 = false;
            this.leads = 0;
            this.InsertCode = "";
            this.RetractCode = "";
        }
        // Just name.
        public Machine(string Name)
        {
            this.name = Name;
            this.type = MachineTypes.ThreeAxis;
            this.header = "";
            this.footer = "";
            this.filestart = "";
            this.fileend = "";
            this.CommentChar = "(";
            this.endCommentChar = ")";
            this.SectionBreak = "------------------------------------------";
            this.SpeedChangeCommand = "M03 ";
            this.PathJump = 2;
            this.Pivot = Vector3d.Zero;
            this.dim2 = false;
            this.leads = 0;
            this.InsertCode = "";
            this.RetractCode = "";
        }
        // All details
        public Machine(string Name, MachineTypes Type, string Header, string Footer, string filestart, string fileend)
        {
            this.name = Name;
            this.type = Type;
            this.header = Header;
            this.footer = Footer;
            this.filestart = filestart;
            this.fileend = fileend;
            this.CommentChar = "(";
            this.endCommentChar = ")";
            this.SectionBreak = "------------------------------------------";
            this.SpeedChangeCommand = "M03 ";
            this.PathJump = 2;
            this.Pivot = Vector3d.Zero;
            this.dim2 = false;
            this.leads = 0;
            this.InsertCode = "";
            this.RetractCode = "";
        }

        // Copy Constructor
        public Machine(Machine M)
        {
            this.name = M.name;
            this.type = M.type;
            this.header = M.header;
            this.footer = M.footer;
            this.filestart = M.filestart;
            this.fileend = M.fileend;
            this.CommentChar = M.CommentChar;
            this.endCommentChar = M.endCommentChar;
            this.SectionBreak = M.SectionBreak;
            this.SpeedChangeCommand = M.SpeedChangeCommand;
            this.PathJump = M.PathJump;
            this.Pivot = M.Pivot;
            this.dim2 = M.dim2;
            this.leads = M.leads;
            this.InsertCode = M.InsertCode;
            this.RetractCode = M.RetractCode;
        }
        // Duplicate
        public Machine Duplicate()
        {
            return new Machine(this);
        }

        public string TypeDescription
        {
            get { return "Details of a CNC Machine"; }
        }

        public string TypeName
        {
            get { return "Machine"; }
        }

        public bool IsValid
        {
            get
            {
                throw new NotImplementedException("Machine has not yet implemented IsValid");
            }
        }

        

        public string InsertCode { get; internal set; } // Code to place before insert
        public string RetractCode { get; internal set; } // Code to place after retract

        public override string ToString()
        {
            return this.type.ToString("g") + " CNC Machine: " + this.name;
        }

        // Real functions
        // TODO Bring PocketNC into this!
        // Call the correct IK function
        public string InverseKinematics (ToolPoint TP, MaterialTool MT)
        {
            string GPoint = "";
            switch (this.type)
            {
                case MachineTypes.ThreeAxis:
                    GPoint = Machine.IK_ThreeAxis(TP, MT);
                        break;
                default:
                    throw new System.NotImplementedException("Machine Type has not implemented Inverse Kinematics.");
            }
            return GPoint;
        }
        static private string IK_ThreeAxis(ToolPoint TP, MaterialTool MT)
        {
            Point3d OP = TP.Pt;
            string GPoint = "";
            GPoint += "X" + OP.X.ToString("0.000") + " Y" + OP.Y.ToString("0.000") + " Z" + OP.Z.ToString("0.000");

            return GPoint;
        }
        static private string IK_TwoAxis(ToolPoint TP, MaterialTool MT)
        {
            Point3d OP = TP.Pt;
            string GPoint = "";
            GPoint += "X" + OP.X.ToString("0.000") + " Y" + OP.Y.ToString("0.000");

            return GPoint;
        }

        static private string IK_FiveAxisBC(ToolPoint TP, MaterialTool MT)
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

            return GPoint;
        }

        private string IK_PocketNC(ToolPoint TP, MaterialTool MT, Vector3d AB, ref Point3d machinePt)
        {
            Point3d OP = TP.Pt;

            // rotate from material orientation to machine orientation
            OP.Transform(Transform.Rotation(AB.Y, Vector3d.YAxis, Point3d.Origin));
            OP.Transform(Transform.Rotation(AB.X, Vector3d.XAxis, Point3d.Origin));

            // translate from origin at pivot to origin set so that the tooltip is at machine origin
            OP = OP - this.Pivot + Vector3d.ZAxis * MT.toolLength;
            //HACK!
            //if (OP.Z > 0.02) OP.Z = 0.02;
            //if (OP.X < -2.0) OP.X = -2.0;
            //if (OP.X > 2.4) OP.X = 2.4;

            StringBuilder GPtBd = new StringBuilder(@"X",34);
            GPtBd.Append(OP.X.ToString("0.000"));
            GPtBd.Append(@" Y"); GPtBd.Append(OP.Y.ToString("0.000"));
            GPtBd.Append(@" Z"); GPtBd.Append(OP.Z.ToString("0.000"));
            GPtBd.Append(@" A"); GPtBd.Append((180 * AB.X / Math.PI).ToString("0.000"));
            GPtBd.Append(@" B"); GPtBd.Append((180 * AB.Y / Math.PI).ToString("0.000"));

            machinePt = OP;
            return GPtBd.ToString();
        }
        static private string IK_PocketNC_orient(MaterialTool materialTool, Vector3d AB)
        {
            String GPoint = "";
            GPoint += "A" + (180 * AB.X / Math.PI).ToString("0.000") + " B" + (180 * AB.Y / Math.PI).ToString("0.000");

            return GPoint;
        }

        // Always gives B from -pi to pi and A from -pi/2 to pi/2.
        static private Vector3d Orient_FiveAxisABP(Vector3d UV)
        {
            double Ao = Math.Asin(-UV.Y);
            double Bo = Math.Atan2(UV.X, -UV.Z);

            if (Ao > Math.PI/2.0)
            {
                Ao = Math.PI-Ao;
                Bo = Bo - Math.PI;
                if (Bo < 0) Bo = Bo + 2.0 * Math.PI;
            }

            if (Ao < -Math.PI/2.0)
            {
                Ao = Math.PI - Ao;
                Bo = Bo - Math.PI;
                if (Bo < 0) Bo = Bo + 2.0 * Math.PI;
            }

            return new Vector3d(Ao, Bo,0);
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
                case MachineTypes.PocketNC:
                    lastPoint = this.WriteCode_PocketNC(ref Co,TP,beforePoint);
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

        // TODO replace beforePoint with a machine state. 
        // In general need to add in a strong concept of machine state. 
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
                if (TP.Count > 0)
                {
                    if (TP[0].feed >= 0) { feed = TP[0].feed; }
                    else { feed = TP.MatTool.feedCut; }
                    if (TP[0].speed >= 0) { speed = TP[0].speed; }
                    else { speed = TP.MatTool.speed; }

                    // Only call Feed/speed if non-negative 
                    // so Material Tool can have -1 for speed/feed and ignore them
                    if (feed >= 0) { FChange = true; }
                    if (speed >= 0) { SChange = true; }
                }
                else
                {
                    feed = TP.MatTool.feedCut;
                    speed = TP.MatTool.speed;
                }
            }
            else
            {
                feed = beforePoint.feed;
                speed = beforePoint.speed;
            }
            
            string PtCode;

            foreach(ToolPoint Pt in TP)
            {
                if (Pt.error != null)
                {
                    foreach (string err in Pt.error)
                    {
                        Co.AddError(err);
                        Co.AppendComment(err);
                    }
                }
                if (Pt.warning != null)
                {
                    foreach (string warn in Pt.warning)
                    {
                        Co.AddWarning(warn);
                        Co.AppendComment(warn);
                    }
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
                if (!dim2) { PtCode = IK_ThreeAxis(Pt, TP.MatTool); }
                else { PtCode = IK_TwoAxis(Pt, TP.MatTool); }

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

                PtCode = Pt.preCode + PtCode + Pt.postCode;

                if(Pt.name != "")
                {
                    PtCode = PtCode + " " + this.CommentChar + Pt.name + this.endCommentChar;
                }
                Co.Append(PtCode);

                // Adjust ranges

                Co.GrowRange("X", Pt.Pt.X);
                Co.GrowRange("Y", Pt.Pt.Y);
                Co.GrowRange("Z", Pt.Pt.Z);

            }

            // return the last point or the beforePoint if the path had no elements
            ToolPoint PtOut;

            if(TP.Count > 0)
            {
                PtOut = new ToolPoint(TP[TP.Count-1]);
                PtOut.feed = feed;
                PtOut.speed = speed;
            }
            else PtOut = beforePoint;

            return PtOut;

        }

        private ToolPoint WriteCode_PocketNC(ref CodeInfo Co, ToolPath TP, ToolPoint beforePoint)
        {
            double AngleAcc = 0.0001; // accuracy of angles to assume we lie on the cusp.

            // We will watch for speed and feed changes.
            // We will adjust A and B as best as possible and otherwise throw errors.
            // Manual unwinding Grrrr!

            // work out initial values of feed. 

            bool FChange = false;
            bool SChange = false;

            double feed;
            double speed;
            Vector3d AB,newAB;
            double Bto = 0;  // Allow for smooth adjustment through the cusp with A at 90.
            int Bsteps = 0;  //
            string PtCode;
           
            if (beforePoint == null) // There were no previous points
            {
                if (TP.Count > 0)
                {
                    feed = TP[0].feed;
                    speed = TP[0].speed;
                    if (feed < 0) { feed = TP.MatTool.feedCut; }
                    if (speed < 0) { speed = TP.MatTool.speed; }
                    AB = Machine.Orient_FiveAxisABP(TP[0].Dir);
                    FChange = true;
                    SChange = false;
                    // making the first move. Orient the tool first

                    PtCode = IK_PocketNC_orient(TP.MatTool, AB);
                    PtCode = "G00 " + PtCode; 
                    PtCode = this.SpeedChangeCommand + " S" + speed.ToString("0") + "\n" + PtCode;
                    Co.Append(PtCode);
                }
                else
                {
                    feed = -1;
                    speed = -1;
                    AB = new Vector3d(Math.PI/2.0, 0,0);
                }
            }
            else
            {
                feed = beforePoint.feed;
                speed = beforePoint.speed;
                AB = new Vector3d(Co.MachineState["A"], Co.MachineState["B"],0);
            }

            if (feed < 0) { feed = TP.MatTool.feedCut; }
            if (speed < 0) { speed = TP.MatTool.speed; }
            
            int i,j;
            ToolPoint Pt;
            Point3d MachPos = new Point3d(0,0,0);
            for(i=0;i<TP.Count;i++)
            {
                Pt = TP[i];

                if (Pt.error != null)
                {
                    foreach (string err in Pt.error) { Co.AddError(err);}
                }
                if (Pt.warning != null)
                {
                    foreach (string warn in Pt.warning) { Co.AddWarning(warn); }
                }

                // Establish new feed value
                if (Pt.feed != feed)
                {
                    if (Pt.feed >= 0)
                    {
                        FChange = true;
                        feed = Pt.feed;
                    }
                    else if (feed != TP.MatTool.feedCut) // Default to the cut feed rate.
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

                // Work on tool orientation

                // get naive orientation
                newAB = Orient_FiveAxisABP(Pt.Dir);

                // adjust B to correct period
                newAB.Y = newAB.Y + 2.0 * Math.PI * Math.Round((AB.Y-newAB.Y)/(2.0*Math.PI));

                // set A to 90 if it is close (to avoid a lot of messing with B for no real reason)

                if (Math.Abs(newAB.X - Math.PI) < AngleAcc) newAB.X = Math.PI/2.0;

                // take advantage of the small double stance for A between 85 and 90 degrees

                if (newAB.X > (Math.PI / 2.0 - Math.PI / 36.0))
                    if ((newAB.Y - AB.Y) > Math.PI)
                    {
                        newAB.X = Math.PI - newAB.X; 
                        newAB.Y = newAB.Y - Math.PI;
                    } else if ((newAB.Y - AB.Y) < -Math.PI)
                    {
                        newAB.X = Math.PI - newAB.X; 
                        newAB.Y = newAB.Y + Math.PI;
                    }

                // adjust through cusp

                if ( newAB.X == Math.PI/2.0) // already set if nearly there. 
                {
                    // detect that we are already moving
                    if(Bsteps > 0)
                    {
                        newAB.Y = AB.Y + (Bto-AB.Y) / Bsteps;
                        Bsteps--;
                    }
                    else // head forward to next non-vertical point or the end. 
                    {
                        j = i+1;

                        while (j < (TP.Count-1) && Math.Abs(Orient_FiveAxisABP(TP[j].Dir).X - Math.PI/2.0) < AngleAcc) j++;

                        // If we are at the start of a path and vertical then we can just use the first non-vertical 
                        // position for the whole run. 
                        if (Math.Abs(AB.X - Math.PI / 2.0) < AngleAcc) 
                        {
                            Bto = Orient_FiveAxisABP(TP[j].Dir).Y;
                            Bsteps = j - i;
                            newAB.Y = Bto;
                        }
                        // if we get to the end and it is still vertical we do not need to rotate.
                        else if (Math.Abs(Orient_FiveAxisABP(TP[j].Dir).X) < AngleAcc)
                        {
                            Bto = AB.X;
                            Bsteps = j - i;
                            newAB.Y = Bto;
                        }
                        else
                        {
                            Bto = Orient_FiveAxisABP(TP[j].Dir).Y;
                            Bsteps = j - i;
                            newAB.Y = AB.Y;
                        }
                    }
                }

                // (throw bounds error if B goes past +-999999 degrees or A is not between -5 and 95)

                if (Math.Abs(180.0*newAB.Y/(Math.PI)) > 999999)
                {
                    Co.AddError("Out of bounds on B");
                }
                if ((180.0 * newAB.X / Math.PI > 95) || (180.0 * newAB.X / Math.PI < -5))
                {
                    Co.AddError("Out of bounds on A");
                }
                
                // update AB value

                AB = newAB;

                // Add the position information

                PtCode = IK_PocketNC(Pt, TP.MatTool,AB, ref MachPos);

                // Act if feed has changed
                if (FChange)
                {
                    if (feed == 0)
                        PtCode = "G00 " + PtCode;
                    else
                        PtCode = "G01 " + PtCode + " F" + feed.ToString("0.00");
                }
                FChange = false;

                // Act if speed has changed
                if (SChange)
                {
                    PtCode = this.SpeedChangeCommand + " S" + speed.ToString("0") + "\n" + PtCode;
                }
                SChange = false;

                PtCode = Pt.preCode + PtCode + Pt.postCode;


                if (Pt.name != "")
                {
                    PtCode = PtCode + this.CommentChar + Pt.name + this.endCommentChar;
                }
                
                Co.Append(PtCode);
                // Adjust ranges

                Co.GrowRange("X", MachPos.X);
                Co.GrowRange("Y", MachPos.Y);
                Co.GrowRange("Z", MachPos.Z);
                Co.GrowRange("A", AB.X);
                Co.GrowRange("B", AB.Y);
            }

            // return the last point or the beforePoint if the path had no elements
            ToolPoint PtOut;

            if (TP.Count > 0)
            {
                PtOut = new ToolPoint(TP[TP.Count - 1]);
                PtOut.feed = feed;
                PtOut.speed = speed;

                // Pass machine state information

                Co.MachineState.Clear();
                Co.MachineState.Add("X", MachPos.X);
                Co.MachineState.Add("Y", MachPos.Y);
                Co.MachineState.Add("Z", MachPos.Z);
                Co.MachineState.Add("A", AB.X);
                Co.MachineState.Add("B", AB.Y);
            }
            else { PtOut = beforePoint; }

            return PtOut;
        }

        // Assumes XY machine
        internal static ToolPath LeadInOut(ToolPath TP, double lead)
        {
            double leadLen = lead * TP.Additions.leadFactor;
            ToolPath newTP = new ToolPath(TP);
            PolylineCurve toolL = TP.GetLine();

            // Find the point on a circle furthest from the toolpath. 
            int testNumber = 50;
            Point3d LeadStart = new Point3d(), testPt;
            double testdist, dist = -1;
            bool noInter, correctSide;
            for(int i = 0; i<testNumber; i++)
            {
                double ang = 2.0 * Math.PI * i / (double)testNumber;
                testPt = TP[0].Pt+leadLen*new Point3d(Math.Cos(ang), Math.Sin(ang), 0);

                // Check point is inside (or outside) the curve
                correctSide = toolL.Contains(testPt) == PointContainment.Inside;
                if (leadLen > 0) { correctSide = !correctSide; }

                // if on the correct side find the distance to the curve and 
                // update the point if there is a line from point to curve that
                // does not hit material.
                if (correctSide)
                {
                    toolL.ClosestPoint(testPt, out testdist);
                    testdist = testPt.DistanceTo(toolL.PointAt(testdist));
                    noInter = Intersection.CurveCurve(toolL, new Line(TP[0].Pt, testPt).ToNurbsCurve(), 0.00001, 0.00001).Count <= 1;

                    if (noInter && testdist > dist)
                    {
                        dist = testdist;
                        LeadStart = testPt;
                    }
                }
            }
            // If no suitable point found throw an error, otherwise add point to 
            // start and end
            if(dist < 0)
            {
                newTP[0].AddError("No suitable point for lead in and out found.");
            }
            else
            {
                ToolPoint LeadTP = new ToolPoint(TP[0]);
                LeadTP.Pt = LeadStart;
                newTP.Add(new ToolPoint(LeadTP));
                LeadTP.feed = 0;
                newTP.Insert(0, LeadTP);
            }

            newTP.Additions.leadFactor = 0;
            return newTP;
        }

        internal ToolPath InsertRetract(ToolPath TP)
        {
            ToolPath newPath;
            if (this.dim2) {
                // lead in and out called here
                newPath = Machine.LeadInOut(TP, this.leads);
                newPath.Additions.insert = false;
                newPath.Additions.retract = false;
            }
            else { newPath = TP.MatForm.InsertRetract(TP); }

            if ( TP.Additions.insert && this.InsertCode != "" ) { newPath.preCode = newPath.preCode + "\n" + this.InsertCode; }
            if (TP.Additions.retract && this.RetractCode != "") { newPath.postCode = newPath.postCode + "\n" + this.RetractCode; }
            newPath.Additions.leadFactor = 0;
            return newPath;
        }

        public Vector3d ToolDir(ToolPoint TP)
        {
            switch (this.type)
            {
                case MachineTypes.ThreeAxis:
                    return -Vector3d.ZAxis;
                case MachineTypes.FiveAxisBCHead:
                    return TP.Dir;
                case MachineTypes.FiveAxisACHead:
                    return TP.Dir;
                case MachineTypes.PocketNC:
                    return TP.Dir;
                default:
                    throw new System.NotImplementedException("Machine Type has not implemented Tool Direction.");
            }
        }

        public ToolPath ReadCode(string Code)
        {
            switch (this.type)
            {
                case MachineTypes.ThreeAxis:
                    return ReadCode_ThreeAxis(Code);
                case MachineTypes.PocketNC:
                    return ReadCode_PocketNC(Code);
                default:
                    throw new System.NotImplementedException("Machine Type has not implemented ReadCode.");
            }
        }

        private static Regex numbPattern = new Regex(@"^([0-9\-.]+).*", RegexOptions.Compiled);

        private ToolPath ReadCode_PocketNC(string Code)
        {
            ToolPath TP = new ToolPath();

            double toolLength = 1;

            double X = 0, Y = 0, Z = 0, A = 0, B = 0, F = -1, S = -1;
            bool changed, found, Fchanged, feedfound, Schanged, speedfound;

            char[] seps = { '\n', '\r' };
            String[] Lines = Code.Split(seps,StringSplitOptions.RemoveEmptyEntries);

            foreach (String line in Lines)
            {
                changed = false;
                Fchanged = false;
                Schanged = false;
                found = false;
                feedfound = false;
                speedfound = false;

                X = GetValue(line, 'X', X, ref found, ref changed);
                Y = GetValue(line, 'Y', Y, ref found, ref changed);
                Z = GetValue(line, 'Z', Z, ref found, ref changed);
                A = GetValue(line, 'A', A, ref found, ref changed);
                B = GetValue(line, 'B', B, ref found, ref changed);
                F = GetValue(line, 'F', F, ref feedfound, ref Fchanged);
                S = GetValue(line, 'S', S, ref speedfound, ref Schanged);

                //interpret a G0 command.
                if (line.Contains(@"G00") || line.ToString().Contains(@"G0 ") )
                {
                    feedfound = true;
                    if (F != 0)
                    {
                        Fchanged = true;
                        F = 0;
                    }
                }

                // If A, B, X, Y or Z changed or Fchanged in a line containing a coordinate
                // add a new point. 
                if (changed || (found && Fchanged))
                {
                    TP.Add(ReadTP_PocketNC(X,Y,Z,A,B,F,S,toolLength));
                }

            }

            return TP;
        }

        private ToolPoint ReadTP_PocketNC(double X, double Y, double Z, double A, double B, double F, double S,double toolLength)
        {
            Point3d OP = new Point3d(X,Y,Z);
            // translate from the tooltip at machine origin origin to pivot at origin
            OP = OP + this.Pivot - Vector3d.ZAxis * toolLength;

            // rotate from machine orientation to material orientation
            OP.Transform(Transform.Rotation(-Math.PI*A/180.0, Vector3d.XAxis, Point3d.Origin));
            OP.Transform(Transform.Rotation(-Math.PI*B/180.0, Vector3d.YAxis, Point3d.Origin));

            Vector3d Dir = Vector3d.ZAxis;
            // rotate from machine orientation to material orientation
            Dir.Transform(Transform.Rotation(-Math.PI*A/180.0, Vector3d.XAxis, Point3d.Origin));
            Dir.Transform(Transform.Rotation(-Math.PI*B/180.0, Vector3d.YAxis, Point3d.Origin));

            return new ToolPoint(OP, -Dir, S, F);
        }

        private ToolPath ReadCode_ThreeAxis(string Code)
        {
            ToolPath TP = new ToolPath();

            double X=0, Y = 0, Z = 0, F = -1, S = -1;
            bool changed, found, Fchanged, feedfound, Schanged, speedfound;

            char[] seps = { '\n', '\r' };
            String[] Lines = Code.Split(seps, StringSplitOptions.RemoveEmptyEntries);

            int i = 0;

            foreach(String line in Lines )
            {
                changed = false;
                Fchanged = false;
                Schanged = false;
                found = false;
                feedfound = false;
                speedfound = false;

                X = GetValue(line.ToString(), 'X', X, ref found, ref changed);
                Y = GetValue(line.ToString(), 'Y', Y, ref found, ref changed);
                Z = GetValue(line.ToString(), 'Z', Z, ref found, ref changed);
                F = GetValue(line.ToString(), 'F', F, ref feedfound, ref Fchanged);
                S = GetValue(line.ToString(), 'S', S, ref speedfound, ref Schanged);

                //interpret a G0 command.

                if (line.Contains(@"G00") || line.ToString().Contains(@"G0 "))
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
                    TP.Add(new ToolPoint(new Point3d(X,Y,Z),new Vector3d(0,0,0),S,F));
                    i++;
                }
            }

            return TP;
        }
         
        static private double GetValue(string line, char split, double old, ref bool found, ref bool changed)
        {
            double val = old;
            string[] splitLine = line.Split(split);
            if (splitLine.Length > 1 && numbPattern.IsMatch(splitLine[1]))
            {
                string monkey = numbPattern.Replace(splitLine[1], "$1");
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
                case MachineTypes.PocketNC:
                    TPo.Pt = toolPoint1.Pt * par + toolPoint2.Pt * (1 - par);
                    TPo.Dir = angShift(toolPoint1.Dir,toolPoint2.Dir,par,false);
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
            
            if ((
                TPfrom.MatForm.intersect(TPfrom[TPfrom.Count - 1], TPfrom.MatForm.safeDistance).thrDist > 0.0001
                && TPto.MatForm.intersect(TPfrom[TPfrom.Count - 1], TPto.MatForm.safeDistance).thrDist > 0.0001
                ) || (
                TPfrom.MatForm.intersect(TPto[0], TPfrom.MatForm.safeDistance).thrDist > 0.0001
                && TPto.MatForm.intersect(TPto[0], TPto.MatForm.safeDistance).thrDist > 0.0001
               ))
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
                    route.Add(TPfrom[TPfrom.Count - 1].Pt);
                    route.Add(TPto[0].Pt);
                    
                    int i;
                    MFintersects inters;
                    MFintersects fromMid;

                    // loop through intersecting with safe bubble and adding points
                    for (i = 0; i < (route.Count - 1) && i < 100;)
                    {
                        if(TPto.MatForm.intersect(route[i], route[i + 1], TPto.MatForm.safeDistance, out inters))
                        {
                            fromMid = TPto.MatForm.intersect(inters.mid, inters.midOut, TPto.MatForm.safeDistance * 1.1);
                            route.Insert(i + 1, inters.mid + fromMid.thrDist * inters.midOut);
                        }
                        else
                        {
                            i++;
                        }
                    }

                    // get rid of start and end points that are already in the paths
                    route.RemoveAt(0);
                    route.RemoveAt(route.Count - 1);

                    foreach(Point3d Pt in route)
                    {
                        // add new point at speed 0 to describe rapid move.
                        Move.Add(new ToolPoint(Pt,new Vector3d(0,0,0),-1,0));
                    }

                    break;

                case MachineTypes.PocketNC:

                    // new method using intersect

                    route = new List<Point3d>();

                    route.Add(TPfrom[TPfrom.Count - 1].Pt);
                    route.Add(TPto[0].Pt);

                    // loop through intersecting with safe bubble and adding points
                    for(i=0;i<(route.Count-1)&&route.Count < 1000;)
                    {
                        
                        if (TPto.MatForm.intersect(route[i], route[i + 1], TPto.MatForm.safeDistance, out inters))
                        {
                            fromMid = TPto.MatForm.intersect(inters.mid, inters.midOut, TPto.MatForm.safeDistance * 1.1);
                            route.Insert(i + 1, inters.mid + fromMid.thrDist * inters.midOut);

                            MFintersects test = TPto.MatForm.intersect(route[i + 1], inters.midOut, TPto.MatForm.safeDistance);
                        }
                        else
                        {
                            i++;
                        }
                    }

                    // add extra points if the angle change between steps is too large (pi/30)

                    Vector3d fromDir = TPfrom[TPfrom.Count - 1].Dir;
                    Vector3d toDir = TPto[0].Dir;
                    Vector3d mixDir;
                    bool lng = false;
                    // ask machine how far it has to move in angle. 
                    double angSpread = this.angDiff(TPfrom[TPfrom.Count - 1].Dir, TPto[0].Dir,lng);

                    int steps = (int)Math.Ceiling(30*angSpread/(Math.PI*route.Count));
                    if (steps == 0) steps = 1; // Need to add at least one point even if angSpread is 0
                    int j;

                    // Try to build a path with angles. 
                    // If a tool line hits the material 
                    // switch to the longer rotate and try again

                    for(i=0; i<(route.Count-1);i++)
                    {
                        // add new point at speed 0 to describe rapid move.
                        for(j=0;j<steps;j++)
                        {
                            mixDir=this.angShift(fromDir,toDir,(double)(steps*i+j)/(double)(steps*(route.Count-1)),lng);

                            ToolPoint newTP = new ToolPoint((j * route[i + 1] + (steps - j) * route[i]) / steps, mixDir, -1, 0);
                            if(TPfrom.MatForm.intersect(newTP,0).thrDist > 0
                                || TPto.MatForm.intersect(newTP, 0).thrDist > 0)
                            {
                                if(lng) 
                                {   // something has gone horribly wrong and 
                                    // both angle change directions will hit the material
 
                                    throw new System.Exception("Safe Route failed to find a safe path from the end of one toolpath to the next.");
                                } else
                                { // start again with the longer angle change
                                    
                                    lng=true;
                                    i=0;
                                    j=0;
                                    angSpread = this.angDiff(TPfrom[TPfrom.Count - 1].Dir, TPto[0].Dir,lng);
                                    steps = (int)Math.Ceiling(30*angSpread/(Math.PI*route.Count));
                                    Move = TPto.copyWithNewPoints(new List<ToolPoint>());
                                }
                            } else { 
                                Move.Add(newTP);
                            }
                        }
                    }
                    // get rid of start point that was already in the paths
                    Move.RemoveAt(0);

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
        // find the (maximum absolute) angular movement between too toolpoints

        private double angDiff(Vector3d tpFrom, Vector3d tpTo, bool lng)
        {
            if (this.type == MachineTypes.PocketNC)
            {
                Vector3d ang1 = Machine.Orient_FiveAxisABP(tpFrom);
                Vector3d ang2 = Machine.Orient_FiveAxisABP(tpTo);

                Vector2d diff = new Vector2d();
                if(lng)
                {
                    diff.X = 2 * Math.PI - Math.Abs(ang1.X - ang2.X);
                    diff.Y = 2 * Math.PI - Math.Abs(ang1.Y - ang2.Y);
                }
                else
                {
                    diff.X = Math.Abs(ang1.X - ang2.X);
                    diff.Y = Math.Min(Math.Min(Math.Abs(ang1.Y - ang2.Y), Math.Abs(2 * Math.PI + ang1.Y - ang2.Y)), Math.Abs(2 * Math.PI - ang1.Y + ang2.Y));
                }
                return Math.Max(diff.X,diff.Y);
            } else
            {
                throw new System.NotImplementedException("Machine has no rotation or has not implemented a calculation of rotation.");
            }
        }

        // Interpolate the machine axes linearly between two positions. 
        // If both axes have full rotation then there are four ways to do this.
        // If lng is true then reverse the direction on the B axis (for PocketNC)
        // TODO work with anything other than AB machine
        // TODO give more options for long turns. 
        private Vector3d angShift(Vector3d fromDir, Vector3d toDir, double p, bool lng)
        {
            Vector3d fromAB = Orient_FiveAxisABP(fromDir);
            Vector3d toAB = Orient_FiveAxisABP(toDir);
            Vector3d outAB;

            outAB = (1-p) * fromAB + p * toAB;
            // switch to long way round or short way round depending on gap between angles
            if((lng && Math.Abs(fromAB.Y - toAB.Y) <= Math.PI) ||
               (!lng && Math.Abs(fromAB.Y -toAB.Y) > Math.PI))
            {
                Vector3d alt;
                if (fromAB.Y > toAB.Y) { alt = new Vector3d(0, 2 * Math.PI, 0); }
                else { alt = new Vector3d(0, -2 * Math.PI, 0); }
                outAB = (1-p) * fromAB + p * (toAB+alt);
            }
            // TODO this is kludgy make a consistent interface for Kinematics and Inverse Kinematics
            // Probably a separate library that machines can refer to. 
            return this.ReadTP_PocketNC(0,0,0,180.0*outAB.X/Math.PI,180.0*outAB.Y/Math.PI,0,0,0).Dir;
        }

        static private Point3d missSphere(Point3d pPt, Point3d cPt, Vector3d away, double safeD, out double d)
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

        ICAMel_Base ICAMel_Base.Duplicate()
        {
            throw new NotImplementedException("Machine has not yet implemented Duplicate.");
        }
    }

    // Grasshopper Type Wrapper
    public class GH_Machine : CAMel_Goo<Machine>
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
        public GH_Machine(string Name, MachineTypes Type, string Header, string Footer, string filestart, string fileend)
        {
            this.Value = new Machine(Name, Type, Header, Footer, filestart, fileend);
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