using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using Rhino.Geometry;
using Rhino.Geometry.Intersect;

using static CAMel.Exceptions;

namespace CAMel.Types.Machine
{
    // Some standards to help develop GCode based machines
    public interface IGCodeMachine : IMachine
    {
        string header { get; }
        string footer { get; }

        string speedChangeCommand { get; }
        string toolChangeCommand { get; }

        string sectionBreak { get; }
        string fileStart { get; }
        string fileEnd { get; }
        string commentStart { get; }
        string commentEnd { get; }

        ToolPoint readTP(Dictionary<char, double> vals, MaterialTool MT);
    }

    public struct TPchanges
    {
        public bool mT { get; set; }
        public bool mF { get; set; }
        public TPchanges(bool mT, bool mF)
        {
            this.mT = mT;
            this.mF = mF;
        }
    }

    public static class Kinematics
    {
        // Collection of Inverse Kinematics 

        // 2-Axis and 3-Axis don't need any work, so they just need writing functions
        // in the GCode library, plus a general purpose linear interpolation.

        static public ToolPoint interpolateLinear(ToolPoint fP, ToolPoint tP, double p)
        {
            ToolPoint TPo = fP.deepClone();
            TPo.pt = tP.pt * p + fP.pt * (1 - p);
            return TPo;
        }

        // 5-Axis...
        // 5-Axis machine have some non-local issues, especially on machines
        // that can rotate fully, so need non-trivial K and IK functions
        //
        // Should really output a machine state type, but not much use for that yet.

        static public Vector3d ikFiveAxisABTable(ToolPoint TP, Vector3d Pivot, double toolLength, out Point3d MachPt)
        {
            // Always gives B from -pi to pi and A from -pi/2 to pi/2.
            double Ao = Math.Asin(TP.dir.Y);
            double Bo = Math.Atan2(-TP.dir.X, TP.dir.Z);

            if (Ao > Math.PI / 2.0)
            {
                Ao = Math.PI - Ao;
                Bo = Bo - Math.PI;
                if (Bo < 0) { Bo = Bo + 2.0 * Math.PI; }
            }

            if (Ao < -Math.PI / 2.0)
            {
                Ao = Math.PI - Ao;
                Bo = Bo - Math.PI;
                if (Bo < 0) { Bo = Bo + 2.0 * Math.PI; }
            }
     
            Point3d OP = TP.pt;

            // rotate from material orientation to machine orientation
            OP.Transform(Transform.Rotation(Bo, Vector3d.YAxis, Point3d.Origin));
            OP.Transform(Transform.Rotation(Ao, Vector3d.XAxis, Point3d.Origin));

            // translate from origin at pivot to origin set so that the tooltip is at machine origin
            OP = OP - Pivot + Vector3d.ZAxis * toolLength;

            MachPt = OP;
            return new Vector3d(Ao, Bo, 0);
        }

        static public ToolPoint kFiveAxisABTable(ToolPoint TP, Vector3d Pivot, double toolLength, Point3d MachPt, Vector3d AB)
        {
            Point3d OP = MachPt;
            // translate from the tooltip at machine origin origin to pivot at origin
            OP = OP + Pivot - Vector3d.ZAxis * toolLength;

            // rotate from machine orientation to material orientation
            OP.Transform(Transform.Rotation(-AB.X, Vector3d.XAxis, Point3d.Origin));
            OP.Transform(Transform.Rotation(-AB.Y, Vector3d.YAxis, Point3d.Origin));

            Vector3d Dir = Vector3d.ZAxis;
            // rotate from machine orientation to material orientation
            Dir.Transform(Transform.Rotation(-AB.X, Vector3d.XAxis, Point3d.Origin));
            Dir.Transform(Transform.Rotation(-AB.Y, Vector3d.YAxis, Point3d.Origin));

            ToolPoint outTP = TP.deepClone();
            outTP.pt = OP;
            outTP.dir = Dir;

            return outTP;
        }

        // Interpolate the machine axes linearly between two positions. 
        // If both axes have full rotation then there are four ways to do this.
        // If lng is true then reverse the direction on the B axis (for PocketNC)

