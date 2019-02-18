using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Rhino.Geometry;

using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Expressions;
using JetBrains.Annotations;

namespace CAMel.Types
{
    public interface ICAMelBase
    {
        [NotNull] string TypeDescription { get; }
        [NotNull] [PublicAPI] string TypeName { get; }

        [NotNull] string ToString();
    }

    // Add a little more standard stuff to GH_Goo
    public class CAMel_Goo<T> : GH_Goo<T> where T : class, ICAMelBase
    {
        // Valid if not null
        public override bool IsValid => this.Value != null;

        public override IGH_Goo Duplicate()
        {
            throw new NotImplementedException("Camel_Base object has not implemented its duplicate command.");
        }

        [NotNull] public override string TypeDescription => this.Value?.TypeDescription ?? "Value of CAMel Grasshopper wrapper currently set to null.";

        [NotNull] public override string TypeName => typeof(T).Name;

        public override string ToString() => this.Value?.ToString() ?? "CAMel type currently set to null.";

        [CanBeNull] public override object ScriptVariable() => this.Value;
    }

    internal static class CAMel_Goo
    {
        [NotNull]
        public static string doubleToCsv([CanBeNull] IEnumerable<double> values, [NotNull] string format)
        {
            // Adapted from https://www.dotnetperls.com/convert-list-string

            if (values == null) { return string.Empty; }

            StringBuilder builder = new StringBuilder();
            foreach (double d in values)
            {
                // Append each int to the StringBuilder overload.
                builder.Append(d.ToString(format)).Append(", ");
            }

            string result = builder.ToString();
            result = result.TrimEnd(',', ' ');

            return result;
        }
        [NotNull]
        public static List<double> cSvToDouble([CanBeNull] string values)
        {
            List<double> result = new List<double>();
            // Adapted from https://www.dotnetperls.com/convert-list-string
            if (values == null) { return result; }

            string[] parts = values.Split(','); // Call Split method
            List<string> list = new List<string>(parts); // Use List constructor
            foreach (string item in list)
            {
                if (double.TryParse(item, out double p)) { result.Add(p); }
                else
                {
                    GH_ExpressionParser parser = new GH_ExpressionParser(false);
                    GH_Variant value = parser.Evaluate(item);
                    if (value?.Type == GH_VariantType.@double || value?.Type == GH_VariantType.@int)
                    {
                        result.Add(value._Double);
                    }
                }
            }
            return result;
        }

        // convert to cylindrical coordinate
        public static Point3d toCyl(Point3d pt)
        {
            Vector3d plPt = new Vector3d(pt.X, pt.Y, 0);
            double angle = Math.Atan2(pt.Y, pt.X);
            if (angle < 0) { angle = angle + Math.PI * 2.0; }
            return new Point3d(plPt.Length, angle, pt.Z);
        }

        // convert from cylindrical coordinate
        public static Point3d fromCyl(Point3d pt)
        {
            return new Point3d(pt.X * Math.Cos(pt.Y), pt.X * Math.Sin(pt.Y), pt.Z);
        }

        [CanBeNull]
        public static object cleanGooList([CanBeNull] object gooey)
        {
            switch (gooey)
            {
                case IGH_Goo goo:
                    return goo.ScriptVariable();
                case IEnumerable objs:
                {
                    List<object> oP = new List<object>();
                    foreach (object obj in objs)
                    {
                        if (obj is IGH_Goo ghGoo) { oP.Add(ghGoo.ScriptVariable()); }
                        else { oP.Add(obj); }
                    }
                    return oP;
                }
                default:
                    return gooey;
            }
        }

        public const double Tolerance = 0.000000001;
    }
}
