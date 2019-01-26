using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Rhino.Geometry;

using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Expressions;

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
        public static string doubleToCSV(IEnumerable<double> vals, string format)
        {
            // Adapted from https://www.dotnetperls.com/convert-list-string
            StringBuilder builder = new StringBuilder();
            foreach (double d in vals)
            {
                // Append each int to the StringBuilder overload.
                builder.Append(d.ToString(format)).Append(", ");
            }
            string result = builder.ToString();
            result = result.TrimEnd(',',' ');
            return result;
        }
        public static List<double> cSVToDouble(string vals)
        {
            List<double> result = new List<double>();
            // Adapted from https://www.dotnetperls.com/convert-list-string
            string[] parts = vals.Split(','); // Call Split method
            List<string> list = new List<string>(parts); // Use List constructor
            foreach (string item in list)
            {
                double p = 0;
                if (double.TryParse(item, out p)) { result.Add(p); }
                else
                {
                    var parser = new GH_ExpressionParser(false);
                    var val = parser.Evaluate(item);
                    if(val.Type == GH_VariantType.@double || val.Type == GH_VariantType.@int) { result.Add(val._Double); }
                }
            }
            return result;
        }

        // convert to cylindrical coordinate
        public static Point3d toCyl(Point3d Pt)
        {
            Vector3d PlPt = new Vector3d(Pt.X, Pt.Y, 0);
            double angle = Math.Atan2(Pt.Y, Pt.X);
            if (angle < 0) { angle = angle + Math.PI * 2.0; }
            return new Point3d(PlPt.Length, angle, Pt.Z);
        }

        // convert from cylindrical coordinate
        public static Point3d fromCyl(Point3d Pt)
        {
            return new Point3d(Pt.X * Math.Cos(Pt.Y), Pt.X * Math.Sin(Pt.Y), Pt.Z);
        }

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
