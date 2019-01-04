﻿using System;
using System.Collections.Generic;

using Rhino.Geometry;

using CAMel.Types.MaterialForm;

namespace CAMel.Types
{

    // Functions to generate operations
    public static class Operations
    {
        public static MachineOperation opIndex2dCut(Curve C, Vector3d D, double oS, double leadInOut, bool tabs, MaterialTool MT, IMaterialForm MF)
        {
            // Shift curve to XY plane

            Plane P = new Plane(Point3d.Origin, D);
            C.Transform(Transform.PlaneToPlane(P, Plane.WorldXY));

            // ensure the curve is anticlockwise
            if (oS != 0)
            {
                if (C.ClosedCurveOrientation(Transform.Identity) == CurveOrientation.Clockwise)
                {
                    C.Reverse();
                }
            }

            // record the average Z location of the curve
            BoundingBox BB = C.GetBoundingBox(true);
            double useZ = (BB.Max.Z + BB.Min.Z) / 2.0;

            // turn the curve into a Polyline
            PolylineCurve PL = ToolPath.convertAccurate(C);

            // offSet
            List<PolylineCurve> osC = new List<PolylineCurve>();
            if (oS == 0) { osC.Add(PL); }
            else { osC = Offsetting.offset(PL, oS * MT.toolWidth / 2.0); }

            // create Operation

            MachineOperation Op = new MachineOperation
            { name = "2d Cut Path" };
            ToolPath TP;
            List<PolylineCurve> Cs = new List<PolylineCurve>();
            int i = 1;
            foreach (PolylineCurve c in osC)
            {
                // Create and add name, material/tool and material form
                TP = new ToolPath("Cut", MT, MF);
                if (osC.Count > 1) { TP.name = TP.name + " " + i.ToString(); }
                i++;

                // Additions for toolpath
                TP.Additions.insert = true;
                TP.Additions.retract = true;
                TP.Additions.stepDown = true;
                TP.Additions.sdDropStart = true;
                TP.Additions.sdDropMiddle = 8 * MF.safeDistance;
                TP.Additions.sdDropEnd = true;
                TP.Additions.threeAxisHeightOffset = true;
                TP.Additions.tabbing = tabs;
                TP.Additions.leadFactor = leadInOut;

                // return to original orientation

                c.Translate(new Vector3d(0, 0, -useZ));
                c.Transform(Transform.PlaneToPlane(Plane.WorldXY, P));

                // Add to Operation
                TP.convertCurve(c, D);
                Op.Add(TP);
                Cs.Add(TP.getLine());
            }
            return Op;
        }

        public static MachineOperation opIndex3Axis(List<Curve> C, Vector3d dir, MaterialTool MT, IMaterialForm MF, out int InvalidCurves)
        {
            MachineOperation Op = new MachineOperation
            { name = "Index 3-Axis Cutting with " + C.Count.ToString() + " path" };
            if (C.Count > 1) { Op.name = Op.name + "s"; }

            ToolPath TP;
            int i = 1;

            InvalidCurves = 0; // Keep track of any invalid curves.

            foreach (Curve c in C)
            {
                // Create and add name, material/tool and material form
                TP = new ToolPath("Index 3-Axis Path", MT, MF);
                if (C.Count > 1) { TP.name = TP.name + " " + i.ToString(); }

                // Additions for toolpath
                TP.Additions.insert = true;
                TP.Additions.retract = true;
                TP.Additions.stepDown = true;
                TP.Additions.sdDropStart = true;
                TP.Additions.sdDropMiddle = 8 * MF.safeDistance;
                TP.Additions.sdDropEnd = true;
                TP.Additions.threeAxisHeightOffset = true;

                // Turn Curve into path

                if (TP.convertCurve(c, dir)) { Op.Add(TP); }
                else { InvalidCurves++; }
                i++;
            }
            return Op;
        }
    }
}
