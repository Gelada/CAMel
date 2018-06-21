using System;
using System.Collections.Generic;
using System.Text;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
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

    }
}