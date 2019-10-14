namespace CAMel.Types
{
    using JetBrains.Annotations;

    using Rhino.Geometry;

    public interface IToolPointContainer : ICAMelBase
    {
        [NotNull, UsedImplicitly] string name { get; set; }
        [NotNull, UsedImplicitly] string preCode { get; set; }
        [NotNull, UsedImplicitly] string postCode { get; set; }

        [NotNull] ToolPath getSinglePath();

        [UsedImplicitly] BoundingBox getBoundingBox();

        [CanBeNull, UsedImplicitly] ToolPoint firstP { get; }
        [CanBeNull, UsedImplicitly] ToolPoint lastP { get; }
    }
}