        public static ToolPoint interpolateFiveAxisABTable(Vector3d Pivot, double toolLength, ToolPoint from, ToolPoint to, double p, bool lng)
        {
            Point3d fromMachPt = new Point3d();
            Point3d toMachPt = new Point3d();
            Vector3d fromAB = ikFiveAxisABTable(from, Pivot, toolLength, out fromMachPt);
            Vector3d toAB = ikFiveAxisABTable(to, Pivot, toolLength, out toMachPt);
            Vector3d outAB;
            Point3d outPt;

            outPt = (1 - p) * fromMachPt + p * toMachPt;
            outAB = (1 - p) * fromAB + p * toAB;
            // switch to long way round or short way round depending on gap between angles
            if ((lng && Math.Abs(fromAB.Y - toAB.Y) <= Math.PI) ||
               (!lng && Math.Abs(fromAB.Y - toAB.Y) > Math.PI))
            {
                Vector3d alt;
                if (fromAB.Y > toAB.Y) { alt = new Vector3d(0, 2 * Math.PI, 0); }
                else { alt = new Vector3d(0, -2 * Math.PI, 0); }
                outAB = (1 - p) * fromAB + p * (toAB + alt);
            }
            return kFiveAxisABTable(from, Pivot, toolLength, outPt, outAB);
        }

        public static double angDiffFiveAxisABTable(Vector3d Pivot, double toolLength, ToolPoint fP, ToolPoint tP,bool lng)
        {
            Point3d MachPt = new Point3d();
            Vector3d ang1 = ikFiveAxisABTable(fP, Pivot, toolLength, out MachPt);
            Vector3d ang2 = ikFiveAxisABTable(tP, Pivot, toolLength, out MachPt);

            Vector2d diff = new Vector2d();
            if (lng)
            {
                diff.X = Math.Abs(ang1.X - ang2.X);
                diff.Y = 2 * Math.PI - Math.Abs(ang1.Y - ang2.Y);
            }
            else
            {
                diff.X = Math.Abs(ang1.X - ang2.X);
                diff.Y = Math.Min(Math.Min(Math.Abs(ang1.Y - ang2.Y), Math.Abs(2 * Math.PI + ang1.Y - ang2.Y)), Math.Abs(2 * Math.PI - ang1.Y + ang2.Y));
            }
            return Math.Max(diff.X, diff.Y);
        }

    }

    public static class Utility
    {
        // Arc intended to blend the lead into a path
        // Is constructed in the XY plane finishing in the X direction. 
        private static PolylineCurve leadArc(Point3d startPt, Point3d endPt, Vector3d tangent, double speed, int pts)
        {
            var Pts = new List<Point3d>() { startPt, endPt - tangent*speed, endPt};
            Curve C = NurbsCurve.Create(false, 2,Pts);
            return C.ToPolyline(pts, 0, 0, 0, 0, 0, 0, 0, true);
        }
        private static List<double> curveTests = new List<double>() { 0.7,0.6,0.5,0.8,0.4,0.3,0.9,0.2,0.1,1.0 };

        private static PolylineCurve testLeads(Curve toolL, Point3d startPt, Point3d endPt, Vector3d tangent, int pts)
        {
            PolylineCurve outC = null, testC = null;
            double maxSpeed = Math.Abs((endPt - startPt) * tangent); // assuming tangent is unit vector the component 
            if (maxSpeed > 3.0*(endPt - startPt).Length/pts) // if not too orthogonal try to find a curve
            {
                for (int i = 0; i < curveTests.Count; i++)
                {
                    testC = leadArc(startPt, endPt, tangent, curveTests[i] * maxSpeed*2.0, pts);
                    if(Intersection.CurveCurve(toolL, testC, 0.00001, 0.00001).Count <= 1)
                    {
                        outC = testC;
                        break;
                    }
                }
            }

            return outC;
        }

        private const int leadAngleTest = 50;
        private static PolylineCurve testAngles(Curve toolL, Point3d endPt, Vector3d tangent, double leadLen, int pts)
        {
            // Find the point on a circle furthest from the toolpath from which we can find a suitable lead
            Point3d testPt;
            PolylineCurve leadCurve = null;
            double testdist, dist = -1;
            bool correctSide;
            for (int i = 0; i < leadAngleTest; i++)
            {
                double ang = 2.0 * Math.PI * i / (double)leadAngleTest;
                testPt = endPt + leadLen * new Point3d(Math.Cos(ang), Math.Sin(ang), 0);

                // Check point is inside (or outside) the curve
                correctSide = toolL.Contains(testPt) == PointContainment.Inside;
                if (leadLen > 0) { correctSide = !correctSide; }

                // if on the correct side find the distance to the curve and 
                // update the point if there is a line from point to curve that
                // does not hit material.
                if (correctSide)
                {
                    toolL.ClosestPoint(testPt, out testdist);
                    testdist = testPt.DistanceTo(toolL.PointAt(testdist));
                    // if start is further from the curve try to find a lead path
                    if (testdist > dist)
                    {
                        PolylineCurve C = testLeads(toolL, testPt, endPt, tangent, pts);
                        dist = testdist;
                        if ( C!= null) { leadCurve = C; }
                    }
                }
            }
            return leadCurve;
        }

