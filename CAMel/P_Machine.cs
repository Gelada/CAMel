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

    public class Machine : CA_base
    {
        public string name;
        public MachineTypes type;
        public string filestart;
        public string header;
        public string footer;
        public string fileend;
        public char CommentChar;
        public char endCommentChar;
        public string SpeedChangeCommand;
        public double PathJump; // Max distance allowed between paths in material.
        public string SectionBreak { get; set; }
        public Vector3d Pivot;

        // Default Constructor (un-named 3 Axis)
        public Machine()
        {
            this.name = "Unamed Machine";
            this.type = MachineTypes.ThreeAxis;
            this.header = "";
            this.footer = "";
            this.filestart = "";
            this.fileend = "";
            this.CommentChar = '(';
            this.endCommentChar = ')';
            this.SectionBreak = "(------------------------------------------)";
            this.SpeedChangeCommand = "M03 ";
            this.PathJump = 2;
            this.Pivot = Vector3d.Zero;
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
            this.CommentChar = '(';
            this.endCommentChar = ')';
            this.SectionBreak = "(------------------------------------------)";
            this.SpeedChangeCommand = "M03 ";
            this.PathJump = 2;
            this.Pivot = Vector3d.Zero;
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
            this.CommentChar = '(';
            this.endCommentChar = ')';
            this.SectionBreak = "(------------------------------------------)";
            this.SpeedChangeCommand = "M03 ";
            this.PathJump = 2;
            this.Pivot = Vector3d.Zero;
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
        // TODO Bring PocketNC into this!
        // Call the correct IK function
        public string InverseKinematics (ToolPoint TP, MaterialTool MT)
        {
            string GPoint = "";
            switch (this.type)
            {
                case MachineTypes.ThreeAxis:
                    GPoint = this.IK_ThreeAxis(TP, MT);
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

            return GPoint;
        }

        private string IK_PocketNC(ToolPoint TP, MaterialTool MT, Vector2d AB, ref Point3d machinePt)
        {
            Point3d OP = TP.Pt;

            // rotate from material orientation to machine orientation
            OP.Transform(Transform.Rotation(AB.Y, Vector3d.YAxis, Point3d.Origin));
            OP.Transform(Transform.Rotation(AB.X, Vector3d.XAxis, Point3d.Origin));

            // translate from origin at pivot to origin set so that the tooltip is at machine origin
            OP = OP - this.Pivot + Vector3d.ZAxis * MT.toolLength;
            //HACK!
            if (OP.Z > 0.02) OP.Z = 0.02;
            //if (OP.X < -2.0) OP.X = -2.0;
            //if (OP.X > 2.4) OP.X = 2.4;

            String GPoint = "";
            GPoint += "X" + OP.X.ToString("0.000") + " Y" + OP.Y.ToString("0.000") + " Z" + OP.Z.ToString("0.000") + " ";
            GPoint += "A" + (180 * AB.X / Math.PI).ToString("0.000") + " B" + (180 * AB.Y / Math.PI).ToString("0.000");

            machinePt = OP;
            return GPoint;
        }
        private string IK_PocketNC_orient(MaterialTool materialTool, Vector2d AB)
        {
            String GPoint = "";
            GPoint += "A" + (180 * AB.X / Math.PI).ToString("0.000") + " B" + (180 * AB.Y / Math.PI).ToString("0.000");

            return GPoint;
        }

        // Always gives B from -pi to pi and A from -pi/2 to pi/2.
        private Vector2d Orient_FiveAxisABP(ToolPoint TP)
        {
            Vector3d UV = TP.Dir;

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

            return new Vector2d(Ao, Bo);
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
                
                foreach(string err in Pt.error)
                {
                    Co.AddError(err);
                    Co.AppendLine(this.CommentChar + err + this.endCommentChar);
                }
                foreach (string warn in Pt.warning)
                {
                    Co.AddWarning(warn);
                    Co.AppendLine(this.CommentChar + warn + this.endCommentChar);
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

                if(Pt.localCode != "")
                {
                    PtCode = PtCode + Pt.localCode;
                }

                if(Pt.name != "")
                {
                    PtCode = PtCode + " " + this.CommentChar + Pt.name + this.endCommentChar;
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
            Vector2d AB,newAB;
            double Bto = 0;  // Allow for smooth adjustment through the cusp with A at 90.
            int Bsteps = 0;  //
            string PtCode;
           
            if (beforePoint == null) // There were no previous points
            {
                if (TP.Pts.Count > 0)
                {
                    feed = TP.Pts[0].feed;
                    speed = TP.Pts[0].speed;
                    if (feed < 0) { feed = TP.MatTool.feedCut; }
                    if (speed < 0) { speed = TP.MatTool.speed; }
                    AB = this.Orient_FiveAxisABP(TP.Pts[0]);
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
                    AB = new Vector2d(Math.PI/2.0, 0);
                }
            }
            else
            {
                feed = beforePoint.feed;
                speed = beforePoint.speed;
                AB = new Vector2d(Co.MachineState["A"], Co.MachineState["B"]);
            }

            if (feed < 0) { feed = TP.MatTool.feedCut; }
            if (speed < 0) { speed = TP.MatTool.speed; }
            
            int i,j;
            ToolPoint Pt;
            Point3d MachPos = new Point3d(0,0,0);
            for(i=0;i<TP.Pts.Count;i++)
            {
                Pt = TP.Pts[i];

                foreach (string err in Pt.error)
                {
                    Co.AddError(err);
                    Co.AppendLine(this.CommentChar + err + this.endCommentChar);
                }
                foreach (string warn in Pt.warning)
                {
                    Co.AddWarning(warn);
                    Co.AppendLine(this.CommentChar + warn + this.endCommentChar);
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
                newAB = Orient_FiveAxisABP(Pt);

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

                        while (j < (TP.Pts.Count-1) && Math.Abs(Orient_FiveAxisABP(TP.Pts[j]).X - Math.PI/2.0) < AngleAcc) j++;

                        // If we are at the start of a path and vertical then we can just use the first non-vertical 
                        // position for the whole run. 
                        if (Math.Abs(AB.X - Math.PI / 2.0) < AngleAcc) 
                        {
                            Bto = Orient_FiveAxisABP(TP.Pts[j]).Y;
                            Bsteps = j - i;
                            newAB.Y = Bto;
                        }
                        // if we get to the end and it is still vertical we do not need to rotate.
                        else if (Math.Abs(Orient_FiveAxisABP(TP.Pts[j]).X) < AngleAcc)
                        {
                            Bto = AB.X;
                            Bsteps = j - i;
                            newAB.Y = Bto;
                        }
                        else
                        {
                            Bto = Orient_FiveAxisABP(TP.Pts[j]).Y;
                            Bsteps = j - i;
                            newAB.Y = AB.Y;
                        }
                    }
                }

                // (throw bounds error if B goes past +-999999 degrees or A is not between -5 and 95)

                if (Math.Abs(180.0*newAB.Y/(Math.PI)) > 999999)
                {
                    Co.AddError("Out of bounds on B");
                    Co.AppendLine(this.CommentChar + "Out of bounds on B" + this.endCommentChar);
                }
                if ((180.0 * newAB.X / Math.PI > 95) || (180.0 * newAB.X / Math.PI < -5))
                {
                    Co.AddError("Out of bounds on A");
                    Co.AppendLine(this.CommentChar + "Out of bounds on A" + this.endCommentChar);
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

                if (Pt.localCode != "")
                {
                    PtCode = PtCode + Pt.localCode;
                }

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

            if (TP.Pts.Count > 0)
            {
                PtOut = new ToolPoint(TP.Pts[TP.Pts.Count - 1]);
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
        // TODO PocketNC read
        private ToolPath ReadCode_PocketNC(string Code)
        {
            ToolPath TP = new ToolPath();

            double toolLength = 1;

            double X = 0, Y = 0, Z = 0, A = 0, B = 0, F = -1, S = -1;
            bool changed, found, Fchanged, feedfound, Schanged, speedfound;

            System.Text.StringBuilder OutputCode = new System.Text.StringBuilder();

            string Xpattern = @".*X([0-9\-.]+).*";
            string Ypattern = @".*Y([0-9\-.]+).*";
            string Zpattern = @".*Z([0-9\-.]+).*";
            string Apattern = @".*A([0-9\-.]+).*";
            string Bpattern = @".*B([0-9\-.]+).*";
            string Fpattern = @".*F([0-9\-.]+).*";
            string Spattern = @".*S([0-9\-.]+).*";
            string G0pattern = @"G0.*";
            string LinePattern = @".*";

            System.Text.RegularExpressions.MatchCollection Lines;

            Lines = System.Text.RegularExpressions.Regex.Matches(Code, LinePattern);

            int i = 0;

            foreach (System.Text.RegularExpressions.Match line in Lines)
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
                A = GetValue(line.ToString(), Apattern, A, ref found, ref changed);
                B = GetValue(line.ToString(), Bpattern, B, ref found, ref changed);
                F = GetValue(line.ToString(), Fpattern, F, ref feedfound, ref Fchanged);
                S = GetValue(line.ToString(), Spattern, S, ref speedfound, ref Schanged);

                //interpret a G0 command.
                if (System.Text.RegularExpressions.Regex.IsMatch(line.ToString(), G0pattern))
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
                    TP.Pts.Add(ReadTP_PocketNC(X,Y,Z,A,B,F,S,toolLength));
                    if(Z<-3.3)
                    {
                        TP.Pts[TP.Pts.Count-1].localCode = "Z";
                    }
                    i++;
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

                case MachineTypes.PocketNC:

                    // Start with a straight line, with a maximum rotation of pi/30 between points, 
                    // see how close it comes to danger. If its too close add a new
                    // point and try again.

                    route = new List<Point3d>();
                    route.Add(TPfrom.Pts[TPfrom.Pts.Count - 1].Pt);

                    route.Add(TPto.Pts[0].Pt);
                   
                    dist = TPfrom.MatForm.closestDanger(route, TPto.MatForm, out cPt, out away, out i);

                    safeD = Math.Max(TPfrom.MatForm.safeDistance, TPto.MatForm.safeDistance);

                    // loop through adding points at problem places until we have 
                    // everything sorted!
                    // Warning this could race, it shouldn't though.
                    int checker = 0;
                    while (dist < safeD && checker < 100)
                    {
                        checker++;
                        // add or edit a point by pushing it to safeD plus a little
                        
                        route.Insert(i + 1, cPt + (safeD - dist + .125) * away);

                        dist = TPfrom.MatForm.closestDanger(route, TPto.MatForm, out cPt, out away, out i);
                    }

                    // add extra points if the angle change between steps is too large (pi/30)

                    Vector3d fromDir = TPfrom.Pts[TPfrom.Pts.Count - 1].Dir;
                    Vector3d toDir = TPto.Pts[0].Dir;
                    Vector3d mixDir;
                    bool lng = false;
                    // ask machine how far it has to move in angle. 
                    double angSpread = this.angDiff(TPfrom.Pts[TPfrom.Pts.Count - 1], TPto.Pts[0],lng);

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
                            mixDir=this.angShift(fromDir,toDir,(double)(steps*i+j)/(double)(steps*route.Count),lng);
                            ToolPoint newTP = new ToolPoint((j * route[i + 1] + (steps - j) * route[i]) / steps, mixDir, "", -1, 0);
                            if(TPfrom.MatForm.TPRayIntersect(newTP) || TPto.MatForm.TPRayIntersect(newTP))
                            {
                                if(lng == true) 
                                {   // something has gone horribly wrong and 
                                    // both angle change directions will hit the material
 
                                    throw new System.Exception("Safe Route failed to find a safe path from the end of one toolpath to the next.");
                                } else
                                { // start again with the longer angle change
                                    lng=true;
                                    i=0;
                                    j=0;
                                    angSpread = this.angDiff(TPfrom.Pts[TPfrom.Pts.Count - 1], TPto.Pts[0],lng);
                                    steps = (int)Math.Ceiling(30*angSpread/(Math.PI*route.Count));
                                    Move = TPto.copyWithNewPoints(new List<ToolPoint>());
                                }
                            } else { 
                                Move.Pts.Add(newTP);
                            }
                        }
                    }
                    // get rid of start point that was already in the paths
                    Move.Pts.RemoveAt(0);

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

        private double angDiff(ToolPoint tpFrom, ToolPoint tpTo, bool lng)
        {
            if (this.type == MachineTypes.PocketNC)
            {
                Vector2d ang1 = this.Orient_FiveAxisABP(tpFrom);
                Vector2d ang2 = this.Orient_FiveAxisABP(tpTo);

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

        // Create a vector a proportion p of the rotation between two vectors.
        // if lng is true go the long way
        private Vector3d angShift(Vector3d fromDir, Vector3d toDir, double p, bool lng)
        {
            double ang;
            if (lng)
            {
                ang = Vector3d.VectorAngle(fromDir, toDir) - 2*Math.PI;
            }
            else
            {
                ang = Vector3d.VectorAngle(fromDir, toDir);
            }
            Vector3d newDir = fromDir;
            newDir.Rotate(ang*p,Vector3d.CrossProduct(fromDir,toDir));
            return newDir;
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