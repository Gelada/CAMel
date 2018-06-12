using System;
using System.Collections;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

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

        // Default Constructor
        public MachineOperation()
        {
            this.TPs = new List<ToolPath>();
            this.name = "";
            this.preCode = "";
            this.postCode = "";
        }
        // Just name
        public MachineOperation(string name)
        {
            this.name = name;
            this.TPs = new List<ToolPath>();
            this.preCode = "";
            this.postCode = "";
        }
        // Name and ToolPaths
        public MachineOperation(string name, List<ToolPath> TPs)
        {
            this.name = name;
            this.TPs = TPs;
            this.preCode = "";
            this.postCode = "";
        }
        // Copy Constructor
        public MachineOperation(MachineOperation Op)
        {
            this.name = Op.name;
            this.preCode = Op.preCode;
            this.postCode = Op.postCode;
            this.TPs = new List<ToolPath>();
            foreach (ToolPath TP in Op)
            {
                this.Add(new ToolPath(TP));
            }
        }

        // Return with new paths.
        public MachineOperation copyWithNewPaths(List<ToolPath> procPaths)
        {
            MachineOperation outOp = new MachineOperation();
            outOp.preCode = this.preCode;
            outOp.postCode = this.postCode;
            outOp.name = this.name;
            outOp.TPs = procPaths;

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

        public bool IsValid
        {
            get
            {
                throw new NotImplementedException("Machine Operation has not implemented IsValid");
            }
        }

        public int Count => ((IList<ToolPath>)TPs).Count;

        public bool IsReadOnly => ((IList<ToolPath>)TPs).IsReadOnly;

        public ToolPath this[int index] { get => ((IList<ToolPath>)TPs)[index]; set => ((IList<ToolPath>)TPs)[index] = value; }

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
        public MachineOperation ProcessAdditions(Machine M)
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
                newPaths.Add(TP.ProcessAdditions(M));

            // Create the list for the output
            List<ToolPath> procPaths = new List<ToolPath>();

            List<ToolPath> levelPaths; // all paths on one level

            // Find path with most levels
            int levels = 0;
            foreach (List<List<ToolPath>> LTP in newPaths)
                if (LTP.Count > levels) levels = LTP.Count;
            // do the roughing layers
            for (int i = 0; i < levels - 1; i++)
            {
                levelPaths = new List<ToolPath>();
                foreach (List<List<ToolPath>> LTP in newPaths)
                    if (i < LTP.Count - 1) levelPaths.AddRange(LTP[i]);
                // sort here (remember to only move chunks that are outside the material!)
                procPaths.AddRange(levelPaths);
            }
            // final cut of everything
            levelPaths = new List<ToolPath>();
            foreach (List<List<ToolPath>> LTP in newPaths)
                levelPaths.AddRange(LTP[LTP.Count - 1]);
            // sort here (remember to only move chunks that are outside the material!)
            procPaths.AddRange(levelPaths);


            return this.copyWithNewPaths(procPaths);
        }



        // Write GCode for this operation
        public void WriteCode(ref CodeInfo Co, Machine M, out ToolPath eP, ToolPath sP = null)
        {
            Co.AppendComment(M.SectionBreak);
            Co.AppendComment("");
            Co.AppendComment(" Operation: " + this.name);
            Co.AppendComment("");
            Co.Append(this.preCode);

            ToolPath oldPath = sP;
            bool inMaterial;
            double Length;
            bool first = true;
            ToolPoint lastPoint; // let the next path know where it is coming from (details like speed and feed can be transferred).

            if (sP == null || sP.Count == 0) { lastPoint = null; }
            else { lastPoint = sP[sP.Count - 1]; }

            foreach (ToolPath TP in this)
            {
                if (TP.Count > 0) // If path has length 0 just ignore
                {
                    if (oldPath != null)
                    {
                        // Create transition move from sP with mf to first point of path array
                        ToolPath Transition = oldPath.TransitionTo(M, TP, out inMaterial, out Length);

                        // Error if changing operations in material

                        if (inMaterial && first)
                        {
                            Co.AddError("Transition between operations might be in material.");
                            Co.AppendComment(" Transition between operations might be in material.");
                        }
                        else if (inMaterial && Length > M.PathJump)
                        {
                            Co.AddError("Long Transition between paths in material. \n"
                                + "To remove this error, don't use ignore, instead change PathJump for the machine from: "
                                + M.PathJump.ToString() + " to at least: " + Length.ToString());
                            Co.AppendComment(" Long Transition between paths in material.");
                        }

                        // Add transition to Code if needed

                        if (Transition.Count > 0)
                            lastPoint = Transition.WriteCode(ref Co, M, lastPoint);
                    }
                    // Add Path to Code

                    lastPoint = TP.WriteCode(ref Co, M, lastPoint);

                    oldPath = TP;
                    first = false;
                }
            }

            Co.Append(this.postCode);
            eP = oldPath;
        }

        // Get the list of tooltip locations
        public List<List<Point3d>> GetPoints()
        {
            List<List<Point3d>> Pts = new List<List<Point3d>>();
            foreach (ToolPath TP in this) { Pts.Add(TP.GetPoints()); }
            return Pts;
        }
        // Get the list of tool directions
        public List<List<Vector3d>> GetDirs()
        {
            List<List<Vector3d>> Dirs = new List<List<Vector3d>>();
            foreach (ToolPath TP in this) { Dirs.Add(TP.GetDirs()); }
            return Dirs;
        }
        // Create a path with the points 
        public List<List<Point3d>> GetPointsandDirs(out List<List<Vector3d>> Dirs)
        {
            List<List<Point3d>> Ptsout = new List<List<Point3d>>();
            Dirs = new List<List<Vector3d>>();
            List<Vector3d> TPDirs;
            foreach (ToolPath TP in this)
            {
                TPDirs = new List<Vector3d>();
                Ptsout.Add(TP.GetPointsandDirs(out TPDirs));
                Dirs.Add(TPDirs);
            }
            return Ptsout;
        }
        // Create a polyline
        public List<PolylineCurve> GetLines()
        {
            List<PolylineCurve> lines = new List<PolylineCurve>();
            foreach(ToolPath TP in this) { lines.Add(TP.GetLine()); }
            return lines;
        }
        // Lines for each toolpoint
        public List<Line> ToolLines()
        {
            List<Line> lines = new List<Line>();
            foreach (ToolPath TP in this) { lines.AddRange(TP.ToolLines()); }
            return lines;
        }

        ICAMel_Base ICAMel_Base.Duplicate()
        {
            return new MachineOperation(this);
        }

        public int IndexOf(ToolPath item)
        {
            return ((IList<ToolPath>)TPs).IndexOf(item);
        }

        public void Insert(int index, ToolPath item)
        {
            ((IList<ToolPath>)TPs).Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            ((IList<ToolPath>)TPs).RemoveAt(index);
        }

        public void Add(ToolPath item)
        {
            ((IList<ToolPath>)TPs).Add(item);
        }

        public void Clear()
        {
            ((IList<ToolPath>)TPs).Clear();
        }

        public bool Contains(ToolPath item)
        {
            return ((IList<ToolPath>)TPs).Contains(item);
        }

        public void CopyTo(ToolPath[] array, int arrayIndex)
        {
            ((IList<ToolPath>)TPs).CopyTo(array, arrayIndex);
        }

        public bool Remove(ToolPath item)
        {
            return ((IList<ToolPath>)TPs).Remove(item);
        }

        public IEnumerator<ToolPath> GetEnumerator()
        {
            return ((IList<ToolPath>)TPs).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IList<ToolPath>)TPs).GetEnumerator();
        }

        internal Curve GetLine()
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
            this.Value = new MachineOperation(Op.Value);
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
                this.Value = new MachineOperation((MachineOperation)source);
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
            foreach (PolylineCurve L in Value.GetLines())
            {
                args.Pipeline.DrawCurve(L, args.Color);
            }
            args.Pipeline.DrawArrows(Value.ToolLines(), args.Color);
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