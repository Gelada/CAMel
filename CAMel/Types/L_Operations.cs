namespace CAMel.Types
{
    using System;
    using System.Collections.Generic;

    using CAMel.Types.MaterialForm;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    // Functions to generate operations
    /// <summary>TODO The operations.</summary>
    public static class Operations
    {
        /// <summary>TODO The plane tolerance.</summary>
        private const double PlaneTolerance = 0.5;
        /// <summary>TODO The op index 2 d cut.</summary>
        /// <param name="c">TODO The c.</param>
        /// <param name="d">TODO The d.</param>
        /// <param name="oS">TODO The o s.</param>
        /// <param name="tPa">TODO The t pa.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="mF">TODO The m f.</param>
        /// <returns>The <see cref="MachineOperation"/>.</returns>
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

            ToolPath tP = new ToolPath(string.Empty, mT, mF, tPa);
            tP.convertCurve(c, d);
            tP.additions.offset = mT.toolWidth * uOS * p.ZAxis / 2.0;
            if (oS > 0) { tP.side = CutSide.Left; }
            else { tP.side = CutSide.Right; }

            mO.Add(tP);

            return mO;
        }

        /// <summary>TODO The op index 3 axis.</summary>
        /// <param name="cs">TODO The cs.</param>
        /// <param name="dir">TODO The dir.</param>
        /// <param name="tPa">TODO The t pa.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="mF">TODO The m f.</param>
        /// <param name="invalidCurves">TODO The invalid curves.</param>
        /// <returns>The <see cref="MachineOperation"/>.</returns>
        [NotNull]
        public static MachineOperation opIndex3Axis([NotNull] List<Curve> cs, [NotNull] List<Vector3d> dir, [NotNull] List<ToolPathAdditions> tPa, [NotNull] List<MaterialTool> mT, [NotNull] List<IMaterialForm> mF, out int invalidCurves)
        {
            MachineOperation mO = new MachineOperation
                {
                    name = "Index 3-Axis Cutting with " + cs.Count + " path"
                };
            if (cs.Count > 1) { mO.name += "s"; }

            int i = 0;

            invalidCurves = 0; // Keep track of any invalid curves.

            MaterialTool uMT = null;
            IMaterialForm uMF = null;
            ToolPathAdditions utPa = ToolPathAdditions.temp;
            Vector3d uDir = Vector3d.ZAxis;

            foreach (Curve c in cs)
            {
                if (i < mT.Count) { uMT = mT[i]; }
                if (i < mF.Count) { uMF = mF[i]; }
                if (i < tPa.Count) { utPa = tPa[i]; }
                if (i < dir.Count) { uDir = dir[i]; }

                // Create and add name, material/tool and material form
                ToolPath tP = new ToolPath("Index 3-Axis Path", uMT, uMF);
                if (cs.Count > 1) { tP.name = tP.name + " " + (i + 1); }

                // Additions for toolpath
                tP.additions = utPa;

                // Turn Curve into path
                if (tP.convertCurve(c, uDir, 1)) { mO.Add(tP); }
                else { invalidCurves++; }
                i++;
            }

            return mO;
        }

        /// <summary>TODO The drill operation.</summary>
        /// <param name="d">TODO The d.</param>
        /// <param name="peck">TODO The peck.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="mF">TODO The m f.</param>
        /// <returns>The <see cref="MachineOperation"/>.</returns>
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
                            stepDown = 0,
                            sdDropStart = false,
                            sdDropMiddle = 0,
                            sdDropEnd = false,
                            threeAxisHeightOffset = false
                        }
                };

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
