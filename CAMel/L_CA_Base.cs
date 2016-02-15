using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace CAMel.Types
{
    // base class for all CAMel classes
    public class CA_base
    {
        virtual public string TypeDescription
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        virtual public string TypeName
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }

    // Add a little more standard stuff to GH_Goo
    public class CA_Goo<T> : GH_Goo<T> where T : CA_base
    {
        // Always valid
        public override bool IsValid
        {
            get
            {
                return true;
            }
        }

        public override IGH_Goo Duplicate()
        {
            throw new NotImplementedException();
        }

        public override string TypeDescription
        {
            get { return this.Value.TypeDescription; }
        }

        public override string TypeName
        {
            get { return "GH_" + this.Value.TypeName; }
        }

        public override string ToString()
        {
            return this.Value.ToString();
        }

        public override object ScriptVariable()
        {
            return this.Value;
        }
    }

}
