using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types.MaterialForm;
using System.Collections;

namespace CAMel.Types
{
    // Features we might add to the path
    public class ToolPathAdditions
    {
        public bool insert;
        public bool retract;
        public bool stepDown;
        public bool threeAxisHeightOffset;
        public bool sdDropStart;    // How stepdown will deal with 
        public double sdDropMiddle; // points that have reached  
        public bool sdDropEnd;      // the required depth (Middle is dropped if length greater than value);

        // Force creation before setting variables so 
        public ToolPathAdditions()
        {
            this.insert = false;
            this.retract = false;
            this.stepDown = false;
            this.sdDropStart = false;
            this.sdDropMiddle = 0;
            this.sdDropEnd = false;
            this.threeAxisHeightOffset = false;
        }

        public ToolPathAdditions(ToolPathAdditions TPA)
        {
            this.insert = TPA.insert;
            this.retract = TPA.retract;
            this.stepDown = TPA.stepDown;
            this.sdDropStart = TPA.sdDropStart;
            this.sdDropMiddle = TPA.sdDropMiddle;
            this.sdDropEnd = TPA.sdDropEnd;
            this.threeAxisHeightOffset = TPA.threeAxisHeightOffset;
        }

        public bool any
        {
            get { return insert || retract || stepDown || threeAxisHeightOffset; }
        }
    }

    // One action of the machine, such as cutting a line
    public class ToolPath : IList<ToolPoint> ,IToolPointContainer
    {
        private List<ToolPoint> Pts;     // Positions of the machine
        public MaterialTool MatTool { get; set; }   // Material and tool to cut it with
        public IMaterialForm MatForm { get; set; }    // Shape of the material
        public ToolPathAdditions Additions { get; set; }       // Features we might add to the path 

