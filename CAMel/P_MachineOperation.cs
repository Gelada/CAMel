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
        private List<ToolPath> _tPs;
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
            this._tPs = new List<ToolPath>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // From list of toolpaths
        public MachineOperation(List<ToolPath> tPs)
        {
            this._tPs =tPs;
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // From toolpath
        public MachineOperation(ToolPath tP)
        {
            this._tPs = new List<ToolPath>() { tP };
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Just name
        public MachineOperation(string name)
        {
            this.name = name;
            this._tPs = new List<ToolPath>();
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Name and ToolPaths
        public MachineOperation(string name, List<ToolPath> tPs)
        {
            this.name = name;
            this._tPs = tPs;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }
        // Copy Constructor
        private MachineOperation(MachineOperation mO)
        {
            this.name = string.Copy(mO.name);
            this.preCode = string.Copy(mO.preCode);
            this.postCode = string.Copy(mO.postCode);
            this._tPs = new List<ToolPath>();
            foreach (ToolPath tP in mO) { Add(tP.deepClone()); }
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
                _tPs = procPaths
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
            int totalTP = 0;
            foreach (ToolPath tP in this)
            {
                totalTP = totalTP + tP.Count;
            }
            return "Machine Operation: " + this.name + ", " + this.Count + " toolpaths, " + totalTP + " total tool points.";
        }

        // Process the toolpaths for additions and ensure ToolPaths are valid for writing.
        public MachineOperation processAdditions(IMachine m, ref ToolPath validTP)
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

            foreach (ToolPath tP in this)
            {
                tP.validate(validTP, m);
                validTP = tP;
                newPaths.Add(tP.processAdditions(m, out List<ToolPath> fP));
                finishPaths.Add(fP);
            }

            // Create the list for the output
            List<ToolPath> procPaths = new List<ToolPath>();

            List<ToolPath> levelPaths; // all paths on one level

            // Find path with most levels
            int levels = 0;
            foreach (List<List<ToolPath>> lTp in newPaths)
            { if (lTp.Count > levels) { levels = lTp.Count; } }
            // do the roughing layers
            for (int i = 0; i < levels; i++)
            {
                levelPaths = new List<ToolPath>();
                foreach (List<List<ToolPath>> lTp in newPaths)
                { if (i < lTp.Count) { levelPaths.AddRange(lTp[i]); } }

                // sort here (remember to only move chunks that are outside the material!)

                procPaths.AddRange(levelPaths);
            }
            // finishing cuts
            // find path with most levels
            levels = 0;
            foreach (List<ToolPath> lTp in finishPaths)
            { if (lTp.Count > levels) { levels = lTp.Count; } }
            // add finishing paths

            for (int i = 0; i < levels; i++)
            {
                levelPaths = new List<ToolPath>();
                foreach (List<ToolPath> lTp in finishPaths)
                { if (i < lTp.Count) { levelPaths.Add(lTp[i]); } }

                // sort here (remember to only move chunks that are outside the material!)

                procPaths.AddRange(levelPaths);
            }

            return deepCloneWithNewPaths(procPaths);
        }

        // Write GCode for this operation
        public void writeCode(ref CodeInfo co, IMachine m, out ToolPath eP, ToolPath sP)
        {
            m.writeOpStart(ref co, this);

            ToolPath oldPath = sP;
            bool first = true;

            foreach (ToolPath tP in this)
            {
                if (tP.Count > 0) // If path has length 0 just ignore
                {
                    // If a move is needed transition from one path to the next
                    if (oldPath.lastP != tP.firstP) { m.writeTransition(ref co, oldPath, tP, first); }

                    // Add Path to Code
                    m.writeCode(ref co, tP);

                    oldPath = tP;
                    first = false;
                }
            }

            co.append(this.postCode);
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
            List<List<Point3d>> pts = new List<List<Point3d>>();
            foreach (ToolPath tP in this) { pts.Add(tP.getPoints()); }
            return pts;
        }
        // Get the list of tool directions
        public List<List<Vector3d>> getDirs()
        {
            List<List<Vector3d>> dirs = new List<List<Vector3d>>();
            foreach (ToolPath tP in this) { dirs.Add(tP.getDirs()); }
            return dirs;
        }
        // Create a path with the points
        public List<List<Point3d>> getPointsAndDirs(out List<List<Vector3d>> dirs)
        {
            List<List<Point3d>> ptsOut = new List<List<Point3d>>();
            dirs = new List<List<Vector3d>>();
            foreach (ToolPath tP in this)
            {
                ptsOut.Add(tP.getPointsandDirs(out List<Vector3d> tPDirs));
                dirs.Add(tPDirs);
            }
            return ptsOut;
        }
        // Bounding Box for previews
        public BoundingBox getBoundingBox()
        {
            BoundingBox bb = BoundingBox.Unset;
            for (int i = 0; i < this.Count; i++)
            { bb.Union(this[i].getBoundingBox()); }
            return bb;
        }
        // Create single polyline
        public PolylineCurve getLine() => getSinglePath().getLine();
        // Create polylines
        public List<PolylineCurve> getLines()
        {
            List<PolylineCurve> lines = new List<PolylineCurve>();
            foreach (ToolPath tP in this) { lines.Add(tP.getLine()); }
            return lines;
        }
        // Lines for each toolpoint
        public List<Line> toolLines()
        {
            List<Line> lines = new List<Line>();
            foreach (ToolPath tP in this) { lines.AddRange(tP.toolLines()); }
            return lines;
        }
        #endregion

        #region List Functions
        public int Count => ((IList<ToolPath>)this._tPs).Count;
        public bool IsReadOnly => ((IList<ToolPath>)this._tPs).IsReadOnly;
        public ToolPath this[int index] { get => ((IList<ToolPath>)this._tPs)[index]; set => ((IList<ToolPath>)this._tPs)[index] = value; }
        public int IndexOf(ToolPath item) { return ((IList<ToolPath>)this._tPs).IndexOf(item); }
        public void Insert(int index, ToolPath item) { ((IList<ToolPath>)this._tPs).Insert(index, item); }
        public void RemoveAt(int index) { ((IList<ToolPath>)this._tPs).RemoveAt(index); }
        public void Add(ToolPath item) { ((IList<ToolPath>)this._tPs).Add(item); }
        public void AddRange(IEnumerable<ToolPath> items) { this._tPs.AddRange(items); }
        public void Clear() { ((IList<ToolPath>)this._tPs).Clear(); }
        public bool Contains(ToolPath item) { return ((IList<ToolPath>)this._tPs).Contains(item); }
        public void CopyTo(ToolPath[] array, int arrayIndex) { ((IList<ToolPath>)this._tPs).CopyTo(array, arrayIndex); }
        public bool Remove(ToolPath item) { return ((IList<ToolPath>)this._tPs).Remove(item); }
        public IEnumerator<ToolPath> GetEnumerator() { return ((IList<ToolPath>)this._tPs).GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return ((IList<ToolPath>)this._tPs).GetEnumerator(); }
        #endregion
}

    // Grasshopper Type Wrapper
    public sealed class GH_MachineOperation : CAMel_Goo<MachineOperation>, IGH_PreviewData
    {
        public BoundingBox ClippingBox => this.Value.getBoundingBox();

        // Default Constructor
        public GH_MachineOperation() { this.Value = new MachineOperation(); }
        // Construct from value alone
        public GH_MachineOperation(MachineOperation mO) { this.Value = mO; }
        // Copy Constructor.
        public GH_MachineOperation(GH_MachineOperation mO) { this.Value = mO.Value.deepClone(); }
        // Duplicate
        public override IGH_Goo Duplicate() { return new GH_MachineOperation(this); }

        public override bool CastTo<T>(ref T target)
        {
            if (typeof(T).IsAssignableFrom(typeof(MachineOperation)))
            {
                object ptr = this.Value;
                target = (T)ptr;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(ToolPath)))
            {
                object ptr = this.Value.getSinglePath();
                target = (T)ptr;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_ToolPath)))
            {
                object ptr = new GH_ToolPath(this.Value.getSinglePath());
                target = (T)ptr;
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(Curve)))
            {
                target = (T)(object)this.Value.getLine();
                return true;
            }
            if (typeof(T).IsAssignableFrom(typeof(GH_Curve)))
            {
                target = (T)(object)new GH_Curve(this.Value.getLine());
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
            foreach (PolylineCurve l in this.Value.getLines())
            {
                args.Pipeline.DrawCurve(l, args.Color);
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
        public BoundingBox ClippingBox => Preview_ComputeClippingBox();
        public void DrawViewportWires(IGH_PreviewArgs args) => Preview_DrawWires(args);
        public void DrawViewportMeshes(IGH_PreviewArgs args) => Preview_DrawMeshes(args);

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