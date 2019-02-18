using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.GUI.Base;
using Rhino.Geometry;

using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Emgu.CV.XImgproc;
using System.Windows.Forms;

namespace CAMel
{
    public class C_PhotoContours : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateToolPath class.
        /// </summary>
        public C_PhotoContours()
            : base("Photo Contours", "PhotoC",
                "Trace the contours in a photo",
                "CAMel", " Photos")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File", "F", "Name of image file", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "Traced Curves", GH_ParamAccess.list);
        }

        int Prune = 3, Jump = 15, Blur = 0;
        private bool m_debug = false;
        public bool debug
        {
            get { return m_debug; }
            set
            {
                m_debug = value;
                if ((m_debug)) { Message = "Showing work..."; }
                else { Message = string.Empty; }
            }
        }
        List<string> times;

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string filename = string.Empty;

            if (!DA.GetData(0, ref filename)) return;
            string filepath = System.IO.Path.GetDirectoryName(filename);

            List<Curve> curves = new List<Curve>();
            times = new List<string>();
            Stopwatch watch = Stopwatch.StartNew();

            Bitmap BM = (Bitmap)Image.FromFile(filename);
            Image<Gray, Byte> img = new Image<Gray, Byte>(BM);

            CvInvoke.GaussianBlur(img, img, new Size(2 * Blur + 1, 2 * Blur + 1), 0, 0);

            if (debug)
            {
                watch.Stop();
                times.Add("Open File: " + watch.ElapsedMilliseconds + " ms");
                watch = Stopwatch.StartNew();
            }
           

            // Find the outer contour, this will fill in any holes in the path
            // It does assume that no paths are loops. 
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(img, contours, null, RetrType.List, ChainApproxMethod.ChainApproxTc89Kcos);
            
            ClipperLib.Clipper.

            for (int i = 0; i < contours.Size; i++)
            {
                VectorOfPoint sco = new VectorOfPoint();
                if (contours[i].Size > 4)
                {
                    cont = new VectorOfPoint();
                    int j = 1;
                    while (contours[i][j - 1] != contours[i][j + 1] && j < contours[i].Size - 1) { j++; }
                    System.Drawing.Point[] pt = new System.Drawing.Point[2] { contours[i][j], contours[i][j + 1] };
                    cont.Push(pt);
                    j += 2;
                    while (j < contours[i].Size && contours[i][j - 2] != contours[i][j])
                    {
                        pt = new System.Drawing.Point[1] { contours[i][j] };
                        cont.Push(pt);
                        j++;
                    }
                    CvInvoke.ApproxPolyDP(cont, sco, 2, false);
                    //sco = cont;
                    List<Point3d> c = new List<Point3d>();
                    for (j = 0; j < sco.Size; j++) { c.Add(Pt2R(sco[j])); }
                    curves.Add(new PolylineCurve(c));
                }
            }

            if (debug)
            {
                watch.Stop();
                times.Add("Find contours 2: " + watch.ElapsedMilliseconds + " ms");
                watch = Stopwatch.StartNew();
            }

            // In Rhino we join the remaining curves, healing the triple points we removed
            // Hopefully ending up with something close to the intended result. 
            // I would be curious to see what happens with trees!

            curves.Sort(delegate (Curve x, Curve y)
            {
                return y.GetLength().CompareTo(x.GetLength());
            });

            List<Curve> Jcurves = new List<Curve>();
            List<Curve> Tcurves = new List<Curve>();

            Tcurves.Add(curves[0]);
            Jcurves = Tcurves;
            for (int i = 1; i < curves.Count; i++)
            {
                Tcurves.Add(curves[i]);
                Jcurves = new List<Curve>();
                Jcurves.AddRange(Curve.JoinCurves(Tcurves, 10, false));
                Tcurves = Jcurves;
            }

            Tcurves = Jcurves;
            Jcurves = new List<Curve>();

            BoundingBox BB = new BoundingBox();
            for (int i = 0; i < Tcurves.Count; i++)
            {
                if (Tcurves[i].GetLength() > Jump * 4)
                {
                    Jcurves.Add(Tcurves[i]);
                    BB.Union(Tcurves[i].GetBoundingBox(false));
                }
            }
            Tcurves = Jcurves;
            Jcurves = new List<Curve>();
            Jcurves.AddRange(Curve.JoinCurves(Tcurves, Jump + 2 * Prune, false));

            for (int i = 0; i < Jcurves.Count; i++)
            {
                Jcurves[i].Translate(-(Vector3d)BB.Center);
            }

            if (debug)
            {
                watch.Stop();
                times.Add("Join Curves: " + watch.ElapsedMilliseconds + " ms");
            }


            watch.Stop();

            DA.SetDataList(0, Jcurves);
        }

        Point3d Pt2R(System.Drawing.Point p)
        {
            return new Point3d(p.X, -p.Y, 0);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            Menu_AppendItem(menu, "Tracing Settings");

            Menu_AppendNumber(menu, "Jump", this.Jump, "Distance to connect edges (in pixels)");
            Menu_AppendNumber(menu, "Blur", this.Blur, "Radius of blur (in pixels)");
            Menu_AppendNumber(menu, "Prune", this.Prune, "Pixels to prune of ends of every branch");

            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Debug", Debug_Clicked, true, debug);
            if (debug)
            {
                for (int i = 0; i < times.Count; i++)
                {
                    Menu_AppendItem(menu, times[i]);
                }
                Menu_AppendItem(menu, "Copy Data", CopyData_Clicked);
            }
        }

        private NumericUpDown Menu_AppendNumber(ToolStripDropDown menu, string name, int val, string desc)
        {
            Panel MI = new FlowLayoutPanel();
            MI.AutoSize = true;
            MI.Text = name;
            MI.AutoSize = true;
            MI.BackColor = Color.White;

            NumericUpDown uD = new NumericUpDown();
            uD.Value = val;
            uD.Name = name;
            uD.ValueChanged += Trace_Settings;
            uD.Width = 50;

            Label L = new Label();
            L.Text = name;

            MI.Controls.Add(L);
            MI.Controls.Add(uD);

            MI.Height = uD.Height + 6;

            ToolStripItem tSI = new ToolStripControlHost(MI);
            tSI.ToolTipText = desc;

            menu.Items.Add(tSI);

            return uD;
        }
        private void CopyData_Clicked(object sender, EventArgs e)
        {
            System.Text.StringBuilder TraceData = new System.Text.StringBuilder();

            for (int i = 0; i < times.Count; i++) { TraceData.AppendLine(times[i]); }

            Clipboard.SetText(TraceData.ToString());
        }
        private void Debug_Clicked(object sender, EventArgs e)
        {
            RecordUndoEvent("Trace_Debug");
            debug = !debug;
            ExpireSolution(true);
        }
        private void Trace_Settings(object sender, EventArgs e)
        {
            NumericUpDown UD = (NumericUpDown)sender;
            RecordUndoEvent(UD.Name);
            switch (UD.Name)
            {
                case "Blur":
                    Blur = (int)UD.Value;
                    break;
                case "Jump":
                    Jump = (int)UD.Value;
                    break;
                case "Prune":
                    Prune = (int)UD.Value;
                    break;
                default:
                    break;
            }

        }
        private void Trace_Settings(object sender, string text)
        {
            ToolStripTextBox TB = (ToolStripTextBox)sender;
            RecordUndoEvent(TB.Name);
            switch (TB.Name)
            {
                case "Blur":
                    Blur = Convert.ToInt32(text);
                    break;
                case "Jump":
                    Jump = Convert.ToInt32(text);
                    break;
                case "Prune":
                    Prune = Convert.ToInt32(text);
                    break;
                default:
                    break;
            }

        }

        // Need to save and recover the trace settings
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            // First add our own field.
            writer.SetInt32("Jump", this.Jump);
            writer.SetInt32("Prune", this.Prune);
            writer.SetInt32("Blur", this.Blur);
            writer.SetBoolean("Debug", this.debug);
            // Then call the base class implementation.
            return base.Write(writer);
        }
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            // First read our own field.
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
                return Properties.Resources.photocontour;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{043136B0-C887-465A-BC1E-6B1BBCCE5137}"); }
        }
    }
}