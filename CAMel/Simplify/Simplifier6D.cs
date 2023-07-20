//Code added to handle multiaxis paths

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimplifyCSharp
{
    public class Simplifier6D<T>: BaseSimplifier<T>
    {
        readonly Func<T, double> _xExtractor;
        readonly Func<T, double> _yExtractor;
        readonly Func<T, double> _zExtractor;
        readonly Func<T, double> _rExtractor;
        readonly Func<T, double> _sExtractor;
        readonly Func<T, double> _tExtractor;

        public Simplifier6D(Func<T, T, Boolean> equalityChecker,
            Func<T, double> xExtractor, Func<T, double> yExtractor, Func<T, double> zExtractor,
            Func<T, double> rExtractor, Func<T, double> sExtractor, Func<T, double> tExtractor) :
            base(equalityChecker)
        {
            _xExtractor = xExtractor;
            _yExtractor = yExtractor;
            _zExtractor = zExtractor;
            _rExtractor = rExtractor;
            _sExtractor = sExtractor;
            _tExtractor = tExtractor;
        }

        protected override double GetSquareDistance(T p1, T p2)
        {
            double dx = _xExtractor(p1) - _xExtractor(p2);
            double dy = _yExtractor(p1) - _yExtractor(p2);
            double dz = _zExtractor(p1) - _zExtractor(p2);
            double dr = _rExtractor(p1) - _rExtractor(p2);
            double ds = _sExtractor(p1) - _sExtractor(p2);
            double dt = _tExtractor(p1) - _tExtractor(p2);

            return dx * dx + dy * dy + dz * dz + dr * dr + ds * ds + dt * dt;
        }

        protected override double GetSquareSegmentDistance(T p0, T p1, T p2)
        {
            double x0, y0, z0, r0, s0, t0, x1, y1, z1, r1, s1, t1, x2, y2, z2, r2, s2, t2, dx, dy, dz, dr, ds, dt, t;

            x1 = _xExtractor(p1);
            y1 = _yExtractor(p1);
            z1 = _zExtractor(p1);
            r1 = _rExtractor(p1);
            s1 = _sExtractor(p1);
            t1 = _tExtractor(p1);
            x2 = _xExtractor(p2);
            y2 = _yExtractor(p2);
            z2 = _zExtractor(p2);
            r2 = _rExtractor(p2);
            s2 = _sExtractor(p2);
            t2 = _tExtractor(p2);
            x0 = _xExtractor(p0);
            y0 = _yExtractor(p0);
            z0 = _zExtractor(p0);
            r0 = _rExtractor(p0);
            s0 = _sExtractor(p0);
            t0 = _tExtractor(p0);

            dx = x2 - x1;
            dy = y2 - y1;
            dz = z2 - z1;
            dr = r2 - r1;
            ds = s2 - s1;
            dt = t2 - t1;

            if (dx != 0.0d || dy != 0.0d || dz != 0.0d || dr != 0.0d || ds != 0.0d || dt != 0.0d)
            {
                t = ((x0 - x1) * dx + (y0 - y1) * dy + (z0 - z1) * dz + (r0 - r1) * dr + (s0 - s1) * ds + (t0 - t1) * dt)
                        / (dx * dx + dy * dy + dz * dz);

                if (t > 1.0d)
                {
                    x1 = x2;
                    y1 = y2;
                    z1 = z2;
                    r1 = r2;
                    s1 = s2;
                    t1 = t2;
                }
                else if (t > 0.0d)
                {
                    x1 += dx * t;
                    y1 += dy * t;
                    z1 += dz * t;
                    r1 += dr * t;
                    s1 += ds * t;
                    t1 += dt * t;
                }
            }

            dx = x0 - x1;
            dy = y0 - y1;
            dz = z0 - z1;
            dr = r0 - r1;
            ds = s0 - s1;
            dt = t0 - t1;

            return dx * dx + dy * dy + dz * dz + dr * dr + ds * ds + dt * dt;
        }
    }
}
