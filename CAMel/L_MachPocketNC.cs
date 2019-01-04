using System;
using System.Collections.Generic;
using System.Text;

using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using CAMel.Types.MaterialForm;
using static CAMel.Exceptions;
using System.Text.RegularExpressions;

namespace CAMel.Types.Machine
{
    public class PocketNC : IGCodeMachine
    {
        public double pathJump { get; }
        public string sectionBreak { get; }
        public string speedChangeCommand { get; }
        public string toolChangeCommand { get; }
        public string fileStart { get; }
        public string fileEnd { get; }
        public string header { get; }
        public string footer { get; }
        public string name { get; }
        public string commentStart { get; }
        public string commentEnd { get; }
        private readonly List<char> terms;

        private double aMin { get; }
        private double aMax { get; }
        private double bMax { get; }
        public bool toolLengthCompensation { get; }

        public Vector3d pivot { get; } // Position of machine origin in design space.

        public PocketNC(string name, string header, string footer, Vector3d pivot, double Amin, double Amax, double Bmax, bool TLC, double pathJump)
        {
            this.name = name;
            this.toolLengthCompensation = TLC;
            this.header = header;
            this.footer = footer;
            this.fileStart = String.Empty;
            this.fileEnd = String.Empty;
            this.commentStart = "(";
            this.commentEnd = ")";
            this.sectionBreak = "------------------------------------------";
            this.speedChangeCommand = "M03";
            this.toolChangeCommand = "G43H";
            this.pathJump = pathJump;
            this.pivot = pivot;
            this.aMin = Amin;
            this.aMax = Amax;
            this.bMax = Bmax;
            this.terms = new List<char> { 'X', 'Y', 'Z', 'A', 'B', 'S', 'F' };
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


        public ToolPath insertRetract(ToolPath tP)
        {
            if(tP.matForm == null) { matFormException(); }
            return tP.matForm.insertRetract(tP);
        }

        public ToolPoint interpolate(ToolPoint fP, ToolPoint tP, MaterialTool MT, double par, bool lng)
        {
            double toolLength = this.toolLengthCompensation ? 0 : MT.toolLength;
            return Kinematics.interpolateFiveAxisABTable(this.pivot, toolLength, fP, tP, par, lng);
        }
        public double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool MT, bool lng)
        {
            double toolLength = this.toolLengthCompensation ? 0 : MT.toolLength;
            return Kinematics.angDiffFiveAxisABTable(this.pivot, toolLength, tP1, tP2, lng);
        }

        public ToolPath readCode(List<MaterialTool> MTs, string Code) => GCode.gcRead(this,MTs,Code, this.terms);

        public ToolPoint readTP(Dictionary<char, double> vals, MaterialTool MT)
        {
            Point3d MachPt = new Point3d(vals['X'], vals['Y'], vals['Z']);
            Vector3d AB = new Vector3d(vals['A']*Math.PI/180.0, vals['B'] * Math.PI/180.0, 0);

            ToolPoint TP = new ToolPoint
            {
                speed = vals['S'],
                feed = vals['F']
            };
            
            double toolLength = this.toolLengthCompensation ? 0 : MT.toolLength;

            return Kinematics.kFiveAxisABTable(TP, this.pivot, toolLength, MachPt, AB);        
        }

        public Vector3d toolDir(ToolPoint TP) => TP.dir;

        readonly double AngleAcc = 0.0001; // accuracy of angles to assume we lie on the cusp.

