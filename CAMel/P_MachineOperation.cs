using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace CAMel.Types
{
    // List of toolpaths forming a general Operation of the machine,
    // from the complex to the simple
    // creating a surface, drilling a whole, cutting out an object...
    // When paths within an operation have a stepdown then all first 
    // step downs with be completed, then the second and so on.
    public class MachineOperation : IToolPointContainer
    {
        public List<ToolPath> TPs;

        // Default Constructor
        public MachineOperation()
        {
            this.TPs = new List<ToolPath>();
            this.name = "";
            this.localCode = "";
        }
        // Just name
        public MachineOperation(string name)
        {
            this.name = name;
            this.TPs = new List<ToolPath>();
            this.localCode = "";
        }
        // Copy Constructor
        public MachineOperation(MachineOperation Op)
        {
            this.name = Op.name;
            this.localCode = Op.localCode;
            this.TPs = new List<ToolPath>();
            foreach (ToolPath TP in Op.TPs)
            {
                this.TPs.Add(new ToolPath(TP));
            }
        }

        // Return with new paths.
        public MachineOperation copyWithNewPaths(List<ToolPath> procPaths)
        {
            MachineOperation outOp = new MachineOperation();
            outOp.localCode = this.localCode;
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

        public string localCode { get; set; }

        public bool IsValid
        {
            get
            {
                throw new NotImplementedException("Machine Operation has not implemented IsValid");
            }
        }

        public override string ToString()
        {
            int total_TP = 0;
            foreach (ToolPath TP in this.TPs)
            {
                total_TP = total_TP + TP.Pts.Count;
            }
            return "Machine Operation: " + this.name + ", " + this.TPs.Count + " toolpaths, " + total_TP + " total tool points.";
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

            foreach (ToolPath TP in this.TPs)
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
            Co.AppendLine(M.SectionBreak);
            Co.AppendLine(M.CommentChar.ToString() + M.endCommentChar);
            Co.AppendLine(M.CommentChar + " Operation: " + this.name + M.endCommentChar);
            Co.AppendLine(M.CommentChar.ToString() + M.endCommentChar);
            if (this.localCode != "") Co.Append(this.localCode);

            ToolPath oldPath = sP;
            bool inMaterial;
            double Length;
            bool first = true;
            ToolPoint lastPoint; // let the next path know where it is coming from (details like speed and feed can be transferred).

            if (sP == null || sP.Pts.Count == 0) { lastPoint = null; }
            else { lastPoint = sP.Pts[sP.Pts.Count - 1]; }

            foreach (ToolPath TP in this.TPs)
            {
                if (TP.Pts.Count > 0) // If path has length 0 just ignore
                {
                    if (oldPath != null)
                    {
                        // Create transition move from sP with mf to first point of path array
                        ToolPath Transition = oldPath.TransitionTo(M, TP, out inMaterial, out Length);

                        // Error if changing operations in material

                        if (inMaterial && first)
                        {
                            Co.AddError("Transition between operations might be in material.");
                            Co.AppendLine(M.CommentChar + " Transition between operations might be in material." + M.endCommentChar);
                        }
                        else if (inMaterial && Length > M.PathJump)
                        {
                            Co.AddError("Long Transition between paths in material. \n"
                                + "To remove this error, don't use ignore, instead change PathJump for the machine from: "
                                + M.PathJump.ToString() + " to at least: " + Length.ToString());
                            Co.AppendLine(M.CommentChar + " Long Transition between paths in material." + M.endCommentChar);
                        }

                        // Add transition to Code if needed

                        if (Transition.Pts.Count > 0)
                            lastPoint = Transition.WriteCode(ref Co, M, lastPoint);
                    }
                    // Add Path to Code

                    lastPoint = TP.WriteCode(ref Co, M, lastPoint);

                    oldPath = TP;
                    first = false;
                }
            }
            eP = oldPath;
        }

        // Give the lists of paths as polyline
        public List<List<Point3d>> RawPaths(out List<List<Vector3d>> Dirs)
        {
            List<List<Point3d>> paths = new List<List<Point3d>>();
            List<Vector3d> dirs = null;
            Dirs = new List<List<Vector3d>>();
            foreach(ToolPath TP in this.TPs)
            {
                paths.Add(TP.RawPath(out dirs));
                Dirs.Add(dirs);
            }
            return paths;
        }

        ICAMel_Base ICAMel_Base.Duplicate()
        {
            return new MachineOperation(this);
        }
    }

    // Grasshopper Type Wrapper
    public class GH_MachineOperation : CAMel_Goo<MachineOperation>
    {
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