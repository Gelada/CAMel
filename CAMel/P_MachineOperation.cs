using System;
using System.Collections;
using System.Collections.Generic;

using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using CAMel.Types.Machine;
using CAMel.Types.MaterialForm;

namespace CAMel.Types
{
    // List of toolpaths forming a general Operation of the machine,
    // from the complex to the simple
    // creating a surface, drilling a whole, cutting out an object...
    // When paths within an operation have a stepdown then all first 
    // step downs with be completed, then the second and so on.
    public class MachineOperation : IList<ToolPath>,IToolPointContainer
    {
        private List<ToolPath> TPs;
        public ToolPoint firstP
        {
            get
            {
                ToolPoint oP = null;
                // Cycle through to find a path of length greater than 1.
                for(int i=0;i<this.Count;i++)
                {
                    if (this[i].Count>0) { oP = this[i].firstP; break; }
                }
                return oP;
            }
        }
        public ToolPoint lastP
        {
            get
            {
                ToolPoint oP = null;
                // Cycle through to find a path of length greater than 1.
                for (int i = this.Count - 1; i >= 0; i--)
                {
                    if (this[i].Count > 0) { oP = this[i].lastP; break; }
                }
                return oP;
            }
        }

        // Default Constructor
        public MachineOperation()
        {
            this.TPs = new List<ToolPath>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // From list of toolpaths
        public MachineOperation(List<ToolPath> TPs)
        {
            this.TPs =TPs;
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // From toolpath
        public MachineOperation(ToolPath TP)
        {
            this.TPs = new List<ToolPath>() { TP };
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Just name
        public MachineOperation(string name)
        {
            this.name = name;
            this.TPs = new List<ToolPath>();
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Name and ToolPaths
        public MachineOperation(string name, List<ToolPath> TPs)
        {
            this.name = name;
            this.TPs = TPs;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Copy Constructor
        private MachineOperation(MachineOperation Op)
        {
            this.name = string.Copy(Op.name);
            this.preCode = string.Copy(Op.preCode);
            this.postCode = string.Copy(Op.postCode);
            this.TPs = new List<ToolPath>();
            foreach (ToolPath TP in Op) { this.Add(TP.deepClone()); }
        }
        public MachineOperation deepClone() => new MachineOperation(this);

        // Return with new paths.
        public MachineOperation deepCloneWithNewPaths(List<ToolPath> procPaths)
        {
            MachineOperation outOp = new MachineOperation
            {
                preCode = this.preCode,
                postCode = this.postCode,
                name = this.name,
                TPs = procPaths
            };

            return outOp;
        }

        public string TypeDescription
        {
            get { return "Single operation of the machine, from the complex (creating a surface) to the simple (drilling a hole)."; }
        }

        public string TypeName
        {
            get { return "MachineOperation"; }
        }

        public string name { get; set; }

        public string preCode { get; set; }
        public string postCode { get; set; }

        public override string ToString()
        {
            int total_TP = 0;
            foreach (ToolPath TP in this)
            {
                total_TP = total_TP + TP.Count;
            }
            return "Machine Operation: " + this.name + ", " + this.Count + " toolpaths, " + total_TP + " total tool points.";
        }

        // Process the toolpaths for additions and ensure ToolPaths are valid for writing. 
        public MachineOperation processAdditions(IMachine M, ref ToolPath validTP)
        {
            // Wow a 3d block of ToolPaths
            // Each of the stepdown paths can have several pieces (1st level)
            // Each ToolPath has several stepdown paths (2nd level)
            // We started with a list of toolpaths (1st level)
            // We create this block and then order it so we do 
            // all preparation a level at a time and then do a final pass of all paths

            List<List<List<ToolPath>>> newPaths = new List<List<List<ToolPath>>>();

            // Store finishing paths separately
            List<List<ToolPath>> finishPaths = new List<List<ToolPath>>();
            List<ToolPath> fP = null;

            foreach (ToolPath TP in this)
            {
                TP.validate(validTP, M);
                validTP = TP;
                newPaths.Add(TP.processAdditions(M, out fP));
                finishPaths.Add(fP);
            }

            // Create the list for the output
            List<ToolPath> procPaths = new List<ToolPath>();

            List<ToolPath> levelPaths; // all paths on one level

            // Find path with most levels
            int levels = 0;
            foreach (List<List<ToolPath>> LTP in newPaths)
            { if (LTP.Count > levels) { levels = LTP.Count; } }
            // do the roughing layers
            for (int i = 0; i < levels; i++)
            {
                levelPaths = new List<ToolPath>();
                foreach (List<List<ToolPath>> LTP in newPaths)
                { if (i < LTP.Count) { levelPaths.AddRange(LTP[i]); } }

                // sort here (remember to only move chunks that are outside the material!)

                procPaths.AddRange(levelPaths);
            }
            // finishing cuts
            // find path with most levels
            levels = 0;
            foreach (List<ToolPath> LTP in finishPaths)
            { if (LTP.Count > levels) { levels = LTP.Count; } }
            // add finishing paths
            levelPaths = new List<ToolPath>();

            for (int i = 0; i < levels; i++)
            {
                levelPaths = new List<ToolPath>();
                foreach (List<ToolPath> LTP in finishPaths)
                { if (i < LTP.Count) { levelPaths.Add(LTP[i]); } }

                // sort here (remember to only move chunks that are outside the material!)

                procPaths.AddRange(levelPaths);
            }

            return this.deepCloneWithNewPaths(procPaths);
        }

        // Write GCode for this operation
        public void writeCode(ref CodeInfo Co, IMachine M, out ToolPath eP, ToolPath sP)
        {
            M.writeOpStart(ref Co, this);

            ToolPath oldPath = sP;
            bool first = true;

            foreach (ToolPath TP in this)
            {
                if (TP.Count > 0) // If path has length 0 just ignore
                {
                    // If a move is needed transition from one path to the next 
                    if (oldPath.lastP != TP.firstP) { M.writeTransition(ref Co, oldPath, TP, first); }
                    
                    // Add Path to Code
                    M.writeCode(ref Co, TP);

                    oldPath = TP;
                    first = false;
                }
            }

            Co.append(this.postCode);
            eP = oldPath;
        }


        // Process a collage of bits and pieces into a list of Operations
        internal static List<MachineOperation> toOperations(object scraps, out int ignores)
        {
            List<MachineOperation> oMOs = new List<MachineOperation>();
            ignores = 0;

            if (scraps is null) { return oMOs; }

            if (scraps is MachineOperation) { oMOs.Add((MachineOperation)scraps); }
            if (scraps is List<ToolPath>) { oMOs.Add(new MachineOperation((List<ToolPath>)scraps)); }
            if (scraps is IMaterialForm) { oMOs.Add(new MachineOperation(new ToolPath((IMaterialForm)scraps))); }
            if (scraps is MaterialTool) { oMOs.Add(new MachineOperation(new ToolPath((MaterialTool)scraps))); }

            // Otherwise process mixed up any other sort of list by term.
            else if (scraps is IEnumerable)
            {
                bool tpPath = false;
                ToolPath tempTP = new ToolPath();
                foreach (object oB in (IEnumerable)scraps)
                {
                    if (oB is Point3d)
                    {
                        tpPath = true;
                        tempTP.Add(new ToolPoint((Point3d)oB));
                    }
                    else if (oB is ToolPoint)
                    {
                        tpPath = true;
                        tempTP.Add((ToolPoint)oB);
                    }
                    else
                    {
                        if (tpPath)
                        {
                            oMOs.Add(new MachineOperation(new List<ToolPath> { tempTP }));
                            tpPath = false;
                            tempTP = new ToolPath();
                        }
                        if (oB is ToolPath) { oMOs.Add(new MachineOperation(new List<ToolPath> { (ToolPath)oB })); }
                        else if (oB is MachineOperation) { oMOs.Add((MachineOperation)oB); }
                        else if (oB is MachineInstruction) { oMOs.AddRange((MachineInstruction)oB); }
                        else if (oB is IMaterialForm) { oMOs.Add(new MachineOperation(new ToolPath((IMaterialForm)oB))); }
                        else if (oB is MaterialTool) { oMOs.Add(new MachineOperation(new ToolPath((MaterialTool)oB))); }
                        else { ignores++; }
                    }
                }
                if (tpPath)
                {
                    oMOs.Add(new MachineOperation(new List<ToolPath> { tempTP }));
                }
            }
            return oMOs;
        }


        #region Point extraction and previews
        public ToolPath getSinglePath()
        {
            ToolPath oP = this[0].getSinglePath();
            for (int i = 1; i < this.Count; i++) { oP.AddRange(this[i].getSinglePath()); }
            return oP;
        }
        // Get the list of tooltip locations
        public List<List<Point3d>> getPoints()
        {
            List<List<Point3d>> Pts = new List<List<Point3d>>();
            foreach (ToolPath TP in this) { Pts.Add(TP.getPoints()); }
            return Pts;
        }
        // Get the list of tool directions
        public List<List<Vector3d>> getDirs()
        {
            List<List<Vector3d>> Dirs = new List<List<Vector3d>>();
            foreach (ToolPath TP in this) { Dirs.Add(TP.getDirs()); }
            return Dirs;
        }
        // Create a path with the points 
        public List<List<Point3d>> getPointsandDirs(out List<List<Vector3d>> Dirs)
        {
            List<List<Point3d>> Ptsout = new List<List<Point3d>>();
            Dirs = new List<List<Vector3d>>();
            List<Vector3d> TPDirs;
            foreach (ToolPath TP in this)
            {
                TPDirs = new List<Vector3d>();
                Ptsout.Add(TP.getPointsandDirs(out TPDirs));
                Dirs.Add(TPDirs);
            }
            return Ptsout;
        }      
        // Bounding Box for previews
        public BoundingBox getBoundingBox()
        {
            BoundingBox BB = BoundingBox.Unset;
            for (int i = 0; i < this.Count; i++)
            { BB.Union(this[i].getBoundingBox()); }
            return BB;
        }
        // Create single polyline
        public PolylineCurve getLine() => this.getSinglePath().getLine();
        // Create polylines
        public List<PolylineCurve> getLines()
        {
            List<PolylineCurve> lines = new List<PolylineCurve>();
            foreach (ToolPath TP in this) { lines.Add(TP.getLine()); }
            return lines;
        }
        // Lines for each toolpoint
        public List<Line> toolLines()
        {
            List<Line> lines = new List<Line>();
            foreach (ToolPath TP in this) { lines.AddRange(TP.toolLines()); }
            return lines;
        }
        #endregion

        #region List Functions
        public int Count => ((IList<ToolPath>)this.TPs).Count;
        public bool IsReadOnly => ((IList<ToolPath>)this.TPs).IsReadOnly;
        public ToolPath this[int index] { get => ((IList<ToolPath>)this.TPs)[index]; set => ((IList<ToolPath>)this.TPs)[index] = value; }
        public int IndexOf(ToolPath item) { return ((IList<ToolPath>)this.TPs).IndexOf(item); }
        public void Insert(int index, ToolPath item) { ((IList<ToolPath>)this.TPs).Insert(index, item); }
        public void RemoveAt(int index) { ((IList<ToolPath>)this.TPs).RemoveAt(index); }
        public void Add(ToolPath item) { ((IList<ToolPath>)this.TPs).Add(item); }
        public void AddRange(IEnumerable<ToolPath> items) { this.TPs.AddRange(items); }
        public void Clear() { ((IList<ToolPath>)this.TPs).Clear(); }
        public bool Contains(ToolPath item) { return ((IList<ToolPath>)this.TPs).Contains(item); }
        public void CopyTo(ToolPath[] array, int arrayIndex) { ((IList<ToolPath>)this.TPs).CopyTo(array, arrayIndex); }
        public bool Remove(ToolPath item) { return ((IList<ToolPath>)this.TPs).Remove(item); }
        public IEnumerator<ToolPath> GetEnumerator() { return ((IList<ToolPath>)this.TPs).GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return ((IList<ToolPath>)this.TPs).GetEnumerator(); }
        #endregion
}

    // Grasshopper Type Wrapper
    public class GH_MachineOperation : CAMel_Goo<MachineOperation>, IGH_PreviewData
    {
        public BoundingBox ClippingBox => this.Value.getBoundingBox();

        // Default Constructor
        public GH_MachineOperation() { this.Value = new MachineOperation(); }
        // Construct from value alone
        public GH_MachineOperation(MachineOperation MO) { this.Value = MO; }
        // Copy Constructor.
        public GH_MachineOperation(GH_MachineOperation Op) { this.Value = Op.Value.deepClone(); }
        // Duplicate
        public override IGH_Goo Duplicate() { return new GH_MachineOperation(this); }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(MachineOperation)))
            {
                object ptr = this.Value;
                target = (Q)ptr;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(ToolPath)))
            {
                object ptr = this.Value.getSinglePath();
                target = (Q)ptr;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(GH_ToolPath)))
            {
                object ptr = new GH_ToolPath(this.Value.getSinglePath());
                target = (Q)ptr;
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

            return false;
        }
        public override bool CastFrom(object source)
        {
            if (source == null) { return false; }
            //Cast from unwrapped MO
            if (typeof(MachineOperation).IsAssignableFrom(source.GetType()))
            {
                this.Value = (MachineOperation)source;
                return true;
            }

            return false;
        }

        public void DrawViewportWires(GH_PreviewWireArgs args)
        {
            foreach (PolylineCurve L in this.Value.getLines())
            {
                args.Pipeline.DrawCurve(L, args.Color);
            }
            args.Pipeline.DrawArrows(this.Value.toolLines(), args.Color);
        }
        public void DrawViewportMeshes(GH_PreviewMeshArgs args) { }
    }

    // Grasshopper Parameter Wrapper
    public class GH_MachineOperationPar : GH_Param<GH_MachineOperation>, IGH_PreviewObject
    {
        public GH_MachineOperationPar() :
            base("Operation", "MachOp", "Contains a collection of Machine Operations", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid
        {
            get { return new Guid("e0dfd958-f0fb-46b7-b743-04e071ea25fd"); }
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
                return Properties.Resources.machineoperations;
            }
        }
    }

}