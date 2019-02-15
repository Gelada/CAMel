using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using Rhino.Geometry;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Expressions;
using Grasshopper.Kernel.Parameters;

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
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new Param_FilePath(), "File", "F", "Name of image file", GH_ParamAccess.item);
            pManager.AddTextParameter("Height Expression", "H", "Function to define height. Evaluates on x along each genrated path from 0 to 1.", GH_ParamAccess.item, "0");
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "Traced Curves", GH_ParamAccess.list);
            pManager.AddCurveParameter("Polyline", "P", "Polyline of points along traced curve, for more creative processing.", GH_ParamAccess.list);
        }

        private int _jump = 15, _blur, _maxFile = 3;
        private bool _debug;
        public bool debug
        {
            get { return this._debug; }
            set
            {
                this._debug = value;
                if ((this._debug)) { this.Message = "Showing work..."; }
                else { this.Message = string.Empty; }
            }
        }

        private List<string> _times;

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            string filename = string.Empty;

            if (!da.GetData(0, ref filename)) { return; }

            string exp = string.Empty;

            if (!da.GetData(1, ref exp)) { return; }
            GH_ExpressionParser eP = new GH_ExpressionParser(true);

            exp = GH_ExpressionSyntaxWriter.RewriteForEvaluator(exp);
            eP.CacheSymbols(exp);

            // Read photo into raw curves list
            List<Curve> jCurves = ReadPhoto.trace(filename, this._blur, this._jump, this.debug, out this._times);

            // Add height to paths using expression
            for (int i = 0; i < jCurves.Count; i++)
            {
                double cp = 0;
                double cl = jCurves[i].GetLength();
                Polyline pl;
                jCurves[i].TryGetPolyline(out pl);
                for(int j=0; j<pl.Count; j++)
                {
                    if (j > 0) { cp = cp + (pl[j]-pl[j-1]).Length/cl; }
                    eP.AddVariable("x", cp);
                    double h = eP.Evaluate()._Double;
                    pl[j] = new Point3d(pl[j].X, pl[j].Y, h);
                }
                jCurves[i] = new PolylineCurve(pl);
            }

            // Turn polylines into nice smooth curves
            var tCurves = new List<Curve>();
            for (int i = 0; i < jCurves.Count; i++)
            {
                Polyline pl;
                List<Point3d> op = new List<Point3d>();
                jCurves[i].TryGetPolyline(out pl);
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
                tCurves.Add(Curve.CreateControlPointCurve(op));
            }

            da.SetDataList(0, tCurves);
            da.SetDataList(1, jCurves);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            //Menu_AppendNumber(menu, "Mega Pixels", this.MaxFile, "Largest file to process (in megapixels)");
            Menu_AppendSeparator(menu);

            Menu_AppendItem(menu, "Tracing Settings");
            //Menu_AppendNumber(menu, "Jump", this.Jump,"Distance to connect edges (in pixels)");
            //Menu_AppendNumber(menu, "Blur", this.Blur,"Radius of blur (in pixels)");

            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Debug", debugClicked, true, this.debug);
            if (this.debug)
            {
                for (int i = 0; i < this._times.Count; i++)
                {
                    Menu_AppendItem(menu, this._times[i]);
                }
                Menu_AppendItem(menu, "Copy Data", copyDataClicked);
            }
        }

        // ReSharper disable once UnusedMember.Local
        private NumericUpDown menuAppendNumber(ToolStripDropDown menu, string name, int val, string desc)
        {
            Panel mI = new FlowLayoutPanel
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

            Label l = new Label
            {
                Text = name
            };

            mI.Controls.Add(l);
            mI.Controls.Add(uD);

            mI.Height = uD.Height+6;

            ToolStripItem tSi = new ToolStripControlHost(mI)
            {
                ToolTipText = desc
            };

            menu.Items.Add(tSi);

            return uD;
        }
        private void copyDataClicked(object sender, EventArgs e)
        {
            System.Text.StringBuilder traceData = new System.Text.StringBuilder();

            for(int i=0;i< this._times.Count;i++) { traceData.AppendLine(this._times[i]); }

            Clipboard.SetText(traceData.ToString());

        }
        private void debugClicked(object sender, EventArgs e)
        {
            RecordUndoEvent("Trace_Debug");
            this.debug = !this.debug;
            ExpireSolution(true);
        }
        private void traceSettings(object sender, EventArgs e)
        {
            NumericUpDown ud = (NumericUpDown)sender;
            RecordUndoEvent(ud.Name);
            switch (ud.Name)
            {
                case "Blur":
                    this._blur = (int)ud.Value;
                    break;
                case "Jump":
                    this._jump = (int)ud.Value;
                    break;
                case "Mega Pixels":
                    this._maxFile = (int)ud.Value;
                    break;
            }

        }

        // Need to save and recover the trace settings
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            // First add our own fields.
            writer.SetInt32("MaxFile", this._maxFile);
            writer.SetInt32("Jump", this._jump);
            writer.SetInt32("Blur", this._blur);
            writer.SetBoolean("Debug", this.debug);
            // Then call the base class implementation.
            return base.Write(writer);
        }
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            // First read our own fields.
            if (reader.ItemExists("MaxFile"))
            {
                this._maxFile = reader.GetInt32("MaxFile");
            }
            if (reader.ItemExists("Jump"))
            {
                this._jump = reader.GetInt32("Jump");
            }
            if (reader.ItemExists("Blur"))
            {
                this._blur = reader.GetInt32("Blur");
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
        protected override Bitmap Icon
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