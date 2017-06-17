using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types.MaterialForm;

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
    public class ToolPath : IToolPointContainer
    {
        public List<ToolPoint> Pts;     // Positions of the machine
        public MaterialTool MatTool;    // Material and tool to cut it with
        public IMaterialForm MatForm;    // Shape of the material
        public ToolPathAdditions Additions;       // Features we might add to the path 

        // Default Constructor, set everything to empty
        public ToolPath()
        {
            this.Pts = new List<ToolPoint>();
            this.MatTool = null;
            this.MatForm = null;
            this.Additions = new ToolPathAdditions();
        }
        // Just a MaterialTool
        public ToolPath(string name, MaterialTool MT)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.MatTool = MT;
            this.MatForm = null;
            this.Additions = new ToolPathAdditions();
        }
        // MaterialTool and features
        public ToolPath(string name, MaterialTool MT, ToolPathAdditions TPA)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.MatTool = MT;
            this.MatForm = null;
            this.Additions = new ToolPathAdditions(TPA);
        }
        // MaterialTool and Code
        public ToolPath(string name, MaterialTool MT, string Co)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.MatTool = MT;
            this.MatForm = null;
            this.localCode = Co;
            this.Additions = new ToolPathAdditions();
        }
        // MaterialTool, Code and features
        public ToolPath(string name, MaterialTool MT, string Co, ToolPathAdditions TPA)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.MatTool = MT;
            this.MatForm = null;
            this.localCode = Co;
            this.Additions = new ToolPathAdditions(TPA);
        }
        // MaterialTool and Form
        public ToolPath(string name, MaterialTool MT, IMaterialForm MF)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.MatTool = MT;
            this.MatForm = MF;
            this.Additions = new ToolPathAdditions();
        }
        // MaterialTool, Form and features
        public ToolPath(string name, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.MatTool = MT;
            this.MatForm = MF;
            this.Additions = new ToolPathAdditions(TPA);
        }
        // MaterialTool, Form and Code
        public ToolPath(string name, MaterialTool MT, IMaterialForm MF, string Co)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.MatTool = MT;
            this.MatForm = MF;
            this.localCode = Co;
            this.Additions = new ToolPathAdditions();
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
            foreach (ToolPoint pt in TP.Pts)
            {
                this.Pts.Add(new ToolPoint(pt));
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

        public string name
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public string localCode
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public bool IsValid
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override string ToString()
        {
            return "Toolpath with " + this.Pts.Count + " points.";
        }

        // Main functions

        // Write the code describing this path
        public ToolPoint WriteCode(ref CodeInfo Co, Machine M, ToolPoint beforePoint)
        {
            Co.AppendLine(M.SectionBreak);
            if (this.name != "")
            {
                Co.AppendLine(M.CommentChar + " ToolPath: " + this.name + M.endCommentChar);
                Co.AppendLine(M.SectionBreak);
            }
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

            // adjust path for three axis (or index three axis)
            if(this.Additions.threeAxisHeightOffset)
            {
                useTP = this.threeAxisHeightOffset(M);
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
                List<Vector3d> MatDir = new List<Vector3d>(); // list of tool directions
                List<Vector3d> MatNorm = new List<Vector3d>(); // list of surface normals
                Vector3d Dir, Norm;

                // ask the material form to refine the path

                ToolPath refPath = useTP.MatForm.Refine(useTP, M);

                foreach(ToolPoint TP in refPath.Pts)
                {
                    MatDist.Add(useTP.MatForm.MatDist(TP,M, useTP.MatTool, out Dir, out Norm)); // distance to material surface
                    //if (MatDist[MatDist.Count - 1] < 0) MatDist[MatDist.Count - 1] = 0; //avoid negative distances
                    MatDir.Add(new Vector3d(Dir));
                    MatNorm.Add(new Vector3d(Norm));
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


                    for(j = 0; j< refPath.Pts.Count && !end; j++)
                    {
                        if(i < NumSteps[j]) // We need to cut here
                        {
                            // if this is the first point to cut we need to add the previous one
                            // if there was one, so we do not miss what was between them
                            if(start && j > 0)
                            {
                                TPt = new ToolPoint(refPath.Pts[j - 1]);
                                height = useTP.MatTool.finishDepth;
                                if (height > MatDist[j-1] + useTP.MatForm.materialTolerance) height = 0;
                                TPt.Pt = MatDir[j - 1] * height + TPt.Pt; // stay finishDepth above final path

                                tempTP.Pts.Add(TPt);
                            }
                            height = MatDist[j] - CutLevel[i];
                            if (height < useTP.MatTool.finishDepth) height = useTP.MatTool.finishDepth; // stay finishDepth above final path
                            TPt = new ToolPoint(refPath.Pts[j]);
                            TPt.Pt = MatDir[j]*height + TPt.Pt;
                            tempTP.Pts.Add(TPt);
                            start = false;
                            droplength = 0;
                        } 
                        else if(start) // We have not hit any cutting yet;
                        {
                            if(!useTP.Additions.sdDropStart) // we are not dropping the start
                            {
                                TPt = new ToolPoint(refPath.Pts[j]);
                                height = useTP.MatTool.finishDepth;
                                if (height > MatDist[j] + useTP.MatForm.materialTolerance) height = 0;
                                TPt.Pt = MatDir[j] * height +TPt.Pt;
                                tempTP.Pts.Add(TPt);
                            } // otherwise we do nothing
                        }
                        else // We need to look ahead
                        {
                            for(k = j; k < refPath.Pts.Count && i >= NumSteps[k] ; k++); // Look ahead to the next cut

                            if(k == refPath.Pts.Count) // No more cutting required
                            {
                                if(useTP.Additions.sdDropEnd) // we are dropping the end
                                {
                                    // Add point as the previous one was deep, 
                                    // then set end to true so we finish
                                    TPt = new ToolPoint(refPath.Pts[j]);
                                    height = useTP.MatTool.finishDepth;
                                    if (height > MatDist[j] + useTP.MatForm.materialTolerance) height = 0;
                                    TPt.Pt = MatDir[j]*height + TPt.Pt;
                                    tempTP.Pts.Add(TPt);
                                    end = true;
                                } 
                                else // add point
                                {
                                    TPt = new ToolPoint(refPath.Pts[j]);
                                    height = useTP.MatTool.finishDepth;
                                    if (height > MatDist[j] + useTP.MatForm.materialTolerance) height = 0;
                                    TPt.Pt = MatDir[j]*height + TPt.Pt;
                                    tempTP.Pts.Add(TPt);
                                }
                            } 
                            else // into the middle
                            {
                                if(useTP.Additions.sdDropMiddle < 0 || (k - j) < 3) // we are not dropping middle or there are not enough points to justify it
                                {
                                    TPt = new ToolPoint(refPath.Pts[j]);
                                    height = useTP.MatTool.finishDepth;
                                    if (height > MatDist[j] + useTP.MatForm.materialTolerance) height = 0;
                                    TPt.Pt = MatDir[j]*height + TPt.Pt;
                                    tempTP.Pts.Add(TPt);
                                }
                                else //check length of drop
                                {
                                    if(droplength == 0) // If we are at the start of a possible drop Add the length until we hit the end or go over 
                                    {
                                        for(l = j; droplength < useTP.Additions.sdDropMiddle && l < k; l++) 
                                            droplength += refPath.Pts[l].Pt.DistanceTo(refPath.Pts[l+1].Pt);
                                    }
                                    if(droplength > useTP.Additions.sdDropMiddle) 
                                    {
                                        // add point, as previous point was in material
                                        TPt = new ToolPoint(refPath.Pts[j]);
                                        height = useTP.MatTool.finishDepth;
                                        if (height > MatDist[j] + useTP.MatForm.materialTolerance) height = 0;
                                        TPt.Pt = MatDir[j]*height + TPt.Pt;
                                        tempTP.Pts.Add(TPt);
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
                                        TPt = new ToolPoint(refPath.Pts[k - 1]);
                                        height = useTP.MatTool.finishDepth;
                                        if (height > MatDist[k-1] + useTP.MatForm.materialTolerance) height = 0;
                                        TPt.Pt = MatDir[k-1]*height + TPt.Pt;
                                        tempTP.Pts.Add(TPt);
                                        j = k - 1; //set j to k-1 so it deals with the k point next
                                      
                                    }
                                    else // after all that we still need to add the point
                                    {
                                        TPt = new ToolPoint(refPath.Pts[j]);
                                        height = useTP.MatTool.finishDepth;
                                        if (height > MatDist[j] + useTP.MatForm.materialTolerance) height = 0;
                                        TPt.Pt = MatDir[j]*height + TPt.Pt;
                                        tempTP.Pts.Add(TPt);
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
                    NewPaths[i][j] = useTP.MatForm.InsertRetract(NewPaths[i][j], M);

            return NewPaths;
        }

        // Adjust the path so it will not be gouged when cut in 3-axis, or indexed 3-axis mode.
        // TODO make this guarantee that it does not gouge locally. There is a problem 
        // with paths that are steep down, followed by some bottom moves followed by steep out. 
        public ToolPath threeAxisHeightOffset(Machine M)
        {
            List<ToolPoint> offsetPath = new List<ToolPoint>();

            Vector3d dir = (Vector3d)(this.Pts[1].Pt -this.Pts[0].Pt); 
            dir.Unitize();

            ToolPoint point;
            Vector3d orth = new Vector3d(0,0,0);
            bool orthSet = false;

            if (dir == this.Pts[0].Dir) point = this.Pts[0];
            else
            {
                orth = Vector3d.CrossProduct(dir, this.Pts[0].Dir);
                point = this.MatTool.threeAxisHeightOffset(this.Pts[0], dir,orth, M);
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

            for (int i = 1; i < this.Pts.Count-1;i++ )
            {
                // Keep track of tool point direction to warn if it changes (but only once)
                if(this.Pts[i-1].Dir != this.Pts[i].Dir && !changeDirection)
                {
                    this.Pts[i].warning.Add("Height offsetting is not designed for 5-Axis motion, it may be unpredictable.");
                    changeDirection = true;
                }
                // Find the next offset line
                dir = (Vector3d)(this.Pts[i+1].Pt - this.Pts[i].Pt);
                // Check for vertical moves
                if (dir == this.Pts[i].Dir)
                {
                    if (orthSet) nextPoint = this.MatTool.threeAxisHeightOffset(this.Pts[i], dir, orth, M);
                    else nextPoint = this.Pts[i];
                }
                else
                {
                    orth = Vector3d.CrossProduct(dir, this.Pts[i].Dir);
                    orthSet = true;
                    nextPoint = this.MatTool.threeAxisHeightOffset(this.Pts[i], dir, orth, M);
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
                    startCP = (ToolPoint)this.Pts[i].Duplicate();
                    startCP.Pt = osLines[osLines.Count - 1].PointAt(nextinter);
                    offsetPath.Add(startCP);
                    
                }
                else
                {
                    // Add the new intersection we like using the closest points on the two lines (the points on each line closest to the other line)
                    // note that we keep the information from the toolpoint before the line we are going to be cutting
                    // this might be removed on later passes, if the line is removed.
                    endCP = (ToolPoint)this.Pts[i].Duplicate();
                    endCP.Pt = osLines[osLines.Count - 2].PointAt(inter);
                    startCP = (ToolPoint)this.Pts[i].Duplicate();
                    startCP.Pt = osLines[osLines.Count - 1].PointAt(nextinter);

                    // take the midpoint of the two intersections
                    // there is possibly something clever to do here
                    startCP.Pt = (startCP.Pt + endCP.Pt) / 2;

                    offsetPath.Add(startCP);
                    //offsetPath.Add(endCP);
                }
            }

            // add the final point.

            if (dir == this.Pts[this.Pts.Count-1].Dir)
            {
                if (orthSet) nextPoint = this.MatTool.threeAxisHeightOffset(this.Pts[this.Pts.Count - 1], dir, orth, M);
                else nextPoint = this.Pts[this.Pts.Count - 1];
            }
            else
            {
                orth = Vector3d.CrossProduct(dir, this.Pts[this.Pts.Count - 1].Dir);
                nextPoint = this.MatTool.threeAxisHeightOffset(this.Pts[this.Pts.Count - 1], dir, orth, M);
            }

            offsetPath.Add(nextPoint);

            ToolPath retPath = this.copyWithNewPoints(offsetPath);
            retPath.Additions.threeAxisHeightOffset = false;

            if (!retPath.Additions.insert)
                retPath.Pts[0].warning.Add("Height Offsetting does not work between ToolPaths. This might cause unexpected behaviour.");
            if (!retPath.Additions.retract)
                retPath.Pts[retPath.Pts.Count-1].warning.Add("Height Offsetting does not work between ToolPaths. This might cause unexpected behaviour.");

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

            if (this.Pts.Count == 0 || TP.Pts.Count == 0)
            {
                throw new ArgumentOutOfRangeException("Toolpath","Trying to transition to or from an empty path.");
            }

            ToolPath TransPath;
            Length = this.Pts[this.Pts.Count - 1].Pt.DistanceTo(TP.Pts[0].Pt);

            // See if we lie in the material
            // Check end of this path and start of TP
            // For each see if it is safe in one Material Form
            // As we pull back to safe distance we allow a little wiggle.

            if ((this.MatForm.SafePoint(this.Pts[this.Pts.Count - 1]) < -0.0001 && TP.MatForm.SafePoint(this.Pts[this.Pts.Count - 1]) < -0.0001)
                || (this.MatForm.SafePoint(TP.Pts[0]) < -0.0001 && TP.MatForm.SafePoint(TP.Pts[0]) < -0.0001))
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

            foreach (ToolPoint tP in this.Pts)
                Points.Add(tP.Pt);
            return Points;
        }

        // Get the list of tool directions
        public List<Vector3d> GetDirs()
        {
            List<Vector3d> Dirs = new List<Vector3d>();

            foreach (ToolPoint tP in this.Pts)
                Dirs.Add(tP.Dir);
            return Dirs;
        }

        // Get the list of speeds and feeds (a vector with speed in X and feed in Y)
        public List<Vector3d> GetSpeedFeed()
        {
            List<Vector3d> SF = new List<Vector3d>();

            foreach (ToolPoint tP in this.Pts)
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
                this.Pts.Add(TPt);
            }
            return true;
        }

        ICAMel_Base ICAMel_Base.Duplicate()
        {
            return new ToolPath(this);
        }
    }

    // Grasshopper Type Wrapper
    public class GH_ToolPath : GH_ToolPointContainer<ToolPath>
    {
        // Default Constructor
        public GH_ToolPath()
        {
            this.Value = new ToolPath();
        }
        // Just a list of points and MaterialTool
        public GH_ToolPath(string name, MaterialTool MT)
        {
            this.Value = new ToolPath(name, MT);
        }
        // Just a list of points, MaterialTool and features
        public GH_ToolPath(string name, MaterialTool MT, ToolPathAdditions TPA)
        {
            this.Value = new ToolPath(name, MT, TPA);
        }
        // Points, MaterialTool and Code
        public GH_ToolPath(string name, MaterialTool MT, string Co)
        {
            this.Value = new ToolPath(name, MT, Co);
        }
        // Points, MaterialTool, Code and features
        public GH_ToolPath(string name, MaterialTool MT, string Co, ToolPathAdditions TPA)
        {
            this.Value = new ToolPath(name, MT, Co, TPA);
        }
        // Points, MaterialTool and Form
        public GH_ToolPath(string name, MaterialTool MT, IMaterialForm MF)
        {
            this.Value = new ToolPath(name, MT, MF);
        }
        // Points, MaterialTool, Form and features
        public GH_ToolPath(string name, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA)
        {
            this.Value = new ToolPath(name, MT, MF, TPA);
        }
        // Points, MaterialTool, Form and Code
        public GH_ToolPath(string name, MaterialTool MT, IMaterialForm MF, string Co)
        {
            this.Value = new ToolPath(name, MT, MF, Co);
        }
        // Points, MaterialTool, Form, Code and features
        public GH_ToolPath(string name, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA, string Co)
        {
            this.Value = new ToolPath(name, MT, MF, TPA, Co);
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
            if (typeof(Q).IsAssignableFrom(typeof(List<Point3d>)))
            {
                List<Point3d> li = new List<Point3d>();
                foreach (ToolPoint p in this.Value.Pts)
                {
                    li.Add(p.Pt);
                }
                object ptr = li;
                target = (Q)ptr;
                return true;
            }

            if (typeof(Q).IsAssignableFrom(typeof(ToolPath)))
            {
                object ptr = this.Value;
                target = (Q)ptr;
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