namespace CAMel.Types
{
    using System;
    using System.Collections.Generic;

    using CAMel.Types.MaterialForm;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    // Functions to generate operations
    public static class Operations
    {
        private const double PlaneTolerance = 0.5;
        [NotNull]
        public static MachineOperation opIndex2DCut([NotNull] Curve c, Vector3d d, double oS, [NotNull] ToolPathAdditions tPa, [NotNull] MaterialTool mT, [CanBeNull] IMaterialForm mF)
        {
            // Shift curve to XY plane
            Plane p = new Plane(Point3d.Origin, d);
            if (c.IsPlanar(PlaneTolerance))
            {
                c.TryGetPlane(out p, PlaneTolerance);
            }

            double uOS = oS;
            if (d * p.ZAxis > 0) { uOS = -uOS; }

            // create Operation
            MachineOperation mO = new MachineOperation { name = "2d Cut " };

            ToolPath tP = new ToolPath("", mT, mF, tPa);
            tP.convertCurve(c, d);
            tP.additions.offset = mT.toolWidth * uOS * p.ZAxis;

            mO.Add(tP);

            return mO;
        }

        [NotNull]
        public static MachineOperation opIndex3Axis([NotNull] List<Curve> cs, Vector3d dir, [NotNull] ToolPathAdditions tPa, [CanBeNull] MaterialTool mT, [CanBeNull] IMaterialForm mF, out int invalidCurves)
        {
            MachineOperation mO = new MachineOperation
                {
                    name = "Index 3-Axis Cutting with " + cs.Count + " path"
                };
            if (cs.Count > 1) { mO.name += "s"; }

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

            /* Additions for toolpath
            // we will handle this with peck*/

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
