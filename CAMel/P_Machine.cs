using System;
using System.Collections.Generic;

using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace CAMel.Types.Machine
{
    // Settings for a machine (this is the POST!)
    // This is the only place we handle language
    // So other languages can be used.

    // TODO create a machine state class for each machine
    //  currently using CodeInfo to store a dictionary of values. 
    //  a bespoke version for each machine type would be better

    // Main interface and public face of the machine
    public interface IMachine : ICAMelBase
    {
        string name { get; }
        double pathJump { get; } // Maximum jump between toolpaths in material
        bool toolLengthCompensation { get; } // Tool Length Compensation
        ToolPathAdditions defaultTPA { get; }
        List<MaterialTool> mTs { get; } // list of Material Tools used by machine

        // Writing and reading code
        string comment(string l);

        void writeFileStart(ref CodeInfo co, MachineInstruction mI, ToolPath startPath);
        void writeFileEnd(ref CodeInfo co, MachineInstruction mI, ToolPath finalPath,ToolPath endPath);
        void writeOpStart(ref CodeInfo co, MachineOperation mO);
        void writeOpEnd(ref CodeInfo co, MachineOperation mO);
        void writeCode(ref CodeInfo co, ToolPath tP);
        void writeTransition(ref CodeInfo co, ToolPath fP, ToolPath tP, bool first);

        ToolPath readCode(string code);

        // Functions needed to process additions
        ToolPath insertRetract(ToolPath tP);
        List<List<ToolPath>> stepDown(ToolPath tP);
        ToolPath threeAxisHeightOffset(ToolPath tP);
        List<ToolPath> finishPaths(ToolPath tP);

        // Machine movement
        Vector3d toolDir(ToolPoint tP);
        ToolPoint interpolate(ToolPoint fP, ToolPoint tP, MaterialTool mT, double par, bool lng);
        double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool mT, bool lng); // max change for orientation axes
    }

 

    // Grasshopper Type Wrapper
    public sealed class GH_Machine : CAMel_Goo<IMachine>
    {
        // Default constructor
        public GH_Machine() { this.Value = null; }
        // Unwrapped type
        public GH_Machine(IMachine m) { this.Value = m; }
        // Copy Constructor (just reference as Machine is Immutable)
        public GH_Machine(GH_Machine m) { this.Value = m.Value;  }
        // Duplicate
        public override IGH_Goo Duplicate() { return new GH_Machine(this); }

        public override bool CastTo<T>(ref T target)
        {
            if (typeof(T).IsAssignableFrom(typeof(IMachine)))
            {
                object ptr = this.Value;
                target = (T)ptr;
                return true;
            }
            return false;
        }

        public override bool CastFrom(object source)
        {
            if (source == null) { return false; }
            if (typeof(IMachine).IsAssignableFrom(source.GetType()))
            {
                this.Value = (IMachine)source;
                return true;
            }
            return false;
        }
    }

    // Grasshopper Parameter Wrapper
    public class GH_MachinePar : GH_Param<GH_Machine>
    {
        public GH_MachinePar() :
            base("Machine", "Machine", "Contains a collection of information on CNC machines", "CAMel", "  Params", GH_ParamAccess.item)
        { }
        public override Guid ComponentGuid
        {
            get { return new Guid("df6dcfa2-510e-4613-bdae-3685b094e7d7"); }
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
                return Properties.Resources.machine;
            }
        }
    }

}