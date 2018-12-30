using System;
using System.Collections.Generic;
using System.Text;

using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using CAMel.Types.MaterialForm;
using System.Text.RegularExpressions;

namespace CAMel.Types.Machine
{
    public class PocketNC : IGCodeMachine
    {
        public double pathJump { get; set; }
        public string sectionBreak { get; set; }
        public string speedChangeCommand { get; set; }
        public string toolChangeCommand { get; set; }
        public string fileStart { get; set; }
        public string fileEnd { get; set; }
        public string header { get; set; }
        public string footer { get; set; }
        public string name { get; set; }
        public string commentStart { get; set; }
        public string commentEnd { get; set; }
        private List<char> terms;

        private double Amin { get; set; }
        private double Amax { get; set; }
        private double Bmax { get; set; }
        public bool TLC { get; set; }

        public Vector3d Pivot { get; set; } // Position of machine origin in design space.

        public bool IsValid => throw new NotImplementedException();

        public PocketNC()
        {
            this.name = "PocketNC";
            this.TLC = false;
            this.header = String.Empty;
            this.footer = String.Empty;
            this.fileStart = String.Empty;
            this.fileEnd = String.Empty;
            this.commentStart = "(";
            this.commentEnd = ")";
            this.sectionBreak = "------------------------------------------";
            this.speedChangeCommand = "M03";
            this.toolChangeCommand = "G43H";
            this.pathJump = .25;
            this.Pivot = new Vector3d(0, 0, 0);
            this.Amin = 0;
            this.Amax = Math.PI/2.0;
            this.Bmax = 9999.0 * Math.PI / 180.0;
            setTerms();
        }
        public PocketNC(string name, string header, string footer, double Amin, double Amax, double Bmax, bool TLC)
        {
            this.name = name;
            this.TLC = TLC;
            this.header = header;
            this.footer = footer;
            this.fileStart = String.Empty;
            this.fileEnd = String.Empty;
            this.commentStart = "(";
            this.commentEnd = ")";
            this.sectionBreak = "------------------------------------------";
            this.speedChangeCommand = "M03";
            this.toolChangeCommand = "G43H";
            this.pathJump = .25;
            this.Pivot = new Vector3d(0,0,0);
            this.Amin = Amin;
            this.Amax = Amax;
            this.Bmax = Bmax;
            setTerms();
        }
        public PocketNC(PocketNC TA)
        {
            this.name = TA.name;
            this.TLC = TA.TLC;
            this.header = TA.header;
            this.footer = TA.footer;
            this.fileStart = TA.fileStart;
            this.fileEnd = TA.fileEnd;
            this.commentStart = TA.commentStart;
            this.commentEnd = TA.commentEnd;
            this.sectionBreak = TA.sectionBreak;
            this.speedChangeCommand = TA.speedChangeCommand;
            this.toolChangeCommand = TA.toolChangeCommand;
            this.pathJump = TA.pathJump;
            this.Pivot = TA.Pivot;
            this.Amin = TA.Amin;
            this.Amax = TA.Amax;
            this.Bmax = TA.Bmax;
            this.terms = new List<char>();
            this.terms.AddRange(TA.terms);
        }

        private void setTerms()
        {
            this.terms = new List<char>();
            this.terms.Add('X');
            this.terms.Add('Y');
            this.terms.Add('Z');
            this.terms.Add('A');
            this.terms.Add('B');
            this.terms.Add('S');
            this.terms.Add('F');
        }

        public string TypeDescription
        { get { return @"Instructions for a PocketNC machine"; } }
        public string TypeName
        { get { return @"CAMelPocketNC"; } }
        public string comment(string L)
        {
            if (L == "" || L == " ") { return " "; }
            else { return this.commentStart + " " + L + " " + this.commentEnd; }
        }

        public ICAMel_Base Duplicate() => new PocketNC(this);

        public ToolPath insertRetract(ToolPath tP) => tP.MatForm.InsertRetract(tP);

        public ToolPoint Interpolate(ToolPoint fP, ToolPoint tP, MaterialTool MT, double par, bool lng)
        {
            double toolLength = MT.toolLength;
            if (this.TLC) { toolLength = 0; }
            return Kinematics.Interpolate_FiveAxisABTable(this.Pivot, toolLength, fP, tP, par, lng);
        }
        public double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool MT, bool lng)
        {
            double toolLength = MT.toolLength;
            if (this.TLC) { toolLength = 0; }
            return Kinematics.AngDiff_FiveAxisABTable(this.Pivot, toolLength, tP1, tP2, lng);
        }

