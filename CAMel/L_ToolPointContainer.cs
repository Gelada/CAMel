using Rhino.Geometry;

namespace CAMel.Types
{
    public interface IToolPointContainer : ICAMel_Base
    {
        string name { get; set; }
        string preCode { get; set; }
        string postCode { get; set; }
        
        ToolPath getSinglePath();
        BoundingBox getBoundingBox();

        ToolPoint firstP { get; }
        ToolPoint lastP { get; }
    }
}
