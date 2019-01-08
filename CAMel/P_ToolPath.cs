using System;
using System.Collections;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;

using CAMel.Types.MaterialForm;
using CAMel.Types.Machine;
using static CAMel.Exceptions;

namespace CAMel.Types
{

    // One action of the machine, such as cutting a line
    public class ToolPath : IList<ToolPoint> ,IToolPointContainer
    {
        private List<ToolPoint> Pts;     // Positions of the machine
        public MaterialTool matTool { get; set; }   // Material and tool to cut it with
        public IMaterialForm matForm { get; set; }    // Shape of the material
        public ToolPathAdditions Additions;       // Features we might add to the path 

        public ToolPoint firstP
        {
            get
            {
                if (this.Count > 0) { return this[0]; }
                else { return null; }
            }
        }
        public ToolPoint lastP
        {
            get
            {
                if (this.Count > 0) { return this[this.Count-1]; }
                else { return null; }
            }
        }
        // Default Constructor, set everything to empty
        public ToolPath()
        {
            this.name = string.Empty;
            this.Pts = new List<ToolPoint>();
            this.matTool = null;
            this.matForm = null;
            this.Additions = new ToolPathAdditions();
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Just a MaterialTool
        public ToolPath(string name, MaterialTool MT)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.matTool = MT;
            this.matForm = null;
            this.Additions = null;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // MaterialTool and Form
        public ToolPath(string name, MaterialTool MT, IMaterialForm MF)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.matTool = MT;
            this.matForm = MF;
            this.Additions = new ToolPathAdditions();
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // MaterialTool, Form and features
        public ToolPath(string name, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA)
        {
            this.name = name;
            this.Pts = new List<ToolPoint>();
            this.matTool = MT; 
            this.matForm = MF;
            this.Additions = TPA;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Copy Constructor
        private ToolPath(ToolPath TP)
        {
            this.name = string.Copy(TP.name);
            this.Pts = new List<ToolPoint>();
            foreach (ToolPoint pt in TP) { this.Add(pt.deepClone()); }
            this.matTool = TP.matTool;
            this.matForm = TP.matForm;
            this.preCode = string.Copy(TP.preCode);
            this.postCode = string.Copy(TP.postCode);
            this.Additions = TP.Additions.deepClone();
        }

        public ToolPath deepClone() => new ToolPath(this);

        public ToolPath deepCloneWithNewPoints(List<ToolPoint> Pts)
        {
            ToolPath newTP = new ToolPath()
            {
                name = string.Copy(this.name),
                matTool = this.matTool,
                matForm = this.matForm,
                preCode = string.Copy(this.preCode),
                postCode = string.Copy(this.postCode),
                Additions = this.Additions.deepClone(),
                Pts = Pts
            };
            return newTP;
        }
        // Copy in features from the valid ToolPath if this does not yet have its own. 
        public void validate(ToolPath valid, IMachine M)
        {
            this.matTool = this.matTool ?? valid.matTool;
            this.matForm = this.matForm ?? valid.matForm;
            this.Additions = this.Additions ?? M.defaultTPA;
        }

        public string TypeDescription => "An action of the machine, for example cutting a single line";
        public string TypeName => "ToolPath"; 

        public string name { get; set; }

        public string preCode { get; set; }
        public string postCode { get; set; }

        public override string ToString() => "Toolpath with " + this.Count + " points.";

        // Main functions

        // Process any additions to the path and return 
        // list of list of toolpaths (for stepdown)
        public List<List<ToolPath>> processAdditions(IMachine M)
        {
            if (this.matTool == null) { matToolException(); }
            int i, j, k, l;
            List<List<ToolPath>> NewPaths = new List<List<ToolPath>>();
            // need a list for each step down as it might split into more than one path
            // and we need to keep those together to coordinate the Machine Operation

            ToolPath useTP;

            // make sure the directions are correct for the machine

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
            if (this.Additions.stepDown)
            {
                // Use the material form to work out the distance to cut in the
                // material, the direction to enter the material and the number of passes.
                List<double> MatDist = new List<double>();
                List<int> NumSteps = new List<int>();
                int MaxSteps = 0; // Maximum distance of all points. 
                List<Vector3d> MatNorm = new List<Vector3d>(); // list of surface normals

                // ask the material form to refine the path

                ToolPath refPath = useTP.matForm.refine(useTP, M);
                MaterialForm.MFintersection inter;

                foreach(ToolPoint TP in refPath)
                {
                    inter = useTP.matForm.intersect(TP, 0).through;
                    MatDist.Add(inter.lineP); // distance to material surface
                    if (MatDist[MatDist.Count - 1] < 0) { MatDist[MatDist.Count - 1] = 0; }// avoid negative distances (outside material)
                    MatNorm.Add(new Vector3d(inter.away));
                    // calculate maximum number of cutDepth height steps down to finishDepth above material
                    NumSteps.Add((int)Math.Ceiling((MatDist[MatDist.Count - 1]-useTP.matTool.finishDepth)/useTP.matTool.cutDepth));
                    if (NumSteps[NumSteps.Count - 1] > MaxSteps) { MaxSteps = NumSteps[NumSteps.Count - 1]; }
                }

                // make a list of depths to cut at.
                // This just steps down right now, but makes it easier to add fancier levelling, if ever worthwhile. 
                // Note that maxsteps currently assumes only stepping down by cutDepth.

                List<double> CutLevel = new List<double>();
                for (i = 0; i < MaxSteps; i++) { CutLevel.Add((i + 1) * useTP.matTool.cutDepth); }

                // process the paths, staying away from the final cut

                ToolPoint TPt;
                bool start;
                bool end;
                double droplength; // length of dropped curve in the middle of a path
                double height; // height above final path

                ToolPath tempTP;

                for(i = 0; i < CutLevel.Count; i++)
                {
                    NewPaths.Add(new List<ToolPath>());
                    tempTP = useTP.deepCloneWithNewPoints(new List<ToolPoint>());
                    tempTP.name = useTP.name + " Pass " + (i + 1).ToString();
                    tempTP.Additions.stepDown = false;

                    start = true;
                    end = false;
                    droplength = 0;

                    for(j = 0; j < refPath.Count && !end; j++)
                    {
                        if(i < NumSteps[j]) // We need to cut here
                        {
                            // if this is the first point to cut we need to add the previous one
                            // if there was one, so we do not miss what was between them
                            if(start && j > 0)
                            {
                                TPt = refPath[j - 1].deepClone();
                                height = useTP.matTool.finishDepth;
                                if (height > MatDist[j - 1]) { height = 0; }
                                TPt.pt = M.toolDir(TPt) * height + TPt.pt; // stay finishDepth above final path

                                tempTP.Add(TPt);
                            }
                            height = MatDist[j] - CutLevel[i];
                            if (height < useTP.matTool.finishDepth) { height = useTP.matTool.finishDepth; } // stay finishDepth above final path
                            TPt = refPath[j].deepClone();
                            TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                            tempTP.Add(TPt);
                            start = false;
                            droplength = 0;
                        }
                        else if(start) // We have not hit any cutting yet;
                        {
                            if(!useTP.Additions.sdDropStart) // we are not dropping the start
                            {
                                TPt = refPath[j].deepClone();
                                height = useTP.matTool.finishDepth;
                                if (height > MatDist[j]) { height = 0; }
                                TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                                tempTP.Add(TPt);
                            } // otherwise we do nothing
                        }
                        else // We need to look ahead
                        {
                            for (k = j; k < refPath.Count && i >= NumSteps[k]; k++) {; } // Look ahead to the next cut

                            if(k == refPath.Count) // No more cutting required
                            {
                                if(useTP.Additions.sdDropEnd) // we are dropping the end
                                {
                                    // Add point as the previous one was deep, 
                                    // then set end to true so we finish
                                    TPt = refPath[j].deepClone();
                                    height = useTP.matTool.finishDepth;
                                    TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                                    tempTP.Add(TPt);
                                    end = true;
                                }
                                else // add point
                                {
                                    TPt = refPath[j].deepClone();
                                    height = useTP.matTool.finishDepth;
                                    TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                                    tempTP.Add(TPt);
                                }
                            }
                            else // into the middle
                            {
                                if (useTP.Additions.sdDropMiddle < 0 || (k - j) < 3) // we are not dropping middle or there are not enough points to justify it
                                {
                                    TPt = refPath[j].deepClone();
                                    height = useTP.matTool.finishDepth;
                                    TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                                    tempTP.Add(TPt);
                                }
                                else //check length of drop
                                {
                                    if (droplength == 0) // If we are at the start of a possible drop Add the length until we hit the end or go over 
                                    {
                                        for (l = j; droplength < useTP.Additions.sdDropMiddle && l < k; l++)
                                        { droplength += refPath[l].pt.DistanceTo(refPath[l + 1].pt); }
                                    }
                                    if (droplength > useTP.Additions.sdDropMiddle)
                                    {
                                        // add point, as previous point was in material
                                        TPt = refPath[j].deepClone();
                                        height = useTP.matTool.finishDepth;
                                        TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                                        tempTP.Add(TPt);
                                        // leap forward cut path and start a new one
                                        // giving settings to add inserts and retracts

                                        tempTP.Additions.retract = true;
                                        NewPaths[NewPaths.Count - 1].Add(tempTP); // add path and create a new one

                                        tempTP = (ToolPath)useTP.deepCloneWithNewPoints(new List<ToolPoint>());
                                        tempTP.name = useTP.name + " Continuing Pass " + i.ToString();
                                        tempTP.Additions.insert = true;
                                        tempTP.Additions.stepDown = false;

                                        // add k-1 point as k is deep
                                        // this will not result in a double point as we checked (k-j) >=3
                                        TPt = refPath[k - 1].deepClone();
                                        height = useTP.matTool.finishDepth;
                                        TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                                        tempTP.Add(TPt);
                                        j = k - 1; //set j to k-1 so it deals with the k point next

                                    }
                                    else // after all that we still need to add the point
                                    {
                                        TPt = refPath[j].deepClone();
                                        height = useTP.matTool.finishDepth;
                                        TPt.pt = M.toolDir(TPt) * height + TPt.pt;
                                        tempTP.Add(TPt);
                                    }
                                }
                            }
                        }
                    }
                    NewPaths[NewPaths.Count - 1].Add(tempTP);
                }
            }

            // add a copy of the original path, making sure step down is false
            // if passes were added, rename with "Final Pass"

            NewPaths.Add(new List<ToolPath>());
            NewPaths[NewPaths.Count - 1].Add(useTP.deepClone());
            NewPaths[NewPaths.Count - 1][0].Additions.stepDown = false;
            if (NewPaths.Count > 1) { NewPaths[NewPaths.Count - 1][0].name = useTP.name + " Final Pass"; }

            // add insert and retract moves

            for (i = 0; i < NewPaths.Count; i++) {
                for (j = 0; j < NewPaths[i].Count; j++)
                { NewPaths[i][j] = M.insertRetract(NewPaths[i][j]); }}

            return NewPaths;
        }

        // Use a curve and direction vector to create a path of toolpoints
        public bool convertCurve(Curve c, Vector3d d)
        {
            if (this.matTool == null) { matToolException(); }
            // Create polyline approximation
            Polyline PL;
            ToolPoint TPt;

            // Check we are dealing with a valid curve.

            if (c != null && c.IsValid)
            {
                Curve c2 = c.ToPolyline(0, 0, Math.PI, 0, 0, this.matTool.tolerance, this.matTool.minStep, 20.0 * this.matTool.toolWidth, true);
                c2.TryGetPolyline(out PL);
            }
            else { return false; }


            this.Pts = new List<ToolPoint>();

            // Add the points to the Path

            foreach (Point3d Pt in PL)
            {
                TPt = new ToolPoint(Pt, d);
                this.Add(TPt);
            }
            return true;
        }

        public static PolylineCurve convertAccurate(Curve C)
        {
            double accTol = 0.000000001;
            Polyline P;
            PolylineCurve PlC;
            // Check if already a polyline, otherwise make one
            if (C.TryGetPolyline(out P)) { PlC = new PolylineCurve(P); }
            else { PlC = C.ToPolyline(0, 0, Math.PI, 0, 0, accTol, 0, 0, true); }

            return PlC;
        }

        internal static ToolPath toPath(object scraps)
        {
            ToolPath oP = new ToolPath();
            if (scraps is IToolPointContainer) { oP.AddRange(((IToolPointContainer)scraps).getSinglePath()); }
            else if (scraps is Point3d) { oP.Add((Point3d)scraps); }
            else if (scraps is IEnumerable)
            {
                foreach (object oB in scraps as IEnumerable)
                {
                    if (oB is IToolPointContainer) { oP.AddRange(((IToolPointContainer)oB).getSinglePath()); }
                    if (oB is Point3d) { oP.Add((Point3d)oB); }
                }
            }
            return oP;
        }

        // Adjust the path so it will not be gouged when cut in 3-axis, or indexed 3-axis mode.
        // TODO make this guarantee that it does not gouge locally. There is a problem 
        // with paths that are steep down, followed by some bottom moves followed by steep out. 
        public ToolPath threeAxisHeightOffset(IMachine M)
        {
            if (this.matTool == null) { matToolException(); }
            List<ToolPoint> offsetPath = new List<ToolPoint>();

            Vector3d dir = (Vector3d)(this[1].pt - this[0].pt);
            dir.Unitize();

            ToolPoint point;
            Vector3d orth = new Vector3d(0, 0, 0);
            bool orthSet = false;

            if (dir == M.toolDir(this[0]) || dir == -M.toolDir(this[0])) { point = this[0]; }
            else
            {
                orth = Vector3d.CrossProduct(dir, M.toolDir(this[0]));
                point = this.matTool.threeAxisHeightOffset(M, this[0], dir, orth);
                orthSet = true;
            }

            List<Line> osLines = new List<Line> { new Line(point.pt, dir) };

            double inter;
            ToolPoint nextPoint;
            double nextinter;
            double orient;
            ToolPoint endCP, startCP;

            bool changeDirection = false; // Has tool direction changed?

            offsetPath.Add(point);

            // loop through the lines of the toolpath finding their offset path 
            // and then travelling to the closest point to the next offset path

            for (int i = 1; i < this.Count - 1; i++)
            {
                // Keep track of tool point direction to warn if it changes (but only once)
                if (M.toolDir(this[i - 1]) != M.toolDir(this[i]) && !changeDirection)
                {
                    this[i].warning.Add("Height offsetting is not designed for 5-Axis motion, it may be unpredictable.");
                    changeDirection = true;
                }
                // Find the next offset line
                dir = (Vector3d)(this[i + 1].pt - this[i].pt);
                // Check for vertical moves
                if (dir == M.toolDir(this[i]) || dir == -M.toolDir(this[i]))
                {
                    if (orthSet) { nextPoint = this.matTool.threeAxisHeightOffset(M, this[i], dir, orth); }
                    else { nextPoint = this[i]; }
                }
                else
                {
                    orth = Vector3d.CrossProduct(dir, M.toolDir(this[i]));
                    orthSet = true;
                    nextPoint = this.matTool.threeAxisHeightOffset(M,this[i], dir, orth);
                }

                // find the next line we will travel along
                osLines.Add(new Line(nextPoint.pt, dir));

                // we need to find the last path that does not reverse when we travel along our new line. 
                // if we go in the wrong direction on an offset path then we are gouging back into previously cut material.
                // In the following we discuss intersection, for lines in 3d this is given by the closest point for two lines.

                // intersect the new line with the last line we used
                Rhino.Geometry.Intersect.Intersection.LineLine(osLines[osLines.Count - 2], osLines[osLines.Count - 1], out inter, out nextinter);
                // find the orientation of the new path
                orient = (osLines[osLines.Count - 2].PointAt(inter) - offsetPath[offsetPath.Count - 1].pt) * osLines[osLines.Count - 2].UnitTangent;

                // loop until we find a suitable line, removing previous points that are now problematic
                // checking the length of offsetPath should ensure we don't try to go past the start 
                // and osLines is always at least 2 long, but we check both just in case.
                while (orient < 0 && offsetPath.Count > 1 && osLines.Count > 1)
                {
                    // remove the reversing line
                    osLines.RemoveAt(osLines.Count - 2);
                    // remove the last point on the offsetPath, which were given by the intersection we are removing
                    offsetPath.RemoveRange(offsetPath.Count - 1, 1);
                    // find the new intersection and orientation
                    Rhino.Geometry.Intersect.Intersection.LineLine(osLines[osLines.Count - 2], osLines[osLines.Count - 1], out inter, out nextinter);
                    orient = (osLines[osLines.Count - 2].PointAt(inter) - offsetPath[offsetPath.Count - 1].pt) * osLines[osLines.Count - 2].UnitTangent;
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
                    startCP = this[i].deepClone();
                    startCP.pt = osLines[osLines.Count - 1].PointAt(nextinter);
                    offsetPath.Add(startCP);

                }
                else
                {
                    // Add the new intersection we like using the closest points on the two lines (the points on each line closest to the other line)
                    // note that we keep the information from the toolpoint before the line we are going to be cutting
                    // this might be removed on later passes, if the line is removed.
                    endCP = this[i].deepClone();
                    endCP.pt = osLines[osLines.Count - 2].PointAt(inter);
                    startCP = this[i].deepClone();
                    startCP.pt = osLines[osLines.Count - 1].PointAt(nextinter);

                    // take the midpoint of the two intersections
                    // there is possibly something clever to do here
                    startCP.pt = (startCP.pt + endCP.pt) / 2;

                    offsetPath.Add(startCP);
                    //offsetPath.Add(endCP);
                }
            }

            // add the final point.

            if (dir == M.toolDir(this.lastP))
            {
                if (orthSet) { nextPoint = this.matTool.threeAxisHeightOffset(M, this.lastP, dir, orth); }
                else { nextPoint = this.lastP; }
            }
            else
            {
                orth = Vector3d.CrossProduct(dir, M.toolDir(this.lastP));
                nextPoint = this.matTool.threeAxisHeightOffset(M,this.lastP, dir, orth);
            }

            offsetPath.Add(nextPoint);

            ToolPath retPath = this.deepCloneWithNewPoints(offsetPath);
            retPath.Additions.threeAxisHeightOffset = false;

            if (!retPath.Additions.insert)
            { retPath.firstP.warning.Add("Height Offsetting does not work between ToolPaths. This might cause unexpected behaviour."); }
            if (!retPath.Additions.retract)
            { retPath.lastP.warning.Add("Height Offsetting does not work between ToolPaths. This might cause unexpected behaviour."); }

            return retPath;
        }

        #region Point extraction and previews
        public ToolPath getSinglePath() => this.deepClone();
        // Get the list of tooltip locations
        public List<Point3d> getPoints()
        {
            List<Point3d> Points = new List<Point3d>();

            foreach (ToolPoint tP in this) { Points.Add(tP.pt); }

            return Points;
        }
        // Get the list of tool directions
        public List<Vector3d> getDirs()
        {
            List<Vector3d> Dirs = new List<Vector3d>();

            foreach (ToolPoint tP in this) { Dirs.Add(tP.dir); }
            return Dirs;
        }
        // Create a path with the points 
        public List<Point3d> getPointsandDirs(out List<Vector3d> Dirs)
        {
            List<Point3d> Ptsout = new List<Point3d>();
            Dirs = new List<Vector3d>();
            foreach (ToolPoint P in this)
            {
                Ptsout.Add(P.pt);
                Dirs.Add(P.dir);
            }
            return Ptsout;
        }
        // Get the list of speeds and feeds (a vector with speed in X and feed in Y)
        public List<Vector3d> getSpeedFeed()
        {
            List<Vector3d> SF = new List<Vector3d>();

            foreach (ToolPoint tP in this) { SF.Add(new Vector3d(tP.speed, tP.feed, 0)); }
            return SF;
        }

        // Bounding Box for previews
        public BoundingBox getBoundingBox()
        {
            BoundingBox BB = BoundingBox.Unset;
            for (int i = 0; i < this.Count; i++)
            { BB.Union(this[i].getBoundingBox()); }
            return BB;
        }
        // Create a polyline
        public PolylineCurve getLine()
        {
            return new PolylineCurve(this.getPoints());
        }
        // Lines for each toolpoint
        public List<Line> toolLines()
        {
            List<Line> lines = new List<Line>();
            foreach (ToolPoint TP in this) { lines.Add(TP.toolLine()); }
            return lines;
        }
       #endregion

        #region List Functions
        public int Count => ((IList<ToolPoint>)this.Pts).Count;
        public bool IsReadOnly => ((IList<ToolPoint>)this.Pts).IsReadOnly;
        public ToolPoint this[int index] { get => ((IList<ToolPoint>)this.Pts)[index]; set => ((IList<ToolPoint>)this.Pts)[index] = value; }
        public int IndexOf(ToolPoint item) { return ((IList<ToolPoint>)this.Pts).IndexOf(item); }
        public void Insert(int index, ToolPoint item) { ((IList<ToolPoint>)this.Pts).Insert(index, item); }
        public void RemoveAt(int index) { ((IList<ToolPoint>)this.Pts).RemoveAt(index); }
        public void Add(ToolPoint item) { ((IList<ToolPoint>)this.Pts).Add(item); }
        public void Add(Point3d item) { ((IList<ToolPoint>)this.Pts).Add(new ToolPoint(item)); }
        public void AddRange(IEnumerable<ToolPoint> items) { this.Pts.AddRange(items); }
        public void AddRange(IEnumerable<Point3d> items)
        { foreach(Point3d Pt in items) { this.Add(Pt); } }
        public void Clear() { ((IList<ToolPoint>)this.Pts).Clear(); }
        public bool Contains(ToolPoint item) { return ((IList<ToolPoint>)this.Pts).Contains(item); }
        public void CopyTo(ToolPoint[] array, int arrayIndex) { ((IList<ToolPoint>)this.Pts).CopyTo(array, arrayIndex); }
        public bool Remove(ToolPoint item) { return ((IList<ToolPoint>)this.Pts).Remove(item); }
        public IEnumerator<ToolPoint> GetEnumerator() { return ((IList<ToolPoint>)this.Pts).GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return ((IList<ToolPoint>)this.Pts).GetEnumerator(); }
        #endregion
    }

    // Grasshopper Type Wrapper
    public class GH_ToolPath : CAMel_Goo<ToolPath>, IGH_PreviewData
    {
        public BoundingBox ClippingBox
        {
            get
            {
                BoundingBox BB = new Polyline(this.Value.getPoints()).BoundingBox;
                BB.Inflate(1);
                return BB; 
            }
        }
        // Default Constructor
        public GH_ToolPath() { this.Value = new ToolPath(); }
        // Create from unwrapped version
        public GH_ToolPath(ToolPath TP) { this.Value = TP; }
        // Copy Constructor
        public GH_ToolPath(GH_ToolPath TP) { this.Value = TP.Value.deepClone(); }
        // Duplicate
        public override IGH_Goo Duplicate() { return new GH_ToolPath(this); }

        public override bool CastTo<Q>(ref Q target)
        {
            // Cast from unwrapped ToolPath
            if (typeof(Q).IsAssignableFrom(typeof(ToolPath)))
            {
                target = (Q)(object)this.Value;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(MachineOperation)))
            {
                target = (Q)(object)new MachineOperation(this.Value);
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_MachineOperation)))
            {
                target = (Q)(object)new GH_MachineOperation(new MachineOperation(this.Value));
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(Curve)))
            {
                target = (Q)(object)this.Value.getLine();
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_Curve)))
            {
                target = (Q)(object)new GH_Curve(this.Value.getLine());
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(IMaterialForm)))
            {
                target = (Q)(object)this.Value.matForm;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_MaterialForm)))
            {
                target = (Q)(object)new GH_MaterialForm(this.Value.matForm);
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(MaterialTool)))
            {
                target = (Q)(object)this.Value.matTool;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_MaterialTool)))
            {
                target = (Q)(object)new GH_MaterialTool(this.Value.matTool);
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(ToolPathAdditions)))
            {
                target = (Q)(object)this.Value.Additions;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_ToolPathAdditions)))
            {
                target = (Q)(object)new GH_ToolPathAdditions(this.Value.Additions);
                return true;
            }
            return false;
        }

        public override bool CastFrom(object source)
        {
            if (source == null) { return false; }
            //Cast from unwrapped ToolPath
            if (typeof(ToolPath).IsAssignableFrom(source.GetType()))
            {
                Value = (ToolPath)source;
                return true;
            }
            if (typeof(Curve).IsAssignableFrom(source.GetType()))
            {
                ToolPath TP = new ToolPath();
                Polyline PL = null;
                if (((Curve)source).TryGetPolyline(out PL))
                {
                    TP.AddRange(PL);
                    Value = TP;
                    return true;
                }
                return false;
            }
            if (typeof(GH_Curve).IsAssignableFrom(source.GetType()))
            {
                ToolPath TP = new ToolPath();
                Polyline PL = null;
                if (((GH_Curve)source).Value.TryGetPolyline(out PL))
                {
                    TP.AddRange(PL);
                    Value = TP;
                    return true;
                }
                return false;
            }

            return false;
        }

        public void DrawViewportWires(GH_PreviewWireArgs args)
        {
            args.Pipeline.DrawCurve(this.Value.getLine(), args.Color);
            args.Pipeline.DrawArrows(this.Value.toolLines(), args.Color);
        }
        public void DrawViewportMeshes(GH_PreviewMeshArgs args) { }
    }

    // Grasshopper Parameter Wrapper
    public class GH_ToolPathPar : GH_Param<GH_ToolPath>, IGH_PreviewObject
    {
        public GH_ToolPathPar() :
            base("ToolPath", "ToolPath", "Contains a collection of Tool Paths", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid
        {
            get { return new Guid("4ea6da38-c19f-43e7-85d4-ada4716c06ac"); }
        }

        public bool Hidden { get; set; }
        public bool IsPreviewCapable => true;
        public BoundingBox ClippingBox => base.Preview_ComputeClippingBox();
        public void DrawViewportWires(IGH_PreviewArgs args) => base.Preview_DrawWires(args);
        public void DrawViewportMeshes(IGH_PreviewArgs args) => base.Preview_DrawMeshes(args);

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