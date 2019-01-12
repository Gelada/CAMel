using System;
using System.Collections.Generic;

using Rhino.Geometry;

using CAMel.Types.MaterialForm;

namespace CAMel.Types
{

    // Functions to generate operations
    public static class Operations
    {
        public static MachineOperation opIndex2dCut(Curve C, Vector3d D, double oS, ToolPathAdditions TPA, MaterialTool MT, IMaterialForm MF)
        {
            if (MT == null) { Exceptions.matToolException(); }
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
                TP = new ToolPath("Cut", MT, MF,TPA);
                if (osC.Count > 1) { TP.name = TP.name + " " + i.ToString(); }
                i++;

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
            if(MF == null) { Exceptions.matFormException(); }
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

        public static MachineOperation drillOperation(Circle D, double peck, MaterialTool MT, IMaterialForm MF)
        {
            if (MT == null) { Exceptions.matToolException(); }
            MachineOperation Op = new MachineOperation
            {
                name = "Drilling depth " + D.Radius.ToString("0.000") + " at (" + D.Center.X.ToString("0.000") + "," + D.Center.Y.ToString("0.000") + "," + D.Center.Z.ToString("0.000") + ")."
            };

            ToolPath TP = new ToolPath(string.Empty, MT, MF);

            // Additions for toolpath
            TP.Additions.insert = true;
            TP.Additions.retract = true;
            TP.Additions.stepDown = false; // we will handle this with peck
            TP.Additions.sdDropStart = false;
            TP.Additions.sdDropMiddle = 0;
            TP.Additions.sdDropEnd = false;
            TP.Additions.threeAxisHeightOffset = false;

            TP.Add(new ToolPoint(D.Center, D.Normal, -1, MT.feedPlunge));

            // calculate the number of pecks we need to do

            int steps;
            if (peck > 0) { steps = (int)Math.Ceiling(D.Radius / peck); }
            else { steps = 1; }

            for (int j = 1; j <= steps; j++)
            {
                TP.Add(new ToolPoint(D.Center - ((double)j / (double)steps) * D.Radius * D.Normal, D.Normal, -1, MT.feedPlunge));
                TP.Add(new ToolPoint(D.Center, D.Normal, -1, MT.feedPlunge));
            }

            Op.Add(TP);

            return Op;
        }
    }
}
