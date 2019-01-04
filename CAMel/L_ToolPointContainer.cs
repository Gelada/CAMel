using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;

namespace CAMel.Types
{
    public interface IToolPointContainer : ICAMel_Base
    {
        string name { get; set; }
        string preCode { get; set; }
        string postCode { get; set; }

        ToolPath getSinglePath();

        ToolPoint firstP { get; }
        ToolPoint lastP { get; }
    }
}
