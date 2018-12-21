using System;
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
        string header { get; set; }
        string footer { get; set; }

        string speedChangeCommand { get; set; }

        string sectionBreak { get; set; }
        string fileStart { get; set; }
        string fileEnd { get; set; }
        string commentStart { get; set; }
        string commentEnd { get; set; }

        ToolPoint readTP(Dictionary<char, double> vals);
    }

    public struct TPchanges
    {
        public bool MT { get; set; }
        public bool MF { get; set; }
        public TPchanges(bool MT, bool MF)
        {
            this.MT = MT;
            this.MF = MF;
        }
    }

    public static class Kinematics
    {
        // Collection of Inverse Kinematics 
        static public string IK_TwoAxis(ToolPoint TP, MaterialTool MT)
        {
            Point3d OP = TP.Pt;
            string GPoint = "";
            GPoint += "X" + OP.X.ToString("0.000") + " Y" + OP.Y.ToString("0.000");

            return GPoint;
        }
        static public string IK_ThreeAxis(ToolPoint TP, MaterialTool MT)
        {
            Point3d OP = TP.Pt;
            string GPoint = "";
            GPoint += "X" + OP.X.ToString("0.000") + " Y" + OP.Y.ToString("0.000") + " Z" + OP.Z.ToString("0.000");

            return GPoint;
        }
        static public string IK_FiveAxisBC(ToolPoint TP, MaterialTool MT)
        {
            Point3d Point = TP.Pt;
            Vector3d UV = TP.Dir;
            double Tooltip = MT.toolLength;
            double Bo = Math.Acos(UV.Z);
            double Co = Math.Atan2(UV.Y, UV.X);

            if (Co > Math.PI) Co = Co - 2 * Math.PI;

            Vector3d Offset = new Vector3d();

            Offset = UV * Tooltip;

            Point3d OP = Point + Offset;

            String GPoint = "";
            GPoint += "X" + OP.X.ToString("0.000") + " Y" + OP.Y.ToString("0.000") + " Z" + OP.Z.ToString("0.000") + " ";
            GPoint += "B" + (180 * Bo / Math.PI).ToString("0.000") + " C" + (180 * Co / Math.PI).ToString("0.000");

            return GPoint;
        }
        static public string IK_FiveAxisABTable(ToolPoint TP, MaterialTool MT, Vector3d Pivot, Vector3d AB, ref Point3d machinePt)
        {
            Point3d OP = TP.Pt;

            // rotate from material orientation to machine orientation
            OP.Transform(Transform.Rotation(AB.Y, Vector3d.YAxis, Point3d.Origin));
            OP.Transform(Transform.Rotation(AB.X, Vector3d.XAxis, Point3d.Origin));

            // translate from origin at pivot to origin set so that the tooltip is at machine origin
            OP = OP - Pivot + Vector3d.ZAxis * MT.toolLength;

            StringBuilder GPtBd = new StringBuilder(@"X", 34);
            GPtBd.Append(OP.X.ToString("0.000"));
            GPtBd.Append(@" Y"); GPtBd.Append(OP.Y.ToString("0.000"));
            GPtBd.Append(@" Z"); GPtBd.Append(OP.Z.ToString("0.000"));
            GPtBd.Append(@" A"); GPtBd.Append((180 * AB.X / Math.PI).ToString("0.000"));
            GPtBd.Append(@" B"); GPtBd.Append((180 * AB.Y / Math.PI).ToString("0.000"));

            machinePt = OP;
            return GPtBd.ToString();
        }
        static private Vector3d Orient_FiveAxisABP(Vector3d UV)
        {
            // Always gives B from -pi to pi and A from -pi/2 to pi/2.
            double Ao = Math.Asin(-UV.Y);
            double Bo = Math.Atan2(UV.X, -UV.Z);

            if (Ao > Math.PI / 2.0)
            {
                Ao = Math.PI - Ao;
                Bo = Bo - Math.PI;
                if (Bo < 0) Bo = Bo + 2.0 * Math.PI;
            }

            if (Ao < -Math.PI / 2.0)
            {
                Ao = Math.PI - Ao;
                Bo = Bo - Math.PI;
                if (Bo < 0) Bo = Bo + 2.0 * Math.PI;
            }

            return new Vector3d(Ao, Bo, 0);
        }
    }

    public static class Utility
    {
        private static Curve blendIn(Point3d fPt, Point3d bPt, Vector3d Dir, double sAng, double eAng)
        {
            if(sAng == eAng) // straight line
            {

            }
        }

        public static ToolPath LeadInOut2d(ToolPath TP, double lead)
        {
            double leadLen = lead * TP.Additions.leadFactor;
            ToolPath newTP = new ToolPath(TP);
            PolylineCurve toolL = TP.GetLine();

            // Find the point on a circle furthest from the toolpath. 
            int testNumber = 50;
            Point3d LeadStart = new Point3d(), testPt;
            double testdist, dist = -1;
            bool noInter, correctSide;
            for (int i = 0; i < testNumber; i++)
            {
                double ang = 2.0 * Math.PI * i / (double)testNumber;
                testPt = TP[0].Pt + leadLen * new Point3d(Math.Cos(ang), Math.Sin(ang), 0);

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
                    noInter = Intersection.CurveCurve(toolL, new Line(TP[0].Pt, testPt).ToNurbsCurve(), 0.00001, 0.00001).Count <= 1;

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
                newTP[0].AddError("No suitable point for lead in and out found.");
            }
            else
            {
                ToolPoint LeadTP = new ToolPoint(TP[0]);
                LeadTP.Pt = LeadStart;
                newTP.Add(new ToolPoint(LeadTP));
                newTP.Insert(0, LeadTP);
            }

            newTP.Additions.leadFactor = 0;
            return newTP;
        }
    }

    public static class GCode
    { 
        // Formatting structure for GCode

        static public void GcInstStart(IGCodeMachine M, ref CodeInfo Co, MachineInstruction MI)
        {
            DateTime thisDay = DateTime.Now;
            Co.AppendLineNoNum(M.fileStart);
            Co.AppendComment(M.sectionBreak);
            if (MI.name != "") Co.AppendComment(MI.name);
            Co.AppendComment("");
            Co.AppendComment(" Machine Instructions Created " + thisDay.ToString("f"));
            Co.AppendComment("  by " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " "
                + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            if (M.name != "") Co.AppendComment("  for " + M.name);
            Co.AppendComment(" Starting with: ");
            Co.AppendComment("  Tool: " + MI[0][0].MatTool.Tool_name);
            Co.AppendComment("  in " + MI[0][0].MatTool.Mat_name + " with shape " + MI[0][0].MatForm.ToString());
            Co.AppendComment("");
            Co.AppendComment(M.sectionBreak);
            Co.Append(M.header);
            Co.Append(MI.preCode);
        }
        static public void GcInstEnd(IGCodeMachine M, ref CodeInfo Co, MachineInstruction MI)
        {
            Co.AppendComment(M.sectionBreak);
            Co.AppendComment(" End of ToolPaths");
            Co.AppendComment(M.sectionBreak);
            Co.Append(MI.postCode);
            Co.Append(M.footer);
            Co.AppendLineNoNum(M.fileEnd);
        }

        static public void GcOpStart(IGCodeMachine M, ref CodeInfo Co, MachineOperation MO)
        {
            Co.AppendComment(M.sectionBreak);
            Co.AppendComment("");
            Co.AppendComment(" Operation: " + MO.name);
            Co.AppendComment("");
            Co.Append(MO.preCode);
        }
        static public void GcOpEnd(IGCodeMachine M, ref CodeInfo Co, MachineOperation MO)
        {
            Co.AppendComment(MO.postCode);
        }

        static public TPchanges GcPathStart(IGCodeMachine M, ref CodeInfo Co, ToolPath TP)
        {
            TPchanges ch = new TPchanges(false, false);
            Co.AppendComment(M.sectionBreak);
            bool preamble = false;
            if (TP.name != "")
            {
                Co.AppendComment(" ToolPath: " + TP.name);
                preamble = true;
            }
            if (Co.currentMT == null || TP.MatTool.Tool_name != Co.currentMT.Tool_name)
            {
                Co.AppendComment(" using: " + TP.MatTool.Tool_name + " into " + TP.MatTool.Mat_name);
                Co.currentMT = TP.MatTool;
                ch.MT = true;
                preamble = true;
            }
            if (Co.currentMF == null || TP.MatForm.ToString() != Co.currentMF.ToString())
            {
                Co.AppendComment(" material: " + TP.MatForm.ToString());
                Co.currentMF = TP.MatForm;
                ch.MF = true;
                preamble = true;
            }

            if (preamble) { Co.AppendComment(M.sectionBreak); }

            Co.Append(TP.preCode);
            return ch;
        }
        static public void GcPathEnd(IGCodeMachine M, ref CodeInfo Co, ToolPath TP)
        {
            Co.Append(TP.postCode);
        }

        // GCode reading
        static private Regex numbPattern = new Regex(@"^([0-9\-.]+).*", RegexOptions.Compiled);
        static private double GetValue(string line, char split, double old, ref bool changed)
        {
            double val = old;
            string[] splitLine = line.Split(split);
            if (splitLine.Length > 1 && numbPattern.IsMatch(splitLine[1]))
            {
                string monkey = numbPattern.Replace(splitLine[1], "$1");
                val = Convert.ToDouble(monkey);
                if (val != old) changed = true;
            }
            return val;
        }

        static public ToolPath Read(IGCodeMachine M, string Code, List<char> terms)
        {
            ToolPath TP = new ToolPath();
            Dictionary<char, double> vals = new Dictionary<char, double>();
            foreach(char c in terms) { vals.Add(c, 0); }

            char[] seps = { '\n', '\r' };
            String[] Lines = Code.Split(seps, StringSplitOptions.RemoveEmptyEntries);
            bool changed;

            foreach (String line in Lines)
            {
                changed = false;
                foreach (char t in terms)
                { vals[t] = GetValue(line, t, vals[t], ref changed); }
                //interpret a G0 command.
                if (line.Contains(@"G00") || line.ToString().Contains(@"G0 "))
                {
                    if (vals.ContainsKey('F') && vals['F'] != 0)
                    {
                        changed = true;
                        vals['F'] = 0;
                    }
                }
                if (changed){TP.Add(M.readTP(vals));}
            }
            return TP;
        }
    }
}