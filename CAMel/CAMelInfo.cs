// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CAMelInfo.cs" company="">
//   
// </copyright>
// <summary>
//   Defines the CAMelInfo type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace CAMel
{
    using System;
    using System.Drawing;

    using Grasshopper.Kernel;

    using JetBrains.Annotations;

    /// <inheritdoc />
    /// <summary>
    /// Information on the CAMel Grasshopper Assembly
    /// </summary>
    [UsedImplicitly]
    public class CAMelInfo : GH_AssemblyInfo
    {
        /// <inheritdoc />
        [NotNull]
        public override string Name => "CAMel";

        /// <inheritdoc />
        [CanBeNull]
        public override Bitmap Icon => Properties.Resources.CAMel;

        /// <inheritdoc />
        [NotNull]
        public override string Description => "CAMel: Tools to roll your own CNC solutions";

        /// <inheritdoc />
        public override Guid Id => new Guid("78ce1cc3-d79f-48d5-af54-9bb4f794186b");

        /// <inheritdoc />
        [NotNull]
        public override string AuthorName => "Edmund Harriss";

        /// <inheritdoc />
        [NotNull]
        public override string AuthorContact => "http://www.Camel3d.com";
    }

    /// <inheritdoc />
    /// <summary>TODO The ca mel category.</summary>
    [UsedImplicitly]
    public class CAMelCategory : GH_AssemblyPriority
    {
        /// <inheritdoc />
        /// <summary>TODO The priority load.</summary>
        /// <returns>The <see cref="T:Grasshopper.Kernel.GH_LoadingInstruction" />.</returns>
        /// <exception cref="T:System.ArgumentNullException"></exception>
        public override GH_LoadingInstruction PriorityLoad()
        {
            if (Grasshopper.Instances.ComponentServer == null) { throw new ArgumentNullException(); }
            Grasshopper.Instances.ComponentServer.AddCategoryIcon("CAMel", Properties.Resources.BW_CAMel);
            Grasshopper.Instances.ComponentServer.AddCategoryShortName("CAMel", "CMl");
            Grasshopper.Instances.ComponentServer.AddCategorySymbolName("CAMel", 'C');
            return GH_LoadingInstruction.Proceed;
        }
    }
}