        public static ToolPath leadInOut2d(ToolPath TP)
        {
            double leadLen = TP.Additions.leadLength;

            // if leadLen is 0, there is nothign to be done.
            if (leadLen == 0) { return TP; }

            ToolPath newTP = TP.deepClone();
            PolylineCurve toolL = TP.getLine();

            PolylineCurve leadIn = testAngles(toolL, toolL.PointAtStart, toolL.TangentAtStart, leadLen, 10);
            PolylineCurve leadOut = testAngles(toolL, toolL.PointAtEnd, -toolL.TangentAtEnd, leadLen, 10);

            // If no suitable curve found throw an error
            if (leadIn == null) { newTP.firstP.addError("No suitable curve for lead in found."); }
            else
            {
                List<ToolPoint> tPts = new List<ToolPoint>();
                for(int i=1; i< leadIn.PointCount;i++)
                {
                    ToolPoint tPt = TP.firstP.deepClone();
                    tPt.pt = leadIn.Point(i);
                    tPts.Add(tPt);
                }
                newTP.InsertRange(0, tPts);
            }
            if (leadOut == null) { newTP.firstP.addError("No suitable curve for lead out found."); }
            else
            {
                leadOut.Reverse();
                for (int i = 1; i < leadOut.PointCount; i++)
                {
                    ToolPoint tPt = TP.firstP.deepClone();
                    tPt.pt = leadOut.Point(i);
                    newTP.Add(tPt);
                }
            }

            newTP.Additions.leadLength = 0;
            newTP.Additions.insert = false;
            newTP.Additions.retract = false;
            return newTP;
        }

    }

    public static class GCode
    {
        // Standard terms

        internal static readonly string defaultCommentStart = "(";
        internal static readonly string defaultCommentEnd = ")";
        internal static readonly string defaultSectionBreak = "------------------------------------------";
        internal static readonly string defaultSpeedChangeCommand = "M03";
        internal static readonly string defaultToolChangeCommand = "G43H";
        internal static readonly string defaultInsertCommand = "M61";
        internal static readonly string defaultRetractCommand = "M62";
        internal static readonly string defaultFileStart = string.Empty;
        internal static readonly string defaultFileEnd = string.Empty;

        // Formatting structure for GCode

