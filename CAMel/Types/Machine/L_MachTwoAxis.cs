﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.Types.Machine
{
    public class TwoAxisFactory
    {
        [NotNull] public string name { get; set; }
        public double pathJump { get; set; }
        [NotNull] public string sectionBreak { get; set; }
        [NotNull] public string speedChangeCommand { get; set; }
        [NotNull] public string toolChangeCommand { get; set; }
        [NotNull] public string fileStart { get; set; }
        [NotNull] public string fileEnd { get; set; }
        [NotNull] public string header { get; set; }
        [NotNull] public string footer { get; set; }
        [NotNull] public string commentStart { get; set; }
        [NotNull] public string commentEnd { get; set; }
        [NotNull] public List<MaterialTool> mTs { get; set; }
        [NotNull] public string insert { get; set; }
        [NotNull] public string retract { get; set; }

        public TwoAxisFactory()
        {
            this.name = string.Empty;
            this.header = string.Empty;
            this.footer = string.Empty;
            this.pathJump = 100;
            this.insert = GCode.DefaultInsertCommand;
            this.retract = GCode.DefaultRetractCommand;
            this.commentStart = GCode.DefaultCommentStart;
            this.commentEnd = GCode.DefaultCommentEnd;
            this.sectionBreak = "---------------------------------";
            this.fileStart = GCode.DefaultFileStart;
            this.fileEnd = GCode.DefaultFileEnd;
            this.speedChangeCommand = GCode.DefaultSpeedChangeCommand;
            this.toolChangeCommand = GCode.DefaultToolChangeCommand;
            this.mTs = new List<MaterialTool>();
        }

    }

    public class TwoAxis : IGCodeMachine
    {
        public string name { get; }
        public double pathJump { get;}
        public bool toolLengthCompensation { get; }
        public string sectionBreak { get; }
        public string speedChangeCommand { get; }
        public string toolChangeCommand { get; }
        public string fileStart { get; }
        public string fileEnd { get; }
        public string header { get; }
        public string footer { get; }
        public string commentStart { get; }
        public string commentEnd { get; }
        [NotNull] private readonly List<char> _terms;
        public List<MaterialTool> mTs { get; }
        [NotNull] private string insert { get; }
        [NotNull] private string retract { get; }

        [NotNull] public ToolPathAdditions defaultTPA => ToolPathAdditions.twoAxisDefault;

        public TwoAxis([NotNull] TwoAxisFactory ta)
        {
            this.name = ta.name;
            this.header = ta.header;
            this.footer = ta.footer;
            this.pathJump = ta.pathJump;
            this.toolLengthCompensation = false;
            this.speedChangeCommand = ta.speedChangeCommand;
            this.insert = ta.insert;
            this.retract = ta.retract;
            this.commentStart = ta.commentStart;
            this.commentEnd = ta.commentEnd;
            this.sectionBreak = ta.sectionBreak;
            this.fileStart = ta.fileStart;
            this.fileEnd = ta.fileEnd;
            this.toolChangeCommand = ta.toolChangeCommand;
            this.mTs =ta.mTs;
            this._terms = new List<char> { 'X', 'Y', 'S', 'F' };
        }

        [PublicAPI] private TwoAxis([NotNull] TwoAxis ta)
        {
            this.name = ta.name;
            this.header = ta.header;
            this.footer = ta.footer;
            this.pathJump = ta.pathJump;
            this.toolLengthCompensation = ta.toolLengthCompensation;
            this.speedChangeCommand = ta.speedChangeCommand;
            this.insert = ta.insert;
            this.retract = ta.retract;
            this.commentStart = ta.commentStart;
            this.commentEnd = ta.commentEnd;
            this.sectionBreak = ta.sectionBreak;
            this.fileStart = ta.fileStart;
            this.fileEnd = ta.fileEnd;
            this.toolChangeCommand = ta.toolChangeCommand;
            this.mTs = ta.mTs;
            this._terms = new List<char> { 'X', 'Y', 'S', 'F' };
        }

        public string TypeDescription => @"Instructions for a 2-Axis machine";
        public string TypeName => @"CAMelTwoAxis";

        public override string ToString() => "2Axis: " + this.name;

        public string comment(string l) => GCode.comment(this, l);

        public ToolPath insertRetract(ToolPath tP) => Utility.leadInOut2D(tP, this.insert, this.retract);
        public List<List<ToolPath>> stepDown(ToolPath tP) => new List<List<ToolPath>>();
        public ToolPath threeAxisHeightOffset(ToolPath tP) => Utility.clearThreeAxisHeightOffset(tP);
        public List<ToolPath> finishPaths(ToolPath tP) => Utility.oneFinishPath(tP);

        public ToolPoint interpolate(ToolPoint fP, ToolPoint tP, MaterialTool mT, double par, bool lng)
        => Kinematics.interpolateLinear(fP, tP, par);
        public double angDiff(ToolPoint tP1, ToolPoint tP2, MaterialTool mT, bool lng) => 0;

        public ToolPath readCode(string code) => GCode.gcRead(this,this.mTs,code, this._terms);
        public ToolPoint readTP(Dictionary<char, double> values, MaterialTool mT) => new ToolPoint(new Point3d(values['X'], values['Y'],0), new Vector3d(0, 0, 0), values['S'], values['F']);

        public Vector3d toolDir(ToolPoint tP) => Vector3d.ZAxis;

        public void writeCode(ref CodeInfo co, [ItemNotNull] ToolPath tP)
        {
            if (tP.matTool == null) { Exceptions.matToolException(); }
            // Double check tP does not have additions.
            if (tP.additions != null && tP.additions.any) { Exceptions.additionsException(); }

            if (tP.Count <= 0) { return; }

            GCode.gcPathStart(this, ref co, tP);

            bool fChange = false;
            bool sChange = false;

            double feed = co.machineState["F"];
            double speed = co.machineState["S"];
            if (feed < 0) { feed = tP.matTool.feedCut; fChange = true; }
            if (speed < 0) { speed = tP.matTool.speed; sChange = true; }

            foreach (ToolPoint pt in tP)
            {
                foreach (string err in pt.error)
                {
                    co.addError(err);
                    co.appendComment(err);
                }
                foreach (string warn in pt.warning)
                {
                    co.addWarning(warn);
                    co.appendComment(warn);
                }

                // Establish new feed value
                if (Math.Abs(pt.feed - feed) > CAMel_Goo.Tolerance)
                {
                    if (pt.feed >= 0)
                    {
                        fChange = true;
                        feed = pt.feed;
                    }
                    else if (Math.Abs(feed - tP.matTool.feedCut) > CAMel_Goo.Tolerance) // Default to the cut feed rate.
                    {
                        fChange = true;
                        feed = tP.matTool.feedCut;
                    }
                }

                // Establish new speed value
                if (Math.Abs(pt.speed - speed) > CAMel_Goo.Tolerance)
                {
                    if (pt.speed > 0)
                    {
                        sChange = true;
                        speed = pt.speed;
                    }
                }

                // Add the position information
                string ptCode = GCode.gcTwoAxis(pt);

                // Act if feed has changed
                if (fChange && feed >= 0)
                {
                    if (Math.Abs(feed) < CAMel_Goo.Tolerance) { ptCode = "G00 " + ptCode; }
                    else { ptCode = "G01 " + ptCode + " F" + feed.ToString("0"); }
                }
                fChange = false;

                // Act if speed has changed
                if (sChange && speed >= 0)
                { ptCode = this.speedChangeCommand + " S" + speed.ToString("0") + "\n" + ptCode; }
                sChange = false;

                if (pt.name != string.Empty) { ptCode = ptCode + " " + comment(pt.name); }

                ptCode = pt.preCode + ptCode + pt.postCode;

                co.append(ptCode);

                // Adjust ranges

                co.growRange("X", pt.pt.X);
                co.growRange("Y", pt.pt.Y);
            }
            // Pass machine state information

            if (tP.lastP == null) { Exceptions.nullPanic(); }
            co.machineState.Clear();
            co.machineState.Add("X", tP.lastP.pt.X);
            co.machineState.Add("Y", tP.lastP.pt.Y);
            co.machineState.Add("F", feed);
            co.machineState.Add("S", speed);

            GCode.gcPathEnd(this, ref co, tP);
        }

        public void writeFileStart(ref CodeInfo co, MachineInstruction mI, ToolPath startPath)
        {
            // Set up Machine State

            if (startPath.firstP == null) { Exceptions.nullPanic(); }
            co.machineState.Clear();
            co.machineState.Add("X", startPath.firstP.pt.X);
            co.machineState.Add("Y", startPath.firstP.pt.Y);
            co.machineState.Add("F", -1);
            co.machineState.Add("S", -1);

            GCode.gcInstStart(this, ref co, mI, startPath);
        }
        public void writeFileEnd(ref CodeInfo co, MachineInstruction mI, ToolPath finalPath, ToolPath endPath) => GCode.gcInstEnd(this, ref co, mI, finalPath, endPath);
        public void writeOpEnd(ref CodeInfo co, MachineOperation mO) => GCode.gcOpEnd(this, ref co, mO);
        public void writeOpStart(ref CodeInfo co, MachineOperation mO) => GCode.gcOpStart(this, ref co, mO);

        public void writeTransition(ref CodeInfo co, ToolPath fP, ToolPath tP, bool first)
        {
            // check there is anything to transition from
            if (fP.Count <= 0 || tP.Count <= 0) { return; }

            if (fP.lastP == null || tP.firstP == null) { Exceptions.nullPanic(); }
            List<Point3d> route = new List<Point3d> { fP.lastP.pt, tP.firstP.pt };

            ToolPath move = tP.deepCloneWithNewPoints(new List<ToolPoint>());
            move.name = string.Empty;
            move.preCode = string.Empty;
            move.postCode = string.Empty;
            foreach (Point3d pt in route)
            {
                // add new point at speed 0 to describe rapid move.
                move.Add(new ToolPoint(pt, new Vector3d(), -1, 0));
            }
            writeCode(ref co, move);
        }
    }
}