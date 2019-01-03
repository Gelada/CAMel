using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using CAMel.Types.MaterialForm;
using System.Text.RegularExpressions;

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
        //public static Curve blendIn(Point3d fPt, Point3d bPt, Vector3d Dir, double sAng, double eAng)
        //{
        //    if(sAng == eAng) // straight line
        //    {
        //
        //    }
        //}



        public static ToolPath leadInOut2d(ToolPath TP, double lead)
        {
            double leadLen = lead * TP.Additions.leadFactor;
            ToolPath newTP = TP.deepClone();
            PolylineCurve toolL = TP.getLine();

            // Find the point on a circle furthest from the toolpath. 
            int testNumber = 50;
            Point3d LeadStart = new Point3d(), testPt;
            double testdist, dist = -1;
            bool noInter, correctSide;
            for (int i = 0; i < testNumber; i++)
            {
                double ang = 2.0 * Math.PI * i / (double)testNumber;
                testPt = TP.firstP.pt + leadLen * new Point3d(Math.Cos(ang), Math.Sin(ang), 0);

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
                    noInter = Intersection.CurveCurve(toolL, new Line(TP.firstP.pt, testPt).ToNurbsCurve(), 0.00001, 0.00001).Count <= 1;

                    if (noInter && testdist > dist)
                    {
                        dist = testdist;
                        LeadStart = testPt;
                    }
                }
            }
            // If no suitable point found throw an error, otherwise add point to 
            // start and end
            if (dist < 0)
            {
                newTP.firstP.addError("No suitable point for lead in and out found.");
            }
            else
            {
                ToolPoint LeadTP = TP.firstP.deepClone();
                LeadTP.pt = LeadStart;
                newTP.Add(LeadTP.deepClone());
                newTP.Insert(0, LeadTP);
            }

            newTP.Additions.leadFactor = 0;
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

        // Formatting structure for GCode

        static public void gcInstStart(IGCodeMachine M, ref CodeInfo Co, MachineInstruction MI, ToolPath startPath)
        {

            Co.currentMT = MI[0][0].matTool;
            Co.currentMF = MI[0][0].matForm;

            DateTime thisDay = DateTime.Now;
            Co.AppendLineNoNum(M.fileStart);
            Co.AppendComment(M.sectionBreak);
            if (MI.name != "") { Co.AppendComment(MI.name); }
            Co.AppendComment("");
            Co.AppendComment(" Machine Instructions Created " + thisDay.ToString("f"));
            Co.AppendComment("  by " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " "
                + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            if (M.name != "") { Co.AppendComment("  for " + M.name); }
            Co.AppendComment(" Starting with: ");
            Co.AppendComment("  Tool: " + MI[0][0].matTool.toolName);
            Co.AppendComment("  in " + MI[0][0].matTool.matName + " with shape " + MI[0][0].matForm.ToString());
            Co.AppendComment("");
            Co.AppendComment(M.sectionBreak);
            Co.Append(M.header);
            Co.Append(MI.preCode);

            M.writeCode(ref Co, startPath);
        }
        static public void gcInstEnd(IGCodeMachine M, ref CodeInfo Co, MachineInstruction MI, ToolPath finalPath, ToolPath endPath)
        {
            Co.AppendComment(M.sectionBreak);
            M.writeTransition(ref Co, finalPath, endPath, true);
            M.writeCode(ref Co, endPath);

            Co.AppendComment(M.sectionBreak);
            Co.AppendComment(" End of ToolPaths");
            Co.AppendComment(M.sectionBreak);

            Co.Append(MI.postCode);
            Co.Append(M.footer);
            Co.AppendLineNoNum(M.fileEnd);
        }

        static public void gcOpStart(IGCodeMachine M, ref CodeInfo Co, MachineOperation MO)
        {
            Co.AppendComment(M.sectionBreak);
            Co.AppendComment("");
            Co.AppendComment(" Operation: " + MO.name);
            Co.AppendComment("");
            Co.Append(MO.preCode);
        }
        static public void gcOpEnd(IGCodeMachine M, ref CodeInfo Co, MachineOperation MO)
        {
            Co.AppendComment(MO.postCode);
        }

        static public TPchanges gcPathStart(IGCodeMachine M, ref CodeInfo Co, ToolPath TP)
        {
            TPchanges ch = new TPchanges(false, false);
            Co.AppendComment(M.sectionBreak);
            bool preamble = false;
            if (TP.name != "")
            {
                Co.AppendComment(" ToolPath: " + TP.name);
                preamble = true;
            }
            if (Co.currentMT == null || TP.matTool.toolName != Co.currentMT.toolName)
            {
                Co.AppendComment(" using: " + TP.matTool.toolName + " into " + TP.matTool.matName);
                Co.currentMT = TP.matTool;
                if (M.toolLengthCompensation) { Co.Append(M.toolChangeCommand + TP.matTool.toolNumber); }
                ch.mT = true;
                preamble = true;
            }
            if (Co.currentMF == null || TP.matForm.ToString() != Co.currentMF.ToString())
            {
                Co.AppendComment(" material: " + TP.matForm.ToString());
                Co.currentMF = TP.matForm;
                ch.mF = true;
                preamble = true;
            }

            if (preamble) { Co.AppendComment(M.sectionBreak); }

            Co.Append(TP.preCode);
            return ch;
        }
        static public void gcPathEnd(IGCodeMachine M, ref CodeInfo Co, ToolPath TP)
        {
            Co.Append(TP.postCode);
        }

        // Toolpoint writers
        // These might be simpler to pull 
        // into a single "write" command taking a dictionary?
        static public string gcTwoAxis(ToolPoint TP)
        {
            Point3d OP = TP.pt;
            string GPoint = "";
            GPoint += "X" + OP.X.ToString("0.000") + " Y" + OP.Y.ToString("0.000");

            return GPoint;
        }
        static public string gcThreeAxis(ToolPoint TP)
        {
            Point3d OP = TP.pt;
            string GPoint = "";
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
        // Give only orientation move
        static public string gcFiveAxisAB_orient(Point3d machPt, Vector3d AB)
        {
            String GPoint = "";
            GPoint += "A" + (180.0 * AB.X / Math.PI).ToString("0.000") + " B" + (180.0 * AB.Y / Math.PI).ToString("0.000");

            return GPoint;
        }

        // GCode reading
        private static Regex numbPattern = new Regex(@"^([0-9\-.]+)", RegexOptions.Compiled);

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