        static public void gcInstStart(IGCodeMachine M, ref CodeInfo Co, MachineInstruction MI, ToolPath startPath)
        {
            if (MI[0][0].matTool == null) { matToolException(); }
            if (MI[0][0].matForm == null) { matFormException(); }
            Co.currentMT = MI[0][0].matTool;
            Co.currentMF = MI[0][0].matForm;

            DateTime thisDay = DateTime.Now;
            Co.appendLineNoNum(M.fileStart);
            Co.appendComment(M.sectionBreak);
            if (MI.name != string.Empty) { Co.appendComment(MI.name); }
            Co.appendComment("");
            Co.appendComment(" Machine Instructions Created " + thisDay.ToString("f"));
            Co.appendComment("  by " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " "
                + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            if (M.name != string.Empty) { Co.appendComment("  for " + M.name); }
            Co.appendComment(" Starting with: ");
            Co.appendComment("  Tool: " + MI[0][0].matTool.toolName);
            Co.appendComment("  in " + MI[0][0].matTool.matName + " with shape " + MI[0][0].matForm.ToString());
            Co.appendComment("");
            Co.appendComment(M.sectionBreak);
            Co.append(M.header);
            Co.append(MI.preCode);
            Co.currentMT = new MaterialTool(); // Clear the tool information so we call a tool change. 
            M.writeCode(ref Co, startPath);
        }
        static public void gcInstEnd(IGCodeMachine M, ref CodeInfo Co, MachineInstruction MI, ToolPath finalPath, ToolPath endPath)
        {
            Co.appendComment(M.sectionBreak);
            M.writeTransition(ref Co, finalPath, endPath, true);
            M.writeCode(ref Co, endPath);

            Co.appendComment(M.sectionBreak);
            Co.appendComment(" End of ToolPaths");
            Co.appendComment(M.sectionBreak);

            Co.append(MI.postCode);
            Co.append(M.footer);
            Co.appendLineNoNum(M.fileEnd);
        }

        static public void gcOpStart(IGCodeMachine M, ref CodeInfo Co, MachineOperation MO)
        {
            Co.appendComment(M.sectionBreak);
            Co.appendComment("");
            Co.appendComment(" Operation: " + MO.name);
            Co.appendComment("");
            Co.append(MO.preCode);
        }
        static public void gcOpEnd(IGCodeMachine M, ref CodeInfo Co, MachineOperation MO)
        {
            Co.appendComment(MO.postCode);
        }

        static public TPchanges gcPathStart(IGCodeMachine M, ref CodeInfo Co, ToolPath tP)
        {
            if (tP.matTool == null) { matToolException(); }
            if (tP.matForm == null) { matFormException(); }
            TPchanges ch = new TPchanges(false, false);
            Co.appendComment(M.sectionBreak);
            bool preamble = false;
            if (tP.name != string.Empty)
            {
                Co.appendComment(" ToolPath: " + tP.name);
                preamble = true;
            }
            if (tP.matTool != null && tP.matTool.toolName != Co.currentMT.toolName)
            {
                Co.appendComment(" using: " + tP.matTool.toolName + " into " + tP.matTool.matName);
                if (M.toolLengthCompensation && tP.matTool.toolNumber != Co.currentMT.toolNumber) {
                    Co.append(M.toolChangeCommand + tP.matTool.toolNumber);
                }
                Co.currentMT = tP.matTool;
                ch.mT = true;
                preamble = true;
            }
            if (tP.matForm != null && tP.matForm.ToString() != Co.currentMF.ToString())
            {
                Co.appendComment(" material: " + tP.matForm.ToString());
                Co.currentMF = tP.matForm;
                ch.mF = true;
                preamble = true;
            }

            if (preamble) { Co.appendComment(M.sectionBreak); }

            Co.append(tP.preCode);
            return ch;
        }
        static public void gcPathEnd(IGCodeMachine M, ref CodeInfo Co, ToolPath TP)
        {
            Co.append(TP.postCode);
        }

        // Toolpoint writers
        // These might be simpler to pull 
        // into a single "write" command taking a dictionary?
        static public string gcTwoAxis(ToolPoint TP)
        {
            Point3d OP = TP.pt;
            string GPoint = string.Empty;
            GPoint += "X" + OP.X.ToString("0.000") + " Y" + OP.Y.ToString("0.000");

            return GPoint;
        }
        static public string gcThreeAxis(ToolPoint TP)
        {
            Point3d OP = TP.pt;
            string GPoint = string.Empty;
            GPoint += "X" + OP.X.ToString("0.000") + " Y" + OP.Y.ToString("0.000") + " Z" + OP.Z.ToString("0.000");

            return GPoint;
        }
        static public string gcFiveAxisAB(Point3d machPt, Vector3d AB)
        {
            StringBuilder GPtBd = new StringBuilder(@"X", 34);
            GPtBd.Append(machPt.X.ToString("0.000"));
            GPtBd.Append(@" Y"); GPtBd.Append(machPt.Y.ToString("0.000"));
            GPtBd.Append(@" Z"); GPtBd.Append(machPt.Z.ToString("0.000"));
            GPtBd.Append(@" A"); GPtBd.Append((180.0 * AB.X / Math.PI).ToString("0.000"));
            GPtBd.Append(@" B"); GPtBd.Append((180.0 * AB.Y / Math.PI).ToString("0.000"));

            return GPtBd.ToString();
        }

        // GCode reading
        private static readonly Regex numbPattern = new Regex(@"^([0-9\-.]+)", RegexOptions.Compiled);

        static private double getValue(string line, char split, double old, ref bool changed, ref bool unset)
        {
            double val = old;
            string[] splitLine = line.Split(split);
            if (splitLine.Length > 1)
            {
                Match monkey = numbPattern.Match(splitLine[1]);
                if (monkey.Success)
                { val = double.Parse(monkey.Value); }
                if (val != old) { changed = true; }
            }
            if (double.IsNaN(val)) { unset = true; }
            return val;
        }
        // TODO detect tool changes and new paths
        static public ToolPath gcRead(IGCodeMachine M, List<MaterialTool> MTs, string Code, List<char> terms)
        {
            ToolPath TP = new ToolPath(); Dictionary<char, double> vals = new Dictionary<char, double>();

            foreach (char c in terms) { vals.Add(c, double.NaN); }

            bool changed, unset;

            using (StringReader reader = new StringReader(Code))
            {
                // Loop over the lines in the string.
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    changed = false;
                    unset = false;
                    foreach (char t in terms)
                    { vals[t] = getValue(line, t, vals[t], ref changed, ref unset); }
                    //interpret a G0 command.
                    if (line.Contains(@"G00") || line.Contains(@"G0 "))
                    {
                        if (vals.ContainsKey('F') && vals['F'] != 0)
                        {
                            changed = true;
                            vals['F'] = 0;
                        }
                    }
                    if (changed && !unset) { TP.Add(M.readTP(vals, MTs[0])); }
                }
            }
            return TP;
        }
    }
}