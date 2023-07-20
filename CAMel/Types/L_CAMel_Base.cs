namespace CAMel.Types
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;

    using Grasshopper.Kernel.Expressions;
    using Grasshopper.Kernel.Types;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    /// <summary>TODO The CAMelBase interface.</summary>
    public interface ICAMelBase
    {
        /// <summary>Gets the type description.</summary>
        [NotNull]
        string TypeDescription { get; }
        /// <summary>Gets the type name.</summary>
        [NotNull, PublicAPI]
        string TypeName { get; }

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        [NotNull]
        string ToString();
    }

    /// <inheritdoc />
    /// <summary>Add a little more standard stuff to GH_Goo.</summary>
    /// <typeparam name="T"></typeparam>
    public class CAMel_Goo<T> : GH_Goo<T> where T : class, ICAMelBase
    {
        // Valid if not null
        /// <inheritdoc />
        public override bool IsValid => this.Value != null;

        /// <inheritdoc />
        public override IGH_Goo Duplicate() => throw new NotImplementedException("Camel_Base object has not implemented its duplicate command.");

        /// <inheritdoc />
        [NotNull]
        public override string TypeDescription => this.Value?.TypeDescription ?? "Value of CAMel Grasshopper wrapper currently set to null.";

        /// <inheritdoc />
        [NotNull]
        public override string TypeName => typeof(T).Name;

        /// <inheritdoc />
        public override string ToString() => this.Value?.ToString() ?? "CAMel type currently set to null.";

        /// <inheritdoc />
        [CanBeNull]
        public override object ScriptVariable() => this.Value;
    }

    /// <summary>TODO The ca mel_ goo.</summary>
    internal static class CAMel_Goo
    {
        /// <summary>TODO The double to csv.</summary>
        /// <param name="values">TODO The values.</param>
        /// <param name="format">TODO The format.</param>
        /// <returns>The <see cref="string"/>.</returns>
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

        /// <summary>Convert comma seprated list to list of double</summary>
        /// <param name="values">Comma separated string of numbers</param>
        /// <returns> List of numbers
        /// .</returns>
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
        /// <summary>TODO The to cyl.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <returns>The <see cref="Point3d"/>.</returns>
        public static Point3d toCyl(Point3d pt)
        {
            Vector3d plPt = new Vector3d(pt.X, pt.Y, 0);
            double angle = Math.Atan2(pt.Y, pt.X);
            if (angle < 0) { angle += Math.PI * 2.0; }
            return new Point3d(plPt.Length, angle, pt.Z);
        }

        // convert from cylindrical coordinate
        /// <summary>TODO The from cyl.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <returns>The <see cref="Point3d"/>.</returns>
        public static Point3d fromCyl(Point3d pt) => new Point3d(pt.X * Math.Cos(pt.Y), pt.X * Math.Sin(pt.Y), pt.Z);

        // find radius of smallest sphere with given origin containing a bounding box
        /// <summary>TODO The bound sphere.</summary>
        /// <param name="bb">TODO The bb.</param>
        /// <param name="c">TODO The c.</param>
        /// <returns>The <see cref="double"/>.</returns>
        public static double boundSphere(BoundingBox bb, Point3d c) => c.DistanceTo(bb.FurthestPoint(c));

        /// <summary>TODO The from io.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <returns>The <see cref="Point3d"/>.</returns>
        public static Point3d fromIO(GH_IO.Types.GH_Point3D pt) => new Point3d(pt.x, pt.y, pt.z);
        /// <summary>TODO The from io.</summary>
        /// <param name="pl">TODO The pl.</param>
        /// <returns>The <see cref="Plane"/>.</returns>
        public static Plane fromIO(GH_IO.Types.GH_Plane pl) =>
            new Plane(fromIO(pl.Origin), fromIO(pl.XAxis), fromIO(pl.YAxis));
        /// <summary>TODO The to io.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <returns>The <see cref="GH_IO.Types.GH_Point3D"/>.</returns>
        [UsedImplicitly]
        public static GH_IO.Types.GH_Point3D toIO(Point3d pt) =>
            new GH_IO.Types.GH_Point3D(pt.X, pt.Y, pt.Z);
        /// <summary>TODO The to io.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <returns>The <see cref="GH_IO.Types.GH_Point3D"/>.</returns>
        public static GH_IO.Types.GH_Point3D toIO(Vector3d pt) =>
            new GH_IO.Types.GH_Point3D(pt.X, pt.Y, pt.Z);
        /// <summary>TODO The to io.</summary>
        /// <param name="pl">TODO The pl.</param>
        /// <returns>The <see cref="GH_Plane"/>.</returns>
        public static GH_IO.Types.GH_Plane toIO(Plane pl) =>
            new GH_IO.Types.GH_Plane(toIO(pl.Origin), toIO(pl.XAxis), toIO(pl.YAxis));
        /// <summary>TODO The clean goo list.</summary>
        /// <param name="gooey">TODO The gooey.</param>
        /// <returns>The <see cref="object"/>.</returns>
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

        /// <summary>TODO The tolerance.</summary>
        public const double Tolerance = 0.000000001;
        public const double surfaceEdge = 0.001;
    }
}
