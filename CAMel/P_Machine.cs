using System;
using System.Collections.Generic;
using System.Text;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using CAMel.Types.MaterialForm;
using System.Text.RegularExpressions;

namespace CAMel.Types.Machine
{
    // Settings for a machine (this is the POST!)
    // This is the only place we handle language
    // So other languages can be used.

    // TODO create a machine state class for each machine
    //  currently using CodeInfo to store a dictionary of values. 
    //  a bespoke version for each machine type would be better

    // Main interface and public face of the machine
    public interface IMachine : ICAMel_Base
    {
        string name { get; }
        double pathJump { get; } // Maximum jump between toolpaths in material
        bool toolLengthCompensation { get; } // Tool Length Compensation
        ToolPathAdditions defaultTPA { get; }
        List<MaterialTool> MTs { get; } // list of Material Tools used by machine

        string comment(string L);

        void writeFileStart(ref CodeInfo Co, MachineInstruction MI, ToolPath startPath);
        void writeFileEnd(ref CodeInfo Co, MachineInstruction MI, ToolPath finalPath,ToolPath endPath);
        void writeOpStart(ref CodeInfo Co, MachineOperation MO);
        void writeOpEnd(ref CodeInfo Co, MachineOperation MO);
        void writeCode(ref CodeInfo Co, ToolPath tP);
        void writeTransition(ref CodeInfo Co, ToolPath fP, ToolPath tP, bool first);

        ToolPath readCode(string Code);

        ToolPath insertRetract(ToolPath tP);
        Vector3d toolDir(ToolPoint TP);
        ToolPoint interpolate(ToolPoint fP, ToolPoint tP, MaterialTool MT, double par, bool lng);
        double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool MT, bool lng); // max change for orientation axes
}

 

    // Grasshopper Type Wrapper
    public class GH_Machine : CAMel_Goo<IMachine>
    {
        // Default constructor
        public GH_Machine() { this.Value = null; }
        // Unwrapped type
        public GH_Machine(IMachine M) { this.Value = M; }
        // Copy Constructor (just reference as Machine is Immutable)
        public GH_Machine(GH_Machine M) { this.Value = M.Value;  }
        // Duplicate
        public override IGH_Goo Duplicate() { return new GH_Machine(this); }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(IMachine)))
            {
                object ptr = this.Value;
                target = (Q)ptr;
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