        public void writeCode(ref CodeInfo Co, ToolPath tP)
        {
            // Double check tP does not have additions.
            if (tP.Additions.any) { additionsException(); }

            if (tP.Count > 0) // Just ignore 0 length paths
            {
                if (tP.matTool == null) { matToolException(); }
                GCode.gcPathStart(this, ref Co, tP);

                // We will watch for speed and feed changes.
                // We will adjust A and B as best as possible and otherwise throw errors.
                // Manual unwinding Grrrr!

                // work out initial values of feed. 

                bool FChange = false;
                bool SChange = false;

                double feed = Co.machineState["F"];
                double speed = Co.machineState["S"];
                if (feed < 0) { feed = tP.matTool.feedCut; FChange = true; }
                if (speed < 0) { speed = tP.matTool.speed; SChange = true; }

                Vector3d AB = new Vector3d(Co.machineState["A"], Co.machineState["B"], 0), newAB;

                double Bto = 0;  // Allow for smooth adjustment through the cusp with A at 90.
                int Bsteps = 0;  //

                string PtCode;
                Point3d machPt = new Point3d();

                double toolLength = this.toolLengthCompensation ? 0 : tP.matTool.toolLength;

                int i, j;
                ToolPoint Pt;
                for (i = 0; i < tP.Count; i++)
                {
                    Pt = tP[i];

                    if (Pt.error != null)
                    {
                        foreach (string err in Pt.error) { Co.addError(err); }
                    }
                    if (Pt.warning != null)
                    {
                        foreach (string warn in Pt.warning) { Co.addWarning(warn); }
                    }

                    // Establish new feed value
                    if (Pt.feed != feed)
                    {
                        if (Pt.feed >= 0)
                        {
                            FChange = true;
                            feed = Pt.feed;
                        }
                        else if (feed != tP.matTool.feedCut) // Default to the cut feed rate.
                        {
                            FChange = true;
                            feed = tP.matTool.feedCut;
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
                    newAB = Kinematics.ikFiveAxisABTable(Pt, this.pivot, toolLength, out machPt);

                    // adjust B to correct period
                    newAB.Y = newAB.Y + 2.0 * Math.PI * Math.Round((AB.Y - newAB.Y) / (2.0 * Math.PI));

                    // set A to 90 if it is close (to avoid a lot of messing with B for no real reason)

                    if (Math.Abs(newAB.X - Math.PI / 2.0) < this.AngleAcc) { newAB.X = Math.PI / 2.0; }

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
                                    Kinematics.ikFiveAxisABTable(tP[j], this.pivot, toolLength, out machPt).X
                                    - Math.PI / 2.0) < this.AngleAcc)
                            { j++; }

                            // If we are at the start of a path and vertical then we can just use the first non-vertical 
                            // position for the whole run. 
                            if (Math.Abs(AB.X - Math.PI / 2.0) < this.AngleAcc)
                            {
                                Bto = Kinematics.ikFiveAxisABTable(tP[j], this.pivot, toolLength, out machPt).Y;
                                Bsteps = j - i;
                                newAB.Y = Bto;
                            }
                            // if we get to the end and it is still vertical we do not need to rotate.
                            else if (Math.Abs(Kinematics.ikFiveAxisABTable(tP[j], this.pivot, toolLength, out machPt).X) < this.AngleAcc)
                            {
                                Bto = AB.X;
                                Bsteps = j - i;
                                newAB.Y = Bto;
                            }
                            else
                            {
                                Bto = Kinematics.ikFiveAxisABTable(tP[j], this.pivot, toolLength, out machPt).Y;
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

                    if (newAB.X > (Math.PI - this.aMax)) // check if double stance is possible
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


                    if (Math.Abs(newAB.Y) > this.bMax)
                    {
                        Co.addError("Out of bounds on B");
                    }
                    if ((newAB.X > this.aMax) || (newAB.X < this.aMin))
                    {
                        Co.addError("Out of bounds on A");
                    }

                    // update AB value

                    AB = newAB;

                    // Add the position information

                    PtCode = GCode.gcFiveAxisAB(machPt, AB);

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

                    if (Pt.name != string.Empty)
                    {
                        PtCode = PtCode + " " + this.commentStart + Pt.name + this.commentEnd;
                    }

                    Co.append(PtCode);
                    // Adjust ranges

                    Co.growRange("X", machPt.X);
                    Co.growRange("Y", machPt.Y);
                    Co.growRange("Z", machPt.Z);
                    Co.growRange("A", AB.X);
                    Co.growRange("B", AB.Y);
                }

                // Pass machine state information

                Co.machineState.Clear();
                Co.machineState.Add("X", machPt.X);
                Co.machineState.Add("Y", machPt.Y);
                Co.machineState.Add("Z", machPt.Z);
                Co.machineState.Add("A", AB.X);
                Co.machineState.Add("B", AB.Y);
                Co.machineState.Add("F", feed);
                Co.machineState.Add("S", speed);
            }    
        }

        public void writeFileStart(ref CodeInfo Co, MachineInstruction MI, ToolPath startPath)
        {
            // Set up Machine State  

            if (startPath.matTool == null) { matToolException(); }
            Point3d MachPt = new Point3d();
            double toolLength = MI.mach.toolLengthCompensation ? 0 : startPath.matTool.toolLength;
            Vector3d AB = Kinematics.ikFiveAxisABTable(MI.startPath.firstP, this.pivot, toolLength, out MachPt);

            Co.machineState.Clear();
            Co.machineState.Add("X", MachPt.X);
            Co.machineState.Add("Y", MachPt.Y);
            Co.machineState.Add("Z", MachPt.Z);
            Co.machineState.Add("A", AB.X);
            Co.machineState.Add("B", AB.Y);
            Co.machineState.Add("F", -1);
            Co.machineState.Add("S", -1);

            GCode.gcInstStart(this, ref Co, MI, startPath);
        }
        public void writeFileEnd(ref CodeInfo Co, MachineInstruction MI, ToolPath finalPath, ToolPath endPath) => GCode.gcInstEnd(this, ref Co, MI, finalPath, endPath);
        public void writeOpStart(ref CodeInfo Co, MachineOperation MO) => GCode.gcOpStart(this, ref Co, MO);
        public void writeOpEnd(ref CodeInfo Co, MachineOperation MO) => GCode.gcOpEnd(this, ref Co, MO);
        
        // This should call a utility with standard options 
        // a good time to move it is when a second 5-axis is added
        // hopefully at that point there is a better understanding of safe moves!



        public void writeTransition(ref CodeInfo Co, ToolPath fP, ToolPath tP, bool first)
        {
            if (fP.matForm == null) { matFormException(); }
            if (tP.matForm == null) { matFormException(); }
            // check there is anything to transition from or to
            if (fP.Count > 0 && tP.Count > 0)
            {
                // See if we lie in the material
                // Check end of this path and start of TP
                // For each see if it is safe in one Material Form
                // As we pull back to safe distance we allow a little wiggle.
                if ((
                    fP.matForm.intersect(fP.lastP, fP.matForm.safeDistance).thrDist > 0.0001
                    && tP.matForm.intersect(fP.lastP, tP.matForm.safeDistance).thrDist > 0.0001
                    ) || (
                    fP.matForm.intersect(tP.firstP, fP.matForm.safeDistance).thrDist > 0.0001
                    && tP.matForm.intersect(tP.firstP, tP.matForm.safeDistance).thrDist > 0.0001
                    ))
                {
                    // If in material we probably need to throw an error
                    // first path in an operation
                    double Length = fP.lastP.pt.DistanceTo(tP.firstP.pt);
                    if (first) { Co.addError("Transition between operations might be in material."); }
                    else if (Length > this.pathJump) // changing between paths in material
                    {
                        Co.addError("Long Transition between paths in material. \n"
                            + "To remove this error, don't use ignore, instead change PathJump for the machine from: "
                            + this.pathJump.ToString() + " to at least: " + Length.ToString());
                    }
                }
                else // Safely move from one safe point to another.
                {
                    ToolPath Move = tP.deepCloneWithNewPoints(new List<ToolPoint>());
                    Move.name = string.Empty;

                    List<Point3d> route = new List<Point3d>();
                    int i;
                    MFintersects inters;
                    MFintersects fromMid;

                    route.Add(fP.lastP.pt);
                    route.Add(tP.firstP.pt);

                    // loop through intersecting with safe bubble and adding points
                    for (i = 0; i < (route.Count - 1) && route.Count < 1000;)
                    {
                        if (tP.matForm.intersect(route[i], route[i + 1], tP.matForm.safeDistance, out inters))
                        {
                            fromMid = tP.matForm.intersect(inters.mid, inters.midOut, tP.matForm.safeDistance * 1.1);
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
                    double angSpread = this.angDiff(fP.lastP, tP.firstP,fP.matTool, lng);

                    int steps = (int)Math.Ceiling(30 * angSpread / (Math.PI * route.Count));
                    if (steps == 0) { steps = 1; } // Need to add at least one point even if angSpread is 0
                    int j;

                    // Try to build a path with angles. 
                    // If a tool line hits the material 
                    // switch to the longer rotate and try again

                    for (i = 0; i < (route.Count - 1); i++)
                    {
                        double shift;
                        // add new points at speed 0 to describe rapid move.
                        for (j = 0; j < steps; j++)
                        {
                            shift = (double)(steps * i + j) / (double)(steps * (route.Count - 1));
                            mixDir = this.interpolate(fP.lastP, tP.firstP, fP.matTool, shift,lng).dir;

                            ToolPoint newTP = new ToolPoint((j * route[i + 1] + (steps - j) * route[i]) / steps, mixDir, -1, 0);
                            if ((fP.matForm.intersect(newTP, 0).thrDist > 0
                                || tP.matForm.intersect(newTP, 0).thrDist > 0))
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
                                    angSpread = this.angDiff(fP.lastP, tP.firstP, fP.matTool, lng);
                                    steps = (int)Math.Ceiling(30 * angSpread / (Math.PI * route.Count));
                                    Move = tP.deepCloneWithNewPoints(new List<ToolPoint>());
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
                    writeCode(ref Co, Move);
                }
            }
        }
    }
}