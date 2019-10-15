namespace CAMel.Types
{
    using JetBrains.Annotations;

    using Rhino.Geometry;

    /// <inheritdoc />
    /// <summary>TODO The ToolPointContainer interface.</summary>
    public interface IToolPointContainer : ICAMelBase
    {
        /// <summary>Gets or sets the name.</summary>
        [NotNull, UsedImplicitly]
        string name { get; set; }
        /// <summary>Gets or sets the pre code.</summary>
        [NotNull, UsedImplicitly]
        string preCode { get; set; }
        /// <summary>Gets or sets the post code.</summary>
        [NotNull, UsedImplicitly]
        string postCode { get; set; }

        /// <summary>TODO The get single path.</summary>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        [NotNull]
        ToolPath getSinglePath();

        /// <summary>TODO The get bounding box.</summary>
        /// <returns>The <see cref="BoundingBox"/>.</returns>
        [UsedImplicitly]
        BoundingBox getBoundingBox();

        /// <summary>Gets the first p.</summary>
        [CanBeNull, UsedImplicitly]
        ToolPoint firstP { get; }
        /// <summary>Gets the last p.</summary>
        [CanBeNull, UsedImplicitly]
        ToolPoint lastP { get; }
    }
}
