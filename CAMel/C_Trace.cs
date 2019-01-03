using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.GUI.Base;
using Grasshopper.Kernel.Expressions;
using Rhino.Geometry;

using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Emgu.CV.XImgproc;
using System.Windows.Forms;

namespace CAMel
{
    public class C_Trace : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateToolPath class.
        /// </summary>
        public C_Trace()
            : base("Trace hand drawn path", "Trace",
                "Trace a path from a photo of a hand drawn image",
                "CAMel", " Photos")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File", "F", "Name of image file", GH_ParamAccess.item);
            pManager.AddTextParameter("Height Expression", "H", "Function to define height. Evaluates on x along each genrated path from 0 to 1.", GH_ParamAccess.item, "0");
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "Traced Curves", GH_ParamAccess.list);
            pManager.AddCurveParameter("Polyline", "P", "Polyline of points along traced curve, for more creative processing.", GH_ParamAccess.list);
        }

        int Prune = 3, Jump = 15, Blur = 0, MaxFile = 3;
        private bool m_debug = false;
        public bool debug
        {
            get { return this.m_debug; }
            set
            {
                this.m_debug = value;
                if ((this.m_debug)) { this.Message = "Showing work..."; }
                else { this.Message = string.Empty; }
            }
        }
        List<string> times;

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch watch = Stopwatch.StartNew();
            string filename = string.Empty;

            if (!DA.GetData(0, ref filename)) { return; }
           
            string filepath= System.IO.Path.GetDirectoryName(filename);

            string exp = string.Empty;
            
            if (!DA.GetData(1, ref exp)) { return; }
            GH_ExpressionParser EP = new GH_ExpressionParser(true);

            exp = GH_ExpressionSyntaxWriter.RewriteForEvaluator(exp);
            EP.CacheSymbols(exp);

            List<Curve> curves = new List<Curve>();
            this.times = new List<string>();

            Mat imgMat = new Mat(filename);

            CvInvoke.CvtColor(imgMat, imgMat,ColorConversion.Bgr2Gray);

            CvInvoke.GaussianBlur(imgMat, imgMat, new Size(2* this.Blur +1,2* this.Blur +1), 0, 0);

            if (this.debug)
            {
                watch.Stop();
                this.times.Add("Open File: " + watch.ElapsedMilliseconds + " ms");
                watch = Stopwatch.StartNew();
            }

            Mat Thresh = new Mat();
            CvInvoke.AdaptiveThreshold(imgMat, Thresh, 255, AdaptiveThresholdType.GaussianC, ThresholdType.Binary, 51, 15);
            imgMat.Dispose();
            
            CvInvoke.BitwiseNot(Thresh,Thresh);

            if (this.debug)
            {
                watch.Stop();
                this.times.Add("Threshold: " + watch.ElapsedMilliseconds + " ms");
                Thresh.Save(System.IO.Path.Combine(filepath,"CAMelTrace_Thresholded.png"));
                watch = Stopwatch.StartNew();
            }

            // Find the outer contour, this will fill in any holes in the path
            // It does assume that no paths are loops. 
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(Thresh, contours, null, RetrType.External, ChainApproxMethod.ChainApproxNone);

            VectorOfVectorOfPoint usecontours = new VectorOfVectorOfPoint();
            for (int i = 0; i < contours.Size; i++)
            {
                if (contours[i].Size > 10)
                {
                    usecontours.Push(contours[i]);
                }
            }

            Mat ContourBM = new Mat(Thresh.Size, Thresh.Depth, 1);
            ContourBM.SetTo(new MCvScalar(0));
            Thresh.Dispose();

            CvInvoke.DrawContours(ContourBM, usecontours, -1, new MCvScalar(255), -1);

            if (this.debug)
            {
                watch.Stop();
                this.times.Add("Redraw Main paths: " + watch.ElapsedMilliseconds + " ms");
                ContourBM.Save(System.IO.Path.Combine(filepath,"CAMelTrace_Redrawn.png"));
                watch = Stopwatch.StartNew();
            }

            // Thin the region to get the center of the curve, with some small branches

            Mat thin = new Mat();
            XImgprocInvoke.Thinning(ContourBM, thin, ThinningTypes.GuoHall);
            ContourBM.Dispose();

            if (this.debug)
            {
                watch.Stop();
                this.times.Add("Thin: " + watch.ElapsedMilliseconds + " ms");
                thin.Save(System.IO.Path.Combine(filepath, "CAMelTrace_Thinned.png"));
                watch = Stopwatch.StartNew();
            }

            // Thinning leaves a branching 1 pixel curve
            // Prune the branches a little

            ElementShape sh = ElementShape.Rectangle;
            System.Drawing.Point an = new System.Drawing.Point(-1, -1);
            Mat element = CvInvoke.GetStructuringElement(sh, new Size(3, 3), an);

            Mat thrpts = new Mat(thin.Size,DepthType.Cv8U,1);
            Mat thinf = new Mat();

            for (int i = 0; i < this.Prune; i++)
            {
                CvInvoke.Threshold(thin, thinf, 128, 64, ThresholdType.Binary);
                CvInvoke.Filter2D(thinf, thrpts, element, an);
                CvInvoke.Threshold(thrpts, thrpts, 129, 255, ThresholdType.Binary);
                CvInvoke.BitwiseAnd(thin, thrpts, thin);
            }

            if (this.debug)
            {
                watch.Stop();
                this.times.Add("Prune: " + watch.ElapsedMilliseconds + " ms");
                thin.Save(System.IO.Path.Combine(filepath, "CAMelTrace_Pruned.png"));
                watch = Stopwatch.StartNew();
            }

            // Now remove triple points to cut off branches


            CvInvoke.Threshold(thin, thinf, 128, 64, ThresholdType.Binary);
            CvInvoke.Filter2D(thinf, thrpts, element, an);
            CvInvoke.Threshold(thrpts, thrpts, 200, 255, ThresholdType.Binary);
            CvInvoke.BitwiseNot(thrpts, thrpts);
            CvInvoke.BitwiseAnd(thin, thrpts, thin);

            if (this.debug)
            {
                watch.Stop();
                this.times.Add("Remove 3 points: " + watch.ElapsedMilliseconds + " ms");
                thin.Save(System.IO.Path.Combine(filepath,"CAMelTrace_JustLines.png"));
                watch = Stopwatch.StartNew();
            }

            // Now we have a collection of 1 pixel thick curves, we can vectorize

            contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(thin, contours, null, RetrType.External, ChainApproxMethod.ChainApproxNone);
            VectorOfPoint cont;
            
            // The contours are loops and there are small paths we want to ignore.
            // We run ApproxPolyDP to simplify the curve
            // Finally we convert to a Rhino curve

            for (int i = 0; i < contours.Size; i++)
            {
                VectorOfPoint sco = new VectorOfPoint();
                if (contours[i].Size > 4)
                {
                    cont = new VectorOfPoint();
                    int j = 1;
                    while(contours[i][j-1] != contours[i][j+1] && j < contours[i].Size-1) { j++; }
                    System.Drawing.Point[] pt = new System.Drawing.Point[2] { contours[i][j], contours[i][j+1] };
                    cont.Push(pt);
                    j+=2;
                    while (j < contours[i].Size && contours[i][j - 2] != contours[i][j])
                    {
                        pt = new System.Drawing.Point[1] { contours[i][j] };
                        cont.Push(pt);
                        j++;
                    }
                    CvInvoke.ApproxPolyDP(cont, sco, 1, false);
                    //sco = cont;
                    List<Point3d> c = new List<Point3d>();
                    for (j = 0; j < sco.Size; j++) { c.Add(pt2R(sco[j])); }
                    curves.Add(new PolylineCurve(c));
                }
            }

            if (this.debug)
            {
                watch.Stop();
                this.times.Add("Find contours 2: " + watch.ElapsedMilliseconds + " ms");
                watch = Stopwatch.StartNew();
            }

            // In Rhino we join the remaining curves, healing the triple points we removed
            // Hopefully ending up with something close to the intended result. 
            // This should be replaced with a heuristic that aims to 
            // create the longest curves possible. 

            curves.Sort(delegate (Curve x, Curve y)
            {
                return y.GetLength().CompareTo(x.GetLength());
            });

            List<Curve> Jcurves = new List<Curve>();
            List<Curve> Tcurves = new List<Curve>();

            if (curves.Count > 0) { Tcurves.Add(curves[0]); }
            Jcurves = Tcurves;
            for(int i=1; i<curves.Count;i++)
            {
                Tcurves.Add(curves[i]);
                Jcurves = new List<Curve>();
                Jcurves.AddRange(Curve.JoinCurves(Tcurves, 10, false));
                Tcurves = Jcurves;
            }

            // Find centre at 0 and do final join.

            Tcurves = Jcurves;
            Jcurves = new List<Curve>();

            BoundingBox BB = new BoundingBox();
            for (int i=0;i<Tcurves.Count;i++)
            {
                if(Tcurves[i].GetLength()> this.Jump *4)
                {
                    Jcurves.Add(Tcurves[i]);
                    BB.Union(Tcurves[i].GetBoundingBox(false));
                }
            }
            Tcurves = Jcurves;
            Jcurves = new List<Curve>();
            Jcurves.AddRange(Curve.JoinCurves(Tcurves, this.Jump + 2* this.Prune, false));

            // Give height and move to centre.
            for (int i = 0; i < Jcurves.Count; i++)
            {
                double cp = 0;
                double cl = Jcurves[i].GetLength();
                Polyline pl;
                Jcurves[i].TryGetPolyline(out pl);
                for(int j=0; j<pl.Count; j++)
                {
                    if (j > 0) { cp = cp + (pl[j]-pl[j-1]).Length/cl; }
                    EP.AddVariable("x", cp);
                    double h = EP.Evaluate()._Double;
                    pl[j] = new Point3d(pl[j].X, pl[j].Y, h);
                }
                Jcurves[i] = new PolylineCurve(pl);
                Jcurves[i].Translate(-(Vector3d)BB.Center);
            }

            // Turn polylines into nice smooth curves
            Tcurves = new List<Curve>();
            for (int i = 0; i < Jcurves.Count; i++)
            {
                Polyline pl;
                List<Point3d> op = new List<Point3d>();
                Jcurves[i].TryGetPolyline(out pl);
                for (int j = 1; j < pl.Count; j++)
                {
                    if ((pl[j - 1] - pl[j]).Length > 5)
                    {
                        op.Add((0.5 * pl[j - 1] + 0.5 * pl[j]));
                    }
                    if (j < pl.Count - 1)
                    {
                        double ang = Vector3d.VectorAngle(pl[j] - pl[j - 1], pl[j + 1] - pl[j]);
                        if (ang > Math.PI / 2.1) { op.Add(pl[j]);  }
                    }
                    op.Add(pl[j]);
                }
                Tcurves.Add(Curve.CreateControlPointCurve(op));
            }
            
            if (this.debug)
            {
                watch.Stop();
                this.times.Add("Join Curves: " + watch.ElapsedMilliseconds + " ms");
            }
            watch.Stop();

            DA.SetDataList(0, Tcurves);
            DA.SetDataList(1, Jcurves);
        }

        Point3d pt2R(System.Drawing.Point p)
        {
            return new Point3d(p.X, -p.Y, 0);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            //Menu_AppendNumber(menu, "Mega Pixels", this.MaxFile, "Largest file to process (in megapixels)");
            Menu_AppendSeparator(menu);

            Menu_AppendItem(menu, "Tracing Settings");
            //Menu_AppendNumber(menu, "Jump", this.Jump,"Distance to connect edges (in pixels)");
            //Menu_AppendNumber(menu, "Blur", this.Blur,"Radius of blur (in pixels)");
            //Menu_AppendNumber(menu, "Prune", this.Prune,"Pixels to prune of ends of every branch");

            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Debug", debugClicked, true, this.debug);
            if (this.debug)
            {
                for (int i = 0; i < this.times.Count; i++)
                {
                    Menu_AppendItem(menu, this.times[i]);
                }
                Menu_AppendItem(menu, "Copy Data", copyDataClicked);
            }
        }

        private NumericUpDown menuAppendNumber(ToolStripDropDown menu, string name, int val, string desc)
        {
            Panel MI = new FlowLayoutPanel
            {
                Text = name,
                AutoSize = true,
                BackColor = Color.White
            };

            NumericUpDown uD = new NumericUpDown
            {
                Value = val,
                Name = name
            };
            uD.ValueChanged += traceSettings;
            uD.Width = 50;

            Label L = new Label
            {
                Text = name
            };

            MI.Controls.Add(L);
            MI.Controls.Add(uD);

            MI.Height = uD.Height+6;

            ToolStripItem tSI = new ToolStripControlHost(MI)
            {
                ToolTipText = desc
            };

            menu.Items.Add(tSI);

            return uD;
        }
        private void copyDataClicked(object sender, EventArgs e)
        {
            System.Text.StringBuilder TraceData = new System.Text.StringBuilder();

            for(int i=0;i< this.times.Count;i++) { TraceData.AppendLine(this.times[i]); }
            
            Clipboard.SetText(TraceData.ToString());
            
        }
        private void debugClicked(object sender, EventArgs e)
        {
            RecordUndoEvent("Trace_Debug");
            this.debug = !this.debug;
            ExpireSolution(true);
        }
        private void traceSettings(object sender, EventArgs e)
        {
            NumericUpDown UD = (NumericUpDown)sender;
            RecordUndoEvent(UD.Name);
            switch (UD.Name)
            {
                case "Blur":
                    this.Blur = (int)UD.Value;
                    break;
                case "Jump":
                    this.Jump = (int)UD.Value;
                    break;
                case "Prune":
                    this.Prune = (int)UD.Value;
                    break;
                case "Mega Pixels":
                    this.MaxFile = (int)UD.Value;
                    break;
                default:
                    break;
            }

        }

        // Need to save and recover the trace settings
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            // First add our own fields.
            writer.SetInt32("MaxFile", this.MaxFile);
            writer.SetInt32("Jump", this.Jump);
            writer.SetInt32("Prune", this.Prune);
            writer.SetInt32("Blur", this.Blur);
            writer.SetBoolean("Debug", this.debug);
            // Then call the base class implementation.
            return base.Write(writer);
        }
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            // First read our own fields.
            if (reader.ItemExists("MaxFile"))
            {
                this.MaxFile = reader.GetInt32("MaxFile");
            }
            if (reader.ItemExists("Jump"))
            {
                this.Jump = reader.GetInt32("Jump");
            }
            if (reader.ItemExists("Prune"))
            {
                this.Prune = reader.GetInt32("Prune");
            }
            if (reader.ItemExists("Blur"))
            {
                this.Blur = reader.GetInt32("Blur");
            }
            if (reader.ItemExists("Debug"))
            {
                this.debug = reader.GetBoolean("Debug");
            }
            // Then call the base class implementation.
            return base.Read(reader);
        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Properties.Resources.phototrace;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{7270759F-B5DA-46BB-B459-C98250ABB995}"); }
        }
    }
}