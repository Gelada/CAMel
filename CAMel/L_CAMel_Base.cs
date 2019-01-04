using System;
using System.Collections;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace CAMel.Types
{
    public interface ICAMel_Base
    {
        string TypeDescription { get; }
        string TypeName { get; }
        
        string ToString();
    }

    // Add a little more standard stuff to GH_Goo
    public class CAMel_Goo<T> : GH_Goo<T> where T : ICAMel_Base
    {
        // Valid if not null
        public override bool IsValid
        {
            get {
                if (this.Value == null) { return false; }
                return true;
            }
        }

        public override IGH_Goo Duplicate()
        {
            throw new NotImplementedException("Camel_Base object has not implemented its duplicate command.");
        }

        public override string TypeDescription
        {
            get
            {
                if (this.Value == null) { return "CAMel type currently set to null."; }
                return this.Value.TypeDescription;
            }
        }

        public override string TypeName { get { return typeof(T).Name; } }

        public override string ToString()
        {
            if (this.Value == null) { return "CAMel type currently set to null."; }
            return this.Value.ToString();
        }

        public override object ScriptVariable() => this.Value;
    }
    static class CAMel_Goo
    {
        public static object cleanGooList(object Gooey)
        {
            if (Gooey is IGH_Goo) { return ((IGH_Goo)Gooey).ScriptVariable(); }
            if (Gooey is IEnumerable)
            {
                List<object> oP = new List<object>();
                foreach (object obj in (IEnumerable)Gooey)
                {
                    if (obj is IGH_Goo) { oP.Add(((IGH_Goo)obj).ScriptVariable()); }
                    else { oP.Add(obj); }
                }
                return oP;
            }
            else { return Gooey; }
        }
    }
}
