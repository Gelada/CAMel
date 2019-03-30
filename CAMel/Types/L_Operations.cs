using System;
using System.Collections.Generic;
using CAMel.Types.MaterialForm;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.Types
{
    // Functions to generate operations
    public static class Operations
    {
        private const double _PlaneTolerance = 0.5;
        [NotNull]
        public static MachineOperation opIndex2DCut([NotNull] Curve c, Vector3d d, double oS, [NotNull] ToolPathAdditions tPa, [NotNull] MaterialTool mT, [CanBeNull] IMaterialForm mF)
        {
            // Shift curve to XY plane

            Plane p = new Plane(Point3d.Origin, d);
            if (c.IsPlanar(_PlaneTolerance))
            {
                c.TryGetPlane(out p, _PlaneTolerance);
            }
            Curve uC = c.DuplicateCurve();
            uC.Transform(Transform.PlaneToPlane(p, Plane.WorldXY));
            double uOS = oS;
            if (d * p.ZAxis > 0) { uOS = -uOS;}
            bool reversed = false;
            // ensure the curve is anticlockwise
            if (Math.Abs(uOS) > CAMel_Goo.Tolerance)
            {
                if (uC.ClosedCurveOrientation(Transform.Identity) == CurveOrientation.Clockwise)
                {
                    uC.Reverse();
                    reversed = true;
                    uOS = -uOS;
                }
            }

            // record the average Z location of the curve
            BoundingBox bb = uC.GetBoundingBox(true);
            double useZ = (bb.Max.Z + bb.Min.Z) / 2.0;

            // turn the curve into a Polyline
            PolylineCurve pl = ToolPath.convertAccurate(uC);

            // offSet
            List<PolylineCurve> osC = new List<PolylineCurve>();
            if (Math.Abs(uOS) < CAMel_Goo.Tolerance) { osC.Add(pl); }
            else { osC = Offsetting.offset(pl, uOS * mT.toolWidth / 2.0); }

            if (Math.Abs(uOS) > CAMel_Goo.Tolerance && !reversed) { foreach (PolylineCurve osPl in osC) { osPl.Reverse(); } }

            // create Operation

            MachineOperation mO = new MachineOperation
            {name = "2d Cut Path"};

            int i = 1;
            foreach (PolylineCurve osPl in osC)
            {
                // Create and add name, material/tool and material form
                ToolPath tP = new ToolPath("Cut", mT, mF,tPa);
                if (osC.Count > 1) { tP.name = tP.name + " " + i; }
                i++;

                // return to original orientation

                osPl.Translate(new Vector3d(0, 0, -useZ));
                osPl.Transform(Transform.PlaneToPlane(Plane.WorldXY, p));

                // Add to Operation
                tP.convertCurve(osPl, d);
                mO.Add(tP);
            }
            return mO;
        }

        [NotNull]
        public static MachineOperation opIndex3Axis([NotNull] List<Curve> cs, Vector3d dir, [NotNull] ToolPathAdditions tPa, [CanBeNull] MaterialTool mT, [CanBeNull] IMaterialForm mF, out int invalidCurves)
        {
            MachineOperation mO = new MachineOperation
            { name = "Index 3-Axis Cutting with " + cs.Count + " path" };
            if (cs.Count > 1) { mO.name = mO.name + "s"; }

            int i = 1;

            invalidCurves = 0; // Keep track of any invalid curves.

            foreach (Curve c in cs)
            {
                // Create and add name, material/tool and material form
                ToolPath tP = new ToolPath("Index 3-Axis Path", mT, mF);
                if (cs.Count > 1) { tP.name = tP.name + " " + i; }

                // Additions for toolpath
                tP.additions = tPa;

                // Turn Curve into path

                if (tP.convertCurve(c, dir)) { mO.Add(tP); }
                else { invalidCurves++; }
                i++;
            }
            return mO;
        }

        [NotNull]
        public static MachineOperation drillOperation(Circle d, double peck, [NotNull] MaterialTool mT, [CanBeNull] IMaterialForm mF)
        {
            MachineOperation mO = new MachineOperation
            {
                name = "Drilling depth " + d.Radius.ToString("0.000") + " at (" + d.Center.X.ToString("0.000") + "," + d.Center.Y.ToString("0.000") + "," + d.Center.Z.ToString("0.000") + ")."
            };

            ToolPath tP = new ToolPath(string.Empty, mT, mF)
            {
                additions =
                {
                    insert = true,
                    retract = true,
                    stepDown = false,
                    sdDropStart = false,
                    sdDropMiddle = 0,
                    sdDropEnd = false,
                    threeAxisHeightOffset = false
                }
            };

            // Additions for toolpath
            // we will handle this with peck

            tP.Add(new ToolPoint(d.Center, d.Normal, -1, mT.feedPlunge));

            // calculate the number of pecks we need to do

            int steps;
            if (peck > 0) { steps = (int)Math.Ceiling(d.Radius / peck); }
            else { steps = 1; }

            for (int j = 1; j <= steps; j++)
            {
                tP.Add(new ToolPoint(d.Center - j / (double)steps * d.Radius * d.Normal, d.Normal, -1, mT.feedPlunge));
                tP.Add(new ToolPoint(d.Center, d.Normal, -1, mT.feedPlunge));
            }

            mO.Add(tP);

            return mO;
        }
    }
}
