using System;
using System.Collections;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using CAMel.Types.Machine;

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

        public int Count => ((IList<ToolPath>)this.TPs).Count;

        public bool IsReadOnly => ((IList<ToolPath>)this.TPs).IsReadOnly;

        public ToolPath this[int index] { get => ((IList<ToolPath>)this.TPs)[index]; set => ((IList<ToolPath>)this.TPs)[index] = value; }

        public override string ToString()
        {
            int total_TP = 0;
            foreach (ToolPath TP in this)
            {
                total_TP = total_TP + TP.Count;
            }
            return "Machine Operation: " + this.name + ", " + this.Count + " toolpaths, " + total_TP + " total tool points.";
        }

        // Process the toolpaths for additions
        public MachineOperation processAdditions(IMachine M)
        {
            // Wow a 3d block of ToolPaths
            // Each of the stepdown paths can have several pieces (1st level)
            // Each ToolPath has several stepdown paths (2nd level)
            // We started with a list of toolpaths (1st level)
            // We create this block and then order it so we do 
            // all preparation a level at a time and then do a final pass of all paths
            // TODO reorder cutting to increase efficency
            List<List<List<ToolPath>>> newPaths = new List<List<List<ToolPath>>>();

            foreach (ToolPath TP in this)
            { newPaths.Add(TP.processAdditions(M)); }

            // Create the list for the output
            List<ToolPath> procPaths = new List<ToolPath>();

            List<ToolPath> levelPaths; // all paths on one level

            // Find path with most levels
            int levels = 0;
            foreach (List<List<ToolPath>> LTP in newPaths)
            { if (LTP.Count > levels) { levels = LTP.Count; } }
            // do the roughing layers
            for (int i = 0; i < levels - 1; i++)
            {
                levelPaths = new List<ToolPath>();
                foreach (List<List<ToolPath>> LTP in newPaths)
                { if (i < LTP.Count - 1) { levelPaths.AddRange(LTP[i]); } }
                // sort here (remember to only move chunks that are outside the material!)
                procPaths.AddRange(levelPaths);
            }
            // final cut of everything
            levelPaths = new List<ToolPath>();
            foreach (List<List<ToolPath>> LTP in newPaths)
            { levelPaths.AddRange(LTP[LTP.Count - 1]); }
            // sort here (remember to only move chunks that are outside the material!)
            procPaths.AddRange(levelPaths);

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

            Co.Append(this.postCode);
            eP = oldPath;
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
        // Create a polyline
        public List<PolylineCurve> getLines()
        {
            List<PolylineCurve> lines = new List<PolylineCurve>();
            foreach(ToolPath TP in this) { lines.Add(TP.getLine()); }
            return lines;
        }
        // Lines for each toolpoint
        public List<Line> toolLines()
        {
            List<Line> lines = new List<Line>();
            foreach (ToolPath TP in this) { lines.AddRange(TP.toolLines()); }
            return lines;
        }

        public ToolPath getSinglePath()
        {
            ToolPath oP = this[0].getSinglePath();
            for (int i = 1; i < this.Count; i++) { oP.AddRange(this[i].getSinglePath()); }
            return oP;
        }
        public int IndexOf(ToolPath item)
        {
            return ((IList<ToolPath>)this.TPs).IndexOf(item);
        }

        public void Insert(int index, ToolPath item)
        {
            ((IList<ToolPath>)this.TPs).Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            ((IList<ToolPath>)this.TPs).RemoveAt(index);
        }

        public void Add(ToolPath item)
        {
            ((IList<ToolPath>)this.TPs).Add(item);
        }

        public void Clear()
        {
            ((IList<ToolPath>)this.TPs).Clear();
        }

        public bool Contains(ToolPath item)
        {
            return ((IList<ToolPath>)this.TPs).Contains(item);
        }

        public void CopyTo(ToolPath[] array, int arrayIndex)
        {
            ((IList<ToolPath>)this.TPs).CopyTo(array, arrayIndex);
        }

        public bool Remove(ToolPath item)
        {
            return ((IList<ToolPath>)this.TPs).Remove(item);
        }

        public IEnumerator<ToolPath> GetEnumerator()
        {
            return ((IList<ToolPath>)this.TPs).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IList<ToolPath>)this.TPs).GetEnumerator();
        }

        internal Curve getLine()
        {
            throw new NotImplementedException();
        }
    }

    // Grasshopper Type Wrapper
    public class GH_MachineOperation : CAMel_Goo<MachineOperation>, IGH_BakeAwareData, IGH_PreviewData
    {
        public BoundingBox ClippingBox => throw new NotImplementedException();

        // Default Constructor
        public GH_MachineOperation()
        {
            this.Value = new MachineOperation();
        }
        // Just name
        public GH_MachineOperation(string name)
        {
            this.Value = new MachineOperation(name);
        }
        // Copy Constructor.
        public GH_MachineOperation(GH_MachineOperation Op)
        {
            this.Value = Op.Value.deepClone();
        }
        // Duplicate
        public override IGH_Goo Duplicate()
        {
            return new GH_MachineOperation(this);
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(MachineOperation)))
            {
                object ptr = this.Value;
                target = (Q)ptr;
                return true;
            }

            return false;
        }

        public override bool CastFrom(object source)
        {
            if (source == null)
            {
                return false;
            }
            if (source is MachineOperation)
            {
                this.Value = ((MachineOperation)source).deepClone();
                return true;
            }
            return false;
        }

        public bool BakeGeometry(RhinoDoc doc, ObjectAttributes att, out Guid obj_guid)
        {/*
            obj_guid = Guid;
            if (att == null) { att = doc.CreateDefaultAttributes(); }
            foreach (PolylineCurve L in Value.GetLines())
            {
                obj_guid.Add(doc.Objects.AddCurve(L,att));
            }*/
            obj_guid = Guid.Empty; 
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

        public void DrawViewportMeshes(GH_PreviewMeshArgs args) {}
    }

    // Grasshopper Parameter Wrapper
    public class GH_MachineOperationPar : GH_Param<GH_MachineOperation>
    {
        public GH_MachineOperationPar() :
            base("Operation", "MachOp", "Contains a collection of Machine Operations", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid
        {
            get { return new Guid("e0dfd958-f0fb-46b7-b743-04e071ea25fd"); }
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
                return Properties.Resources.machineoperations;
            }
        }
    }

}