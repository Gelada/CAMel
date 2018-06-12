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
    }

    public class GH_ToolPointContainer<T> : CAMel_Goo<T> where T : IToolPointContainer
    {
        // Strip off our hierarchy and plonk it into a tree 
        public GH_Structure<GH_ToolPoint> TreeOfPoints()
        {
            throw new NotImplementedException("ToolPoint Container has not yet implemented conversion to a tree of points.");
        }
    }
}
