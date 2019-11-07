

namespace CAMel.Types.Machine
{
    using Rhino.Geometry;

    /// <summary>Store the state of a machine. </summary>
    internal class MachineState : ICAMelBase
    {
        /// <summary>The Tool position and Orientation</summary>
        private Plane tDir1;

        /// <summary>Gets or sets the Tool position and Orientation</summary>
        internal Plane tDir { get => this.tDir1; set => this.tDir1 = value; }

        /// <summary>Gets or sets the Material position and Orientation</summary>
        internal Plane mDir { get; set; } // Material position and Orientation

        /// <summary>Gets or sets the pt.</summary>
        internal Point3d pt // Tool Tip position
        {
            get => this.tDir.Origin;
            set => this.tDir1.Origin = value;
        }

        /// <summary>Gets or sets the machine speed. </summary>
        public double speed { get; set; }
        /// <summary>Gets or sets the machine feed rate. </summary>
        public double feed { get; set; }

        /// <summary>Gets or sets the current cut quality.</summary>
        public int quality { get; set; }

        /// <inheritdoc />
        public string TypeDescription => "Store the state of a machine. ";
        /// <inheritdoc />
        public string TypeName => "Machine State";
    }
}
