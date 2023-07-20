using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimplifyCSharp
{
    public class SimplificationHelpers
    {
        public static IList<T> Simplify<T>(
            IList<T> points,
            Func<T, T, Boolean> equalityChecker,
            Func<T, double> xExtractor, 
            Func<T, double> yExtractor, 
            Func<T, double> zExtractor,
            Func<T, double> rExtractor,
            Func<T, double> sExtractor,
            Func<T, double> tExtractor,
            double tolerance = 1.0,
            bool highestQuality = false)
        {
            var simplifier6D = new Simplifier6D<T>(equalityChecker, xExtractor, yExtractor, zExtractor, rExtractor, sExtractor,tExtractor);
            return simplifier6D.Simplify(points, tolerance, highestQuality);
        }

        public static IList<T> Simplify<T>(
            IList<T> points,
            Func<T, T, Boolean> equalityChecker,
            Func<T, double> xExtractor,
            Func<T, double> yExtractor,
            Func<T, double> zExtractor,
            double tolerance = 1.0,
            bool highestQuality = false)
        {
            var simplifier3D = new Simplifier3D<T>(equalityChecker, xExtractor, yExtractor, zExtractor);
            return simplifier3D.Simplify(points, tolerance, highestQuality);
        }

        public static IList<T> Simplify<T>(
            IList<T> points,
            Func<T, T, Boolean> equalityChecker,
            Func<T, double> xExtractor,
            Func<T, double> yExtractor,
            double tolerance = 1.0,
            bool highestQuality = false)
        {
            var simplifier2D = new Simplifier2D<T>(equalityChecker, xExtractor, yExtractor);
            return simplifier2D.Simplify(points, tolerance, highestQuality);
        }
    }
}