        // Default Constructor, set everything to empty
        public ToolPath()
        {
            this.Pts = new List<ToolPoint>();
            this.MatTool = null;
            this.MatForm = null;
            this.Additions = new ToolPathAdditions();
            this.localCode = "";
        }
        // Just a MaterialTool
        public ToolPath(string name, MaterialTool MT)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.MatTool = MT;
            this.MatForm = null;
            this.Additions = new ToolPathAdditions();
            this.localCode = "";
        }
        // MaterialTool and features
        public ToolPath(string name, MaterialTool MT, ToolPathAdditions TPA)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.MatTool = MT;
            this.MatForm = null;
            this.Additions = new ToolPathAdditions(TPA);
            this.localCode = "";
        }
        // MaterialTool and Form
        public ToolPath(string name, MaterialTool MT, IMaterialForm MF)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.MatTool = MT;
            this.MatForm = MF;
            this.Additions = new ToolPathAdditions();
            this.localCode = "";
        }
        // MaterialTool, Form and features
        public ToolPath(string name, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.MatTool = MT;
            this.MatForm = MF;
            this.Additions = new ToolPathAdditions(TPA);
            this.localCode = "";
        }
        // MaterialTool, Form, Code and features
        public ToolPath(string name, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA, string Co)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.MatTool = MT;
            this.MatForm = MF;
            this.localCode = Co;
            this.Additions = new ToolPathAdditions(TPA);
        }
        // Copy Constructor
        public ToolPath(ToolPath TP)
        {
            this.name = TP.name;
            this.Pts = new List<ToolPoint>();
            foreach (ToolPoint pt in TP)
            {
                this.Add(new ToolPoint(pt));
            }
            this.MatTool = new MaterialTool(TP.MatTool);
            this.MatForm = (IMaterialForm) TP.MatForm.Duplicate();
            this.localCode = TP.localCode;
            this.Additions = new ToolPathAdditions(TP.Additions);
        }

        public ToolPath copyWithNewPoints(List<ToolPoint> Pts)
        {
            ToolPath newTP = new ToolPath(this.name,this.MatTool,this.MatForm,this.Additions,this.localCode);
            newTP.Pts = Pts;
            return newTP;
        }

        public string TypeDescription
        {
            get { return "An action of the machine, for example cutting a single line"; }
        }

        public string TypeName
        {
            get { return "ToolPath"; }
        }

        public string name { get; set; }

        public string localCode { get; set; }

        public bool IsValid
        {
            get
            {
                throw new NotImplementedException("ToolPath has not implemented IsValid");
            }
        }

        public int Count => ((IList<ToolPoint>)Pts).Count;

        public bool IsReadOnly => ((IList<ToolPoint>)Pts).IsReadOnly;

        public ToolPoint this[int index] { get => ((IList<ToolPoint>)Pts)[index]; set => ((IList<ToolPoint>)Pts)[index] = value; }

        public override string ToString()
        {
            return "Toolpath with " + this.Count + " points.";
        }

        // Main functions

        // Write the code describing this path
        public ToolPoint WriteCode(ref CodeInfo Co, Machine M, ToolPoint beforePoint)
        {
            Co.AppendLine(M.SectionBreak);
            bool preamble = false;
            if (this.name != "")
            {
                Co.AppendComment(" ToolPath: " + this.name);
                preamble = true;
            }
            if (Co.currentMT==null || this.MatTool.Tool_name != Co.currentMT.Tool_name)
            {
                Co.AppendComment(" using: " + this.MatTool.Tool_name + " into " + this.MatTool.Mat_name);
                Co.currentMT = this.MatTool;
                preamble = true;
            }
            if (Co.currentMF==null || this.MatForm.ToString() != Co.currentMF.ToString())
            {
                Co.AppendComment(" material: " + this.MatForm.ToString());
                Co.currentMF = this.MatForm;
                preamble = true;
            }

            if (preamble) { Co.AppendLine(M.SectionBreak); }

            if (this.localCode != "") Co.Append(this.localCode);

            if (this.Additions.any)
                throw new InvalidOperationException("Cannot write Code for toolpaths with unprocessed additions (such as step down or insert and retract moves.\n");

            return M.WriteCode(ref Co, this, beforePoint);
        }
        // Process any additions to the path and return 
        // list of list of toolpaths (for stepdown)
        public List<List<ToolPath>> ProcessAdditions(Machine M)
        {
            int i, j, k, l;
            List<List<ToolPath>> NewPaths = new List<List<ToolPath>>();
            // need a list for each step down as it might split into more than one path
            // and we need to keep those together to coordinate the Machine Operation

            ToolPath useTP;

            // make sure the directions are correct for the machine

            foreach(ToolPoint tp in this)
            {
                tp.Dir = M.ToolDir(tp);
            }

            // adjust path for three axis (or index three axis)
            if(this.Additions.threeAxisHeightOffset)
            {
                useTP = this.threeAxisHeightOffset();
            }
            else
            {
                useTP = this;
            }

            // add steps into material
            if(this.Additions.stepDown)
            {
                // Use the material form to work out the distance to cut in the
                // material, the direction to enter the material and the number of passes.
                List<double> MatDist = new List<double>();
                List<int> NumSteps = new List<int>();
                int MaxSteps = 0; // Maximum distance of all points. 
                List<Vector3d> MatNorm = new List<Vector3d>(); // list of surface normals

                // ask the material form to refine the path

                ToolPath refPath = useTP.MatForm.refine(useTP, M);
                MaterialForm.intersection inter;

                foreach(ToolPoint TP in refPath)
                {
                    inter = useTP.MatForm.intersect(TP, 0).through;
                    MatDist.Add(inter.lineP); // distance to material surface
                    if (MatDist[MatDist.Count - 1] < 0) MatDist[MatDist.Count - 1] = 0; // avoid negative distances (outside material)
                    MatNorm.Add(new Vector3d(inter.Away));
                    // calculate maximum number of cutDepth height steps down to finishDepth above material
                    NumSteps.Add((int)Math.Ceiling((MatDist[MatDist.Count - 1]-useTP.MatTool.finishDepth)/useTP.MatTool.cutDepth));
                    if(NumSteps[NumSteps.Count - 1] > MaxSteps) MaxSteps = NumSteps[NumSteps.Count - 1];
                }

                // make a list of depths to cut at.
                // This just steps down right now, but makes it easier to add fancier levelling, if ever worthwhile. 
                // Note that maxsteps currently assumes only stepping down by cutDepth.

                List<double> CutLevel = new List<double>();
                for (i = 0; i < MaxSteps; i++) CutLevel.Add((i+1)*useTP.MatTool.cutDepth);

                // process the paths, staying away from the final cut

                ToolPoint TPt;
                bool start;
                bool end;
                double droplength; // length of dropped curve in the middle of a path
                double height; // height above final path

                ToolPath tempTP;

                for(i = 0; i < CutLevel.Count;i++)
                {
                    NewPaths.Add(new List<ToolPath>());
                    tempTP = (ToolPath)useTP.copyWithNewPoints(new List<ToolPoint>());
                    tempTP.name = useTP.name + " Pass " + (i+1).ToString();
                    tempTP.Additions.stepDown = false;

                    start = true;
                    end = false;
                    droplength = 0;

                    for(j = 0; j< refPath.Count && !end; j++)
                    {
                        if(i < NumSteps[j]) // We need to cut here
                        {
                            // if this is the first point to cut we need to add the previous one
                            // if there was one, so we do not miss what was between them
                            if(start && j > 0)
                            {
                                TPt = new ToolPoint(refPath[j - 1]);
                                height = useTP.MatTool.finishDepth;
                                if (height > MatDist[j-1] ) height = 0;
                                TPt.Pt = -TPt.Dir * height + TPt.Pt; // stay finishDepth above final path

                                tempTP.Add(TPt);
                            }
                            height = MatDist[j] - CutLevel[i];
                            if (height < useTP.MatTool.finishDepth) height = useTP.MatTool.finishDepth; // stay finishDepth above final path
                            TPt = new ToolPoint(refPath[j]);
                            TPt.Pt = -TPt.Dir * height + TPt.Pt;
                            tempTP.Add(TPt);
                            start = false;
                            droplength = 0;
                        } 
                        else if(start) // We have not hit any cutting yet;
                        {
                            if(!useTP.Additions.sdDropStart) // we are not dropping the start
                            {
                                TPt = new ToolPoint(refPath[j]);
                                height = useTP.MatTool.finishDepth;
                                if (height > MatDist[j] ) height = 0;
                                TPt.Pt = -TPt.Dir * height +TPt.Pt;
                                tempTP.Add(TPt);
                            } // otherwise we do nothing
                        }
                        else // We need to look ahead
                        {
                            for(k = j; k < refPath.Count && i >= NumSteps[k] ; k++); // Look ahead to the next cut

                            if(k == refPath.Count) // No more cutting required
                            {
                                if(useTP.Additions.sdDropEnd) // we are dropping the end
                                {
                                    // Add point as the previous one was deep, 
                                    // then set end to true so we finish
                                    TPt = new ToolPoint(refPath[j]);
                                    height = useTP.MatTool.finishDepth;
                                    if (height > MatDist[j] ) height = 0;
                                    TPt.Pt = -TPt.Dir *height + TPt.Pt;
                                    tempTP.Add(TPt);
                                    end = true;
                                } 
                                else // add point
                                {
                                    TPt = new ToolPoint(refPath[j]);
                                    height = useTP.MatTool.finishDepth;
                                    if (height > MatDist[j] ) height = 0;
                                    TPt.Pt = -TPt.Dir *height + TPt.Pt;
                                    tempTP.Add(TPt);
                                }
                            } 
                            else // into the middle
                            {
                                if(useTP.Additions.sdDropMiddle < 0 || (k - j) < 3) // we are not dropping middle or there are not enough points to justify it
                                {
                                    TPt = new ToolPoint(refPath[j]);
                                    height = useTP.MatTool.finishDepth;
                                    if (height > MatDist[j] ) height = 0;
                                    TPt.Pt = -TPt.Dir * height + TPt.Pt;
                                    tempTP.Add(TPt);
                                }
                                else //check length of drop
                                {
                                    if(droplength == 0) // If we are at the start of a possible drop Add the length until we hit the end or go over 
                                    {
                                        for(l = j; droplength < useTP.Additions.sdDropMiddle && l < k; l++) 
                                            droplength += refPath[l].Pt.DistanceTo(refPath[l+1].Pt);
                                    }
                                    if(droplength > useTP.Additions.sdDropMiddle) 
                                    {
                                        // add point, as previous point was in material
                                        TPt = new ToolPoint(refPath[j]);
                                        height = useTP.MatTool.finishDepth;
                                        if (height > MatDist[j] ) height = 0;
                                        TPt.Pt = -TPt.Dir *height + TPt.Pt;
                                        tempTP.Add(TPt);
                                        // leap forward cut path and start a new one
                                        // giving settings to add inserts and retracts
                                        
                                        tempTP.Additions.retract = true;
                                        NewPaths[NewPaths.Count-1].Add(tempTP); // add path and create a new one

                                        tempTP = (ToolPath)useTP.copyWithNewPoints(new List<ToolPoint>());
                                        tempTP.name = useTP.name + " Continuing Pass " + i.ToString();
                                        tempTP.Additions.insert = true;
                                        tempTP.Additions.stepDown = false;

                                        // add k-1 point as k is deep
                                        // this will not result in a double point as we checked (k-j) >=3
                                        TPt = new ToolPoint(refPath[k - 1]);
                                        height = useTP.MatTool.finishDepth;
                                        if (height > MatDist[k-1] ) height = 0;
                                        TPt.Pt = -TPt.Dir * height + TPt.Pt;
                                        tempTP.Add(TPt);
                                        j = k - 1; //set j to k-1 so it deals with the k point next
                                      
                                    }
                                    else // after all that we still need to add the point
                                    {
                                        TPt = new ToolPoint(refPath[j]);
                                        height = useTP.MatTool.finishDepth;
                                        if (height > MatDist[j] ) height = 0;
                                        TPt.Pt = -TPt.Dir * height + TPt.Pt;
                                        tempTP.Add(TPt);
                                    }
                                }
                            }
                        }
                    } 
                    NewPaths[NewPaths.Count-1].Add(tempTP);
                }
            }

            // add a copy of the original path, making sure step down is false
            // if passes were added, rename with "Final Pass"

            NewPaths.Add(new List<ToolPath>());
            NewPaths[NewPaths.Count-1].Add(new ToolPath(useTP));
            NewPaths[NewPaths.Count-1][0].Additions.stepDown = false;
            if (NewPaths.Count > 1) NewPaths[NewPaths.Count - 1][0].name = useTP.name + " Final Pass";

            // add insert and retract moves

            for (i = 0; i < NewPaths.Count; i++)
                for (j = 0; j < NewPaths[i].Count;j++ )
                    NewPaths[i][j] = useTP.MatForm.InsertRetract(NewPaths[i][j]);

            return NewPaths;
        }
        
        // Adjust the path so it will not be gouged when cut in 3-axis, or indexed 3-axis mode.
        // TODO make this guarantee that it does not gouge locally. There is a problem 
        // with paths that are steep down, followed by some bottom moves followed by steep out. 
        public ToolPath threeAxisHeightOffset()
        {
            List<ToolPoint> offsetPath = new List<ToolPoint>();

            Vector3d dir = (Vector3d)(this[1].Pt -this[0].Pt); 
            dir.Unitize();

            ToolPoint point;
            Vector3d orth = new Vector3d(0,0,0);
            bool orthSet = false;

            if (dir == this[0].Dir) point = this[0];
            else
            {
                orth = Vector3d.CrossProduct(dir, this[0].Dir);
                point = this.MatTool.threeAxisHeightOffset(this[0], dir,orth);
                orthSet = true;
            }

            List<Line> osLines = new List<Line>();
            osLines.Add(new Line(point.Pt, dir));

            double inter;
            ToolPoint nextPoint;
            double nextinter;
            double orient;
            ToolPoint endCP, startCP;

            bool changeDirection = false; // Has tool direction changed?

            offsetPath.Add(point);

            // loop through the lines of the toolpath finding their offset path 
            // and then travelling to the closest point to the next offset path

            for (int i = 1; i < this.Count-1;i++ )
            {
                // Keep track of tool point direction to warn if it changes (but only once)
                if(this[i-1].Dir != this[i].Dir && !changeDirection)
                {
                    this[i].warning.Add("Height offsetting is not designed for 5-Axis motion, it may be unpredictable.");
                    changeDirection = true;
                }
                // Find the next offset line
                dir = (Vector3d)(this[i+1].Pt - this[i].Pt);
                // Check for vertical moves
                if (dir == this[i].Dir)
                {
                    if (orthSet) nextPoint = this.MatTool.threeAxisHeightOffset(this[i], dir, orth);
                    else nextPoint = this[i];
                }
                else
                {
                    orth = Vector3d.CrossProduct(dir, this[i].Dir);
                    orthSet = true;
                    nextPoint = this.MatTool.threeAxisHeightOffset(this[i], dir, orth);
                }

                // find the next line we will travel along
                osLines.Add(new Line(nextPoint.Pt, dir));

                // we need to find the last path that does not reverse when we travel along our new line. 
                // if we go in the wrong direction on an offset path then we are gouging back into previously cut material.
                // In the following we discuss intersection, for lines in 3d this is given by the closest point for two lines.

                // intersect the new line with the last line we used
                Rhino.Geometry.Intersect.Intersection.LineLine(osLines[osLines.Count - 2], osLines[osLines.Count - 1], out inter, out nextinter);
                // find the orientation of the new path
                orient = (osLines[osLines.Count - 2].PointAt(inter) - offsetPath[offsetPath.Count - 1].Pt) * osLines[osLines.Count - 2].UnitTangent;

                // loop until we find a suitable line, removing previous points that are now problematic
                // checking the length of offsetPath should ensure we don't try to go past the start 
                // and osLines is always at least 2 long, but we check both just in case.
                while(orient < 0 && offsetPath.Count > 1 && osLines.Count > 1)
                {
                    // remove the reversing line
                    osLines.RemoveAt(osLines.Count - 2);
                    // remove the last point on the offsetPath, which were given by the intersection we are removing
                    offsetPath.RemoveRange(offsetPath.Count - 1, 1);
                    // find the new intersection and orientation
                    Rhino.Geometry.Intersect.Intersection.LineLine(osLines[osLines.Count - 2], osLines[osLines.Count - 1], out inter, out nextinter);
                    orient = (osLines[osLines.Count - 2].PointAt(inter) - offsetPath[offsetPath.Count - 1].Pt) * osLines[osLines.Count - 2].UnitTangent;              
                }

                // if we got to the start and things are still bad we have to deal with things differently
                if (orient < 0)
                {
                    // remove the old start point and add the closest point on the new first line
                    offsetPath.RemoveAt(0);

                    // intersect our new line with the first direction 
                    Rhino.Geometry.Intersect.Intersection.LineLine(osLines[0], osLines[osLines.Count - 1], out inter, out nextinter);

                    // note that we keep the information from the toolpoint before the line we are going to be cutting
                    // this might be removed on later passes, if the line is removed.
                    startCP = (ToolPoint)this[i].Duplicate();
                    startCP.Pt = osLines[osLines.Count - 1].PointAt(nextinter);
                    offsetPath.Add(startCP);
                    
                }
                else
                {
                    // Add the new intersection we like using the closest points on the two lines (the points on each line closest to the other line)
                    // note that we keep the information from the toolpoint before the line we are going to be cutting
                    // this might be removed on later passes, if the line is removed.
                    endCP = (ToolPoint)this[i].Duplicate();
                    endCP.Pt = osLines[osLines.Count - 2].PointAt(inter);
                    startCP = (ToolPoint)this[i].Duplicate();
                    startCP.Pt = osLines[osLines.Count - 1].PointAt(nextinter);

                    // take the midpoint of the two intersections
                    // there is possibly something clever to do here
                    startCP.Pt = (startCP.Pt + endCP.Pt) / 2;

                    offsetPath.Add(startCP);
                    //offsetPath.Add(endCP);
                }
            }

            // add the final point.

            if (dir == this[this.Count-1].Dir)
            {
                if (orthSet) nextPoint = this.MatTool.threeAxisHeightOffset(this[this.Count - 1], dir, orth);
                else nextPoint = this[this.Count - 1];
            }
            else
            {
                orth = Vector3d.CrossProduct(dir, this[this.Count - 1].Dir);
                nextPoint = this.MatTool.threeAxisHeightOffset(this[this.Count - 1], dir, orth);
            }

            offsetPath.Add(nextPoint);

            ToolPath retPath = this.copyWithNewPoints(offsetPath);
            retPath.Additions.threeAxisHeightOffset = false;

            if (!retPath.Additions.insert)
                retPath[0].warning.Add("Height Offsetting does not work between ToolPaths. This might cause unexpected behaviour.");
            if (!retPath.Additions.retract)
                retPath[retPath.Count-1].warning.Add("Height Offsetting does not work between ToolPaths. This might cause unexpected behaviour.");

            return retPath;
        }

        // Move safely from one toolpath to another.
        // We assume that the safe space to move is the union 
        // of the safe spaces from both material forms.
        public ToolPath TransitionTo(Machine M, ToolPath TP, out bool inMaterial, out double Length)
        {
            // Throw error if either toolpath is empty
            // This is not generally a problem but does 
            // not give the information we need here and 
            // could be dangerous.

            if (this.Count == 0 || TP.Count == 0)
            {
                throw new ArgumentOutOfRangeException("Toolpath","Trying to transition to or from an empty path.");
            }

            ToolPath TransPath;
            Length = this[this.Count - 1].Pt.DistanceTo(TP[0].Pt);

            // See if we lie in the material
            // Check end of this path and start of TP
            // For each see if it is safe in one Material Form
            // As we pull back to safe distance we allow a little wiggle.

            if ((
                this.MatForm.intersect(this[this.Count - 1], this.MatForm.safeDistance).thrDist > 0.0001 
                && TP.MatForm.intersect(this[this.Count-1], TP.MatForm.safeDistance).thrDist > 0.0001
                ) || (
                this.MatForm.intersect(TP[0], this.MatForm.safeDistance).thrDist > 0.0001
                && TP.MatForm.intersect(TP[0], TP.MatForm.safeDistance).thrDist > 0.0001
               ))
            {
                inMaterial = true;
                TransPath = TP.copyWithNewPoints(new List<ToolPoint>());
                TransPath.name = "";
            }
            else
            {
                // Throw the challenge to the Machine which knows what to do
                // Machine can also throw in a tool change.
                inMaterial = false;
                TransPath = M.SafeMove(this, TP);
            }

            return TransPath;
        }

        // Get the list of tooltip locations
        public List<Point3d> GetPoints()
        {
            List<Point3d> Points = new List<Point3d>();

            foreach (ToolPoint tP in this)
                Points.Add(tP.Pt);
            return Points;
        }

        // Get the list of tool directions
        public List<Vector3d> GetDirs()
        {
            List<Vector3d> Dirs = new List<Vector3d>();

            foreach (ToolPoint tP in this)
                Dirs.Add(tP.Dir);
            return Dirs;
        }
        // Create a path with the points 
        public List<Point3d> GetPointsandDirs(out List<Vector3d> Dirs)
        {
            List<Point3d> Ptsout = new List<Point3d>();
            Dirs = new List<Vector3d>();
            foreach (ToolPoint P in this)
            {
                Ptsout.Add(P.Pt);
                Dirs.Add(P.Dir);
            }
            return Ptsout;
        }

        // Get the list of speeds and feeds (a vector with speed in X and feed in Y)
        public List<Vector3d> GetSpeedFeed()
        {
            List<Vector3d> SF = new List<Vector3d>();

            foreach (ToolPoint tP in this)
                SF.Add(new Vector3d(tP.speed,tP.feed,0));
            return SF;
        }

        // Use a curve and direction vector to create a path of toolpoints
        public bool ConvertCurve(Curve c, Vector3d d)
        {
            // Create polyline approximation
            Polyline PL;
            ToolPoint TPt;

            // Check we are dealing with a valid curve.

            if (c != null && c.IsValid)
                c.ToPolyline(0, 5000, Math.PI, 0, 0, this.MatTool.tolerance, this.MatTool.minStep, this.MatTool.toolWidth / 4, true).TryGetPolyline(out PL);
            else
                return false;


            this.Pts = new List<ToolPoint>();

            // Add the points to the Path

            foreach(Point3d Pt in PL)
            {
                TPt = new ToolPoint(Pt, d);
                this.Add(TPt);
            }
            return true;
        }

        ICAMel_Base ICAMel_Base.Duplicate()
        {
            return new ToolPath(this);
        }

        public int IndexOf(ToolPoint item)
        {
            return ((IList<ToolPoint>)Pts).IndexOf(item);
        }

        public void Insert(int index, ToolPoint item)
        {
            ((IList<ToolPoint>)Pts).Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            ((IList<ToolPoint>)Pts).RemoveAt(index);
        }

        public void Add(ToolPoint item)
        {
            ((IList<ToolPoint>)Pts).Add(item);
        }

        public void Clear()
        {
            ((IList<ToolPoint>)Pts).Clear();
        }

        public bool Contains(ToolPoint item)
        {
            return ((IList<ToolPoint>)Pts).Contains(item);
        }

        public void CopyTo(ToolPoint[] array, int arrayIndex)
        {
            ((IList<ToolPoint>)Pts).CopyTo(array, arrayIndex);
        }

        public bool Remove(ToolPoint item)
        {
            return ((IList<ToolPoint>)Pts).Remove(item);
        }

        public IEnumerator<ToolPoint> GetEnumerator()
        {
            return ((IList<ToolPoint>)Pts).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IList<ToolPoint>)Pts).GetEnumerator();
        }
    }

    // Grasshopper Type Wrapper
    public class GH_ToolPath : GH_ToolPointContainer<ToolPath>, IGH_PreviewData
    {
        public BoundingBox ClippingBox
        {
            get
            {
                BoundingBox BB = new Polyline(Value.GetPoints()).BoundingBox;
                BB.Inflate(1);
                return BB; 
            }
        }
        // Default Constructor
        public GH_ToolPath()
        {
            this.Value = new ToolPath();
        }
        // Create from unwrapped version
        public GH_ToolPath(ToolPath TP)
        {
            this.Value = new ToolPath(TP);
        }
        // Copy Constructor
        public GH_ToolPath(GH_ToolPath TP)
        {
            this.Value = new ToolPath(TP.Value);
        }
        // Duplicate
        public override IGH_Goo Duplicate()
        {
            return new GH_ToolPath(this);
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(Polyline)))
            {
                target = (Q)(object) new Polyline(Value.GetPoints());
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(Curve)))
            {
                target = (Q)(object)new PolylineCurve(Value.GetPoints());
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_Curve)))
            {
                Curve C = (Curve)new PolylineCurve(Value.GetPoints());
                target = (Q)(object)new GH_Curve(C);
                return true;
            }

            if (typeof(Q).IsAssignableFrom(typeof(ToolPath)))
            {
                target = (Q)(object)Value;
                return true;
            }
            return false;
        }

        public override bool CastFrom(object source)
        {
            if( source is ToolPath)
            {
                this.Value = new ToolPath((ToolPath)source);
                return true;
            }
            return false;
        }

        public void DrawViewportWires(GH_PreviewWireArgs args)
        {
            args.Pipeline.DrawCurve(new PolylineCurve(Value.GetPoints()), args.Color);
        }

        public void DrawViewportMeshes(GH_PreviewMeshArgs args) { }
    }

    // Grasshopper Parameter Wrapper
    public class GH_ToolPathPar : GH_Param<GH_ToolPath>
    {
        public GH_ToolPathPar() :
            base("ToolPath", "ToolPath", "Contains a collection of Tool Paths", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid
        {
            get { return new Guid("4ea6da38-c19f-43e7-85d4-ada4716c06ac"); }
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
                return Properties.Resources.toolpath;
            }
        }
    }
}