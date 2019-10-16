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
    /// <inheritdoc />
    /// <summary>TODO The Machine interface.</summary>
    public interface IMachine : ICAMelBase
    {
        /// <summary>Gets the name.</summary>
        [NotNull]
        string name { get; }
        /// <summary>Gets a value indicating whether tool length compensation.</summary>
        bool toolLengthCompensation { get; } // Tool Length Compensation
        // ReSharper disable once InconsistentNaming
        /// <summary>Gets the default tpa.</summary>
        [NotNull]
        ToolPathAdditions defaultTPA { get; }
        /// <summary>Gets the m ts.</summary>
        [NotNull]
        List<MaterialTool> mTs { get; } // list of Material Tools used by machine

        // Writing and reading code
        /// <summary>Gets the extension.</summary>
        [NotNull]
        string extension { get; }

        /// <summary>TODO The comment.</summary>
        /// <param name="l">TODO The l.</param>
        /// <returns>The <see cref="string"/>.</returns>
        [NotNull]
        string comment([NotNull] string l);
        /// <summary>TODO The line number.</summary>
        /// <param name="l">TODO The l.</param>
        /// <param name="line">TODO The line.</param>
        /// <returns>The <see cref="string"/>.</returns>
        [NotNull]
        string lineNumber([NotNull] string l, int line);

        /// <summary>TODO The write file start.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mI">TODO The m i.</param>
        void writeFileStart([NotNull] ref CodeInfo co, [NotNull] MachineInstruction mI);
        /// <summary>TODO The write file end.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mI">TODO The m i.</param>
        void writeFileEnd([NotNull] ref CodeInfo co, [NotNull] MachineInstruction mI);
        /// <summary>TODO The write op start.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mO">TODO The m o.</param>
        void writeOpStart([NotNull] ref CodeInfo co, [NotNull] MachineOperation mO);
        /// <summary>TODO The write op end.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="mO">TODO The m o.</param>
        void writeOpEnd([NotNull] ref CodeInfo co, [NotNull] MachineOperation mO);
        /// <summary>TODO The write code.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="tP">TODO The t p.</param>
        void writeCode([NotNull] ref CodeInfo co, [NotNull] ToolPath tP);

        /// <summary>TODO The tool change.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="toolNumber">TODO The tool number.</param>
        void toolChange([NotNull] ref CodeInfo co, int toolNumber);
        /// <summary>TODO The jump check.</summary>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="double"/>.</returns>
        [UsedImplicitly]
        double jumpCheck([NotNull] ToolPath fP, [NotNull] ToolPath tP);
        /// <summary>TODO The jump check.</summary>
        /// <param name="co">TODO The co.</param>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        void jumpCheck([NotNull] ref CodeInfo co, [NotNull] ToolPath fP, [NotNull] ToolPath tP);

        /// <summary>TODO The read code.</summary>
        /// <param name="code">TODO The code.</param>
        /// <returns>The <see cref="MachineInstruction"/>.</returns>
        [NotNull]
        MachineInstruction readCode([NotNull] string code);

        // Functions needed to process additions
        /// <summary>TODO The refine.</summary>
        /// <param name="toolPath">TODO The tool path.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        [NotNull]
        ToolPath refine([NotNull] ToolPath toolPath);
        /// <summary>TODO The off set.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull, ItemNotNull]
        List<ToolPath> offSet([NotNull] ToolPath tP);
        /// <summary>TODO The insert retract.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        List<ToolPath> insertRetract([NotNull] ToolPath tP);
        /// <summary>TODO The step down.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull]
        List<List<ToolPath>> stepDown([NotNull] ToolPath tP);
        /// <summary>TODO The three axis height offset.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        [NotNull]
        ToolPath threeAxisHeightOffset([NotNull] ToolPath tP);
        /// <summary>TODO The finish paths.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull, ItemNotNull]
        List<ToolPath> finishPaths([NotNull] ToolPath tP);
        /// <summary>TODO The transition.</summary>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        [NotNull]
        ToolPath transition([NotNull] ToolPath fP, [NotNull] ToolPath tP);

        // Machine movement
        /// <summary>TODO The tool dir.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="Vector3d"/>.</returns>
        Vector3d toolDir([NotNull] ToolPoint tP);
        /// <summary>TODO The interpolate.</summary>
        /// <param name="fP">TODO The f p.</param>
        /// <param name="tP">TODO The t p.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="par">TODO The par.</param>
        /// <param name="lng">TODO The lng.</param>
        /// <returns>The <see cref="ToolPoint"/>.</returns>
        [NotNull]
        ToolPoint interpolate([NotNull] ToolPoint fP, [NotNull] ToolPoint tP, [NotNull] MaterialTool mT, double par, bool lng);
        /// <summary>TODO The ang diff.</summary>
        /// <param name="tP1">TODO The t p 1.</param>
        /// <param name="tP2">TODO The t p 2.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="lng">TODO The lng.</param>
        /// <returns>The <see cref="double"/>.</returns>
        [UsedImplicitly]
        double angDiff([NotNull] ToolPoint tP1, [NotNull] ToolPoint tP2, [NotNull] MaterialTool mT, bool lng); // max change for orientation axes
    }

    // Grasshopper Type Wrapper
    /// <inheritdoc />
    /// <summary>TODO The g h_ machine.</summary>
    public sealed class GH_Machine : CAMel_Goo<IMachine>
    {
        // Default constructor
        /// <summary>Initializes a new instance of the <see cref="GH_Machine"/> class.</summary>
        [UsedImplicitly]
        public GH_Machine() => this.Value = null;

        // Unwrapped type
        /// <summary>Initializes a new instance of the <see cref="GH_Machine"/> class.</summary>
        /// <param name="m">TODO The m.</param>
        public GH_Machine([CanBeNull] IMachine m) => this.Value = m;

        // Copy Constructor (just reference as Machine is Immutable)
        /// <summary>Initializes a new instance of the <see cref="GH_Machine"/> class.</summary>
        /// <param name="m">TODO The m.</param>
        public GH_Machine([CanBeNull] GH_Machine m) => this.Value = m?.Value;

        // Duplicate
        /// <inheritdoc />
        /// <summary>TODO The duplicate.</summary>
        /// <returns>The <see cref="T:Grasshopper.Kernel.Types.IGH_Goo" />.</returns>
        [NotNull]
        public override IGH_Goo Duplicate() => new GH_Machine(this);

        /// <inheritdoc />
        /// <summary>TODO The cast to.</summary>
        /// <param name="target">TODO The target.</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>The <see cref="T:System.Boolean" />.</returns>
        public override bool CastTo<T>(ref T target)
        {
            if (!typeof(T).IsAssignableFrom(typeof(IMachine))) { return false; }

            object ptr = this.Value;
            target = (T)ptr;
            return true;
        }

        /// <inheritdoc />
        /// <summary>TODO The cast from.</summary>
        /// <param name="source">TODO The source.</param>
        /// <returns>The <see cref="T:System.Boolean" />.</returns>
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
    /// <inheritdoc />
    /// <summary>TODO The g h_ machine par.</summary>
    public class GH_MachinePar : GH_Param<GH_Machine>
    {
        /// <summary>Initializes a new instance of the <see cref="GH_MachinePar"/> class.</summary>
        public GH_MachinePar()
            : base(
                "Machine", "Machine",
                "Contains a collection of information on CNC machines",
                "CAMel", "  Params", GH_ParamAccess.item) { }
        /// <inheritdoc />
        /// <summary>TODO The component guid.</summary>
        public override Guid ComponentGuid => new Guid("df6dcfa2-510e-4613-bdae-3685b094e7d7");
        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.machine;
    }
}