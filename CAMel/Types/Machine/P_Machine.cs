namespace CAMel.Types.Machine
{
    using System;
    using System.Collections.Generic;

    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Types;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    // Settings for a machine (this is the POST!)
    // This is the only place we handle language
    // So other languages can be used.

    // TODO create a machine state class for each machine
    //  currently using CodeInfo to store a dictionary of values.
    //  a bespoke version for each machine type would be better

    // Main interface and public face of the machine
    public interface IMachine : ICAMelBase
    {
        [NotNull] string name { get; }
        bool toolLengthCompensation { get; } // Tool Length Compensation
        // ReSharper disable once InconsistentNaming
        [NotNull] ToolPathAdditions defaultTPA { get; }
        [NotNull] List<MaterialTool> mTs { get; } // list of Material Tools used by machine

        // Writing and reading code
        [NotNull] string extension { get; }

        [NotNull] string comment([NotNull] string l);
        [NotNull] string lineNumber([NotNull] string l, int line);

        void writeFileStart([NotNull] ref CodeInfo co, [NotNull] MachineInstruction mI);
        void writeFileEnd([NotNull] ref CodeInfo co, [NotNull] MachineInstruction mI);
        void writeOpStart([NotNull] ref CodeInfo co, [NotNull] MachineOperation mO);
        void writeOpEnd([NotNull] ref CodeInfo co, [NotNull] MachineOperation mO);
        void writeCode([NotNull] ref CodeInfo co, [NotNull] ToolPath tP);

        //void writeTransition([NotNull] ref CodeInfo co, [NotNull] ToolPath fP, [NotNull] ToolPath tP, bool first);
        void toolChange([NotNull] ref CodeInfo co, int toolNumber);
        [UsedImplicitly] double jumpCheck([NotNull] ToolPath fP, [NotNull] ToolPath tP);
        void jumpCheck([NotNull] ref CodeInfo co, [NotNull] ToolPath fP, [NotNull] ToolPath tP);

        [NotNull] MachineInstruction readCode([NotNull] string code);

        // Functions needed to process additions
        [NotNull] ToolPath refine([NotNull] ToolPath toolPath);
        [NotNull, ItemNotNull] List<ToolPath> offSet([NotNull] ToolPath tP);
        [NotNull] List<ToolPath> insertRetract([NotNull] ToolPath tP);
        [NotNull] List<List<ToolPath>> stepDown([NotNull] ToolPath tP);
        [NotNull] ToolPath threeAxisHeightOffset([NotNull] ToolPath tP);
        [NotNull, ItemNotNull] List<ToolPath> finishPaths([NotNull] ToolPath tP);
        [NotNull] ToolPath transition([NotNull] ToolPath fP, [NotNull] ToolPath tP);

        // Machine movement
        Vector3d toolDir([NotNull] ToolPoint tP);
        [NotNull] ToolPoint interpolate([NotNull] ToolPoint fP, [NotNull] ToolPoint tP, [NotNull] MaterialTool mT, double par, bool lng);
        [UsedImplicitly] double angDiff([NotNull] ToolPoint tP1, [NotNull] ToolPoint tP2, [NotNull] MaterialTool mT, bool lng); // max change for orientation axes
    }

    // Grasshopper Type Wrapper
    public sealed class GH_Machine : CAMel_Goo<IMachine>
    {
        // Default constructor
        [UsedImplicitly]
        public GH_Machine() => this.Value = null;

        // Unwrapped type
        public GH_Machine([CanBeNull] IMachine m) => this.Value = m;

        // Copy Constructor (just reference as Machine is Immutable)
        public GH_Machine([CanBeNull] GH_Machine m) => this.Value = m?.Value;

        // Duplicate
        [NotNull]
        public override IGH_Goo Duplicate() => new GH_Machine(this);

        public override bool CastTo<T>(ref T target)
        {
            if (!typeof(T).IsAssignableFrom(typeof(IMachine))) { return false; }

            object ptr = this.Value;
            target = (T)ptr;
            return true;
        }

        public override bool CastFrom([CanBeNull] object source)
        {
            switch (source) {
                case null: return false;
                case IMachine m:
                    this.Value = m;
                    return true;
                default: return false;
            }
        }
    }

    // Grasshopper Parameter Wrapper
    public class GH_MachinePar : GH_Param<GH_Machine>
    {
        public GH_MachinePar()
            : base(
                "Machine", "Machine",
                "Contains a collection of information on CNC machines",
                "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid => new Guid("df6dcfa2-510e-4613-bdae-3685b094e7d7");
        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.machine;
    }
}