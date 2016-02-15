using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;

namespace CAMel.Types
{
    public class ToolPointContainer : CA_base
    {
        public string name;
        public string localCode;

        public ToolPointContainer()
        {
            this.name = "";
            this.localCode = "";
        }

        public override string TypeDescription
        {
            get { throw new NotImplementedException(); }
        }

        public override string TypeName
        {
            get { throw new NotImplementedException(); }
        }

        virtual public ToolPointContainer Duplicate()
        {
            throw new NotImplementedException();
        }
    }

    public class GH_ToolPointContainer<T> : CA_Goo<T> where T : ToolPointContainer
    {
        // Strip off our hierarchy and plonk it into a tree 
        public GH_Structure<GH_ToolPoint> TreeOfPoints()
        {
            throw new NotImplementedException();
        }
    }
}