        public ToolPath ReadCode(List<MaterialTool> MTs, string Code) => GCode.GcRead(this,MTs,Code,terms);

        public ToolPoint readTP(Dictionary<char, double> vals, MaterialTool MT)
        {
            Point3d MachPt = new Point3d(vals['X'], vals['Y'], vals['Z']);
            Vector3d AB = new Vector3d(vals['A']*Math.PI/180.0, vals['B'] * Math.PI/180.0, 0);

            ToolPoint TP = new ToolPoint();
            TP.speed = vals['S'];
            TP.feed = vals['F'];
            double toolLength = MT.toolLength;
            if(this.TLC) { toolLength = 0; }

            return Kinematics.K_FiveAxisABTable(TP, this.Pivot, toolLength, MachPt, AB);        
        }

        public Vector3d toolDir(ToolPoint TP) => TP.Dir;

        public ToolPoint writeCode(ref CodeInfo Co, ToolPath tP, ToolPoint beforePoint)
        {
            GCode.GcPathStart(this, ref Co, tP);

            double AngleAcc = 0.0001; // accuracy of angles to assume we lie on the cusp.

            // We will watch for speed and feed changes.
            // We will adjust A and B as best as possible and otherwise throw errors.
            // Manual unwinding Grrrr!

            // work out initial values of feed. 

            bool FChange = false;
            bool SChange = false;

            double feed;
            double speed;
            Vector3d AB, newAB;
            double Bto = 0;  // Allow for smooth adjustment through the cusp with A at 90.
            int Bsteps = 0;  //
            string PtCode;
            Point3d machPt = new Point3d();

            double toolLength = tP.MatTool.toolLength;
            if(this.TLC) { toolLength = 0; }

            if (beforePoint == null) // There were no previous points
            {
                if (tP.Count > 0)
                {
                    feed = tP[0].feed;
                    speed = tP[0].speed;
                    if (feed < 0) { feed = tP.MatTool.feedCut; }
                    if (speed < 0) { speed = tP.MatTool.speed; }
                    AB = Kinematics.IK_FiveAxisABTable(tP[0], this.Pivot, toolLength, out machPt );
                    FChange = true;
                    SChange = false;
                    // making the first move. Orient the tool first

                    PtCode = GCode.GcFiveAxisAB_orient(machPt, AB);
                    PtCode = "G00 " + PtCode;
                    PtCode = this.speedChangeCommand + " S" + speed.ToString("0") + "\n" + PtCode;
                    Co.Append(PtCode);
                }
                else
                {
                    feed = -1;
                    speed = -1;
                    AB = new Vector3d(Math.PI / 2.0, 0, 0);
                }
            }
            else
            {
                feed = beforePoint.feed;
                speed = beforePoint.speed;
                AB = new Vector3d(Co.MachineState["A"], Co.MachineState["B"], 0);
            }

            if (feed < 0) { feed = tP.MatTool.feedCut; }
            if (speed < 0) { speed = tP.MatTool.speed; }

            int i, j;
            ToolPoint Pt;
            Point3d MachPos = new Point3d(0, 0, 0);
            for (i = 0; i < tP.Count; i++)
            {
                Pt = tP[i];

                if (Pt.error != null)
                {
                    foreach (string err in Pt.error) { Co.AddError(err); }
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
                    else if (feed != tP.MatTool.feedCut) // Default to the cut feed rate.
                    {
                        FChange = true;
                        feed = tP.MatTool.feedCut;
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

                // get naive orientation and Machine XYZ position
                newAB = Kinematics.IK_FiveAxisABTable(Pt, this.Pivot, toolLength, out machPt);

                // adjust B to correct period
                newAB.Y = newAB.Y + 2.0 * Math.PI * Math.Round((AB.Y - newAB.Y) / (2.0 * Math.PI));

                // set A to 90 if it is close (to avoid a lot of messing with B for no real reason)

                if (Math.Abs(newAB.X - Math.PI / 2.0) < AngleAcc) { newAB.X = Math.PI / 2.0; }

                // adjust through cusp

                if (newAB.X == Math.PI / 2.0) // already set if nearly there. 
                {
                    // detect that we are already moving
                    if (Bsteps > 0)
                    {
                        newAB.Y = AB.Y + (Bto - AB.Y) / Bsteps;
                        Bsteps--;
                    }
                    else // head forward to next non-vertical point or the end. 
                    {
                        j = i + 1;

                        while (j < (tP.Count - 1) &&
                            Math.Abs(
                                Kinematics.IK_FiveAxisABTable(tP[j], this.Pivot, toolLength, out machPt).X
                                - Math.PI / 2.0) < AngleAcc)
                        { j++; }

                        // If we are at the start of a path and vertical then we can just use the first non-vertical 
                        // position for the whole run. 
                        if (Math.Abs(AB.X - Math.PI / 2.0) < AngleAcc)
                        {
                            Bto = Kinematics.IK_FiveAxisABTable(tP[j], this.Pivot, toolLength, out machPt).Y;
                            Bsteps = j - i;
                            newAB.Y = Bto;
                        }
                        // if we get to the end and it is still vertical we do not need to rotate.
                        else if (Math.Abs(Kinematics.IK_FiveAxisABTable(tP[j], this.Pivot, toolLength, out machPt).X) < AngleAcc)
                        {
                            Bto = AB.X;
                            Bsteps = j - i;
                            newAB.Y = Bto;
                        }
                        else
                        {
                            Bto = Kinematics.IK_FiveAxisABTable(tP[j], this.Pivot, toolLength, out machPt).Y;
                            Bsteps = j - i;
                            newAB.Y = AB.Y;
                        }
                    }
                }

                // take advantage of the double stance for A, 
                // up until now, A is between -90 and 90, all paths start in 
                // that region, but can rotate out of it if necessary.
                // This will mean some cutable paths bcome impossible.
                // This assumes only a double stance in positive position.

                if (newAB.X > (Math.PI - this.Amax)) // check if double stance is possible
                {
                    if ((newAB.Y - AB.Y) > Math.PI) // check for big rotation in B
                    {
                        newAB.X = Math.PI - newAB.X;
                        newAB.Y = newAB.Y - Math.PI;
                    }
                    else if ((newAB.Y - AB.Y) < -Math.PI) // check for big rotation in B
                    {
                        newAB.X = Math.PI - newAB.X;
                        newAB.Y = newAB.Y + Math.PI;
                    }
                }

                // (throw bounds error if B goes past +-Bmax degrees or A is not between Amin and Amax)


                if (Math.Abs(newAB.Y) > Bmax)
                {
                    Co.AddError("Out of bounds on B");
                }
                if (( newAB.X > Amax) || (newAB.X < Amin))
                {
                    Co.AddError("Out of bounds on A");
                }

                // update AB value

                AB = newAB;

                // Add the position information

                 PtCode = GCode.GcFiveAxisAB(machPt,AB);

                // Act if feed has changed
                if (FChange)
                {
                    if (feed == 0) { PtCode = "G00 " + PtCode; }
                    else { PtCode = "G01 " + PtCode + " F" + feed.ToString("0.00"); }
                }
                FChange = false;

                // Act if speed has changed
                if (SChange)
                {
                    PtCode = this.speedChangeCommand + " S" + speed.ToString("0") + "\n" + PtCode;
                }
                SChange = false;

                PtCode = Pt.preCode + PtCode + Pt.postCode;
                
                if (Pt.name != "")
                {
                    PtCode = PtCode + " " + this.commentStart + Pt.name + this.commentEnd;
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

            if (tP.Count > 0)
            {
                PtOut = new ToolPoint(tP[tP.Count - 1]);
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

        public void writeFileEnd(ref CodeInfo Co, MachineInstruction MI) => GCode.GcInstEnd(this, ref Co, MI);
        public void writeFileStart(ref CodeInfo Co, MachineInstruction MI) => GCode.GcInstStart(this, ref Co, MI);
        public void writeOpEnd(ref CodeInfo Co, MachineOperation MO) => GCode.GcOpEnd(this, ref Co, MO);
        public void writeOpStart(ref CodeInfo Co, MachineOperation MO) => GCode.GcOpStart(this, ref Co, MO);

        // This should call a utility with standard options 
        // a good time to move it is when a second 5-axis is added
        // hopefully at that point there is a better understanding of safe moves!



        public ToolPoint writeTransition(ref CodeInfo Co, ToolPath fP, ToolPath tP, bool first, ToolPoint beforePoint)
        {
            ToolPoint outPoint = beforePoint;
            // check there is anything to transition from or to
            if (fP != null && fP.Count > 0 && tP != null && tP.Count > 0)
            {
                // See if we lie in the material
                // Check end of this path and start of TP
                // For each see if it is safe in one Material Form
                // As we pull back to safe distance we allow a little wiggle.
                if ((
                    fP.MatForm.intersect(fP[fP.Count - 1], fP.MatForm.safeDistance).thrDist > 0.0001
                    && tP.MatForm.intersect(fP[fP.Count - 1], tP.MatForm.safeDistance).thrDist > 0.0001
                    ) || (
                    fP.MatForm.intersect(tP[0], fP.MatForm.safeDistance).thrDist > 0.0001
                    && tP.MatForm.intersect(tP[0], tP.MatForm.safeDistance).thrDist > 0.0001
                    ))
                {
                    // If in material we probably need to throw an error
                    // first path in an operation
                    double Length = fP[fP.Count - 1].Pt.DistanceTo(tP[0].Pt);
                    if (first) { Co.AddError("Transition between operations might be in material."); }
                    else if (Length > this.pathJump) // changing between paths in material
                    {
                        Co.AddError("Long Transition between paths in material. \n"
                            + "To remove this error, don't use ignore, instead change PathJump for the machine from: "
                            + this.pathJump.ToString() + " to at least: " + Length.ToString());
                    }
                }
                else // Safely move from one safe point to another.
                {

                    ToolPath Move = tP.copyWithNewPoints(new List<ToolPoint>());
                    Move.name = "";

                    List<Point3d> route = new List<Point3d>();
                    int i;
                    MFintersects inters;
                    MFintersects fromMid;

                    route.Add(fP[fP.Count - 1].Pt);
                    route.Add(tP[0].Pt);

                    // loop through intersecting with safe bubble and adding points
                    for (i = 0; i < (route.Count - 1) && route.Count < 1000;)
                    {

                        if (tP.MatForm.intersect(route[i], route[i + 1], tP.MatForm.safeDistance, out inters))
                        {
                            fromMid = tP.MatForm.intersect(inters.mid, inters.midOut, tP.MatForm.safeDistance * 1.1);
                            route.Insert(i + 1, inters.mid + fromMid.thrDist * inters.midOut);
                        }
                        else
                        {
                            i++;
                        }
                    }

                    // add extra points if the angle change between steps is too large (pi/30)

                    Vector3d mixDir;
                    bool lng = false;
                    // work out how fare angle needs to move 
                    double angSpread = this.angDiff(fP[fP.Count - 1], tP[0],fP.MatTool, lng);

                    int steps = (int)Math.Ceiling(30 * angSpread / (Math.PI * route.Count));
                    if (steps == 0) { steps = 1; } // Need to add at least one point even if angSpread is 0
                    int j;

                    // Try to build a path with angles. 
                    // If a tool line hits the material 
                    // switch to the longer rotate and try again

                    for (i = 0; i < (route.Count - 1); i++)
                    {
                        double shift;
                        // add new point at speed 0 to describe rapid move.
                        for (j = 0; j < steps; j++)
                        {
                            shift = (double)(steps * i + j) / (double)(steps * (route.Count - 1));
                            mixDir = this.Interpolate(fP[fP.Count-1], tP[0], fP.MatTool, shift,lng).Dir;

                            ToolPoint newTP = new ToolPoint((j * route[i + 1] + (steps - j) * route[i]) / steps, mixDir, -1, 0);
                            if (fP.MatForm.intersect(newTP, 0).thrDist > 0
                                || tP.MatForm.intersect(newTP, 0).thrDist > 0)
                            {
                                if (lng)
                                {   // something has gone horribly wrong and 
                                    // both angle change directions will hit the material

                                    throw new System.Exception("Safe Route failed to find a safe path from the end of one toolpath to the next.");
                                }
                                else
                                { // start again with the longer angle change

                                    lng = true;
                                    i = 0;
                                    j = 0;
                                    angSpread = this.angDiff(fP[fP.Count - 1], tP[0], fP.MatTool, lng);
                                    steps = (int)Math.Ceiling(30 * angSpread / (Math.PI * route.Count));
                                    Move = tP.copyWithNewPoints(new List<ToolPoint>());
                                }
                            }
                            else
                            {
                                Move.Add(newTP);
                            }
                        }
                    }
                    // get rid of start point that was already in the paths
                    Move.RemoveAt(0);
                    outPoint = Move.WriteCode(ref Co, this, beforePoint);
                }
            }
            return outPoint;
        }
    }
}