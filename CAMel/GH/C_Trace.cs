namespace CAMel.GH
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;

    using CAMel.Types;

    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Expressions;
    using Grasshopper.Kernel.Parameters;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    /// <inheritdoc />
    /// <summary>TODO The c_ trace.</summary>
    [UsedImplicitly]
    public class C_Trace : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the CreateToolPath class.
        /// </summary>
        public C_Trace()
            : base(
                "Trace hand drawn path", "Trace",
                "Trace a path from a photo of a hand drawn image",
                "CAMel", " Photos")
            => this.times = new List<string>();

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new Param_FilePath(), "File", "F", "Name of image file", GH_ParamAccess.item);
            pManager.AddTextParameter("Height Expression", "H", "Function to define height. Evaluates on x along each generated path from 0 to 1.", GH_ParamAccess.item, "0");
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddCurveParameter("Curves", "C", "Traced Curves", GH_ParamAccess.list);
            pManager.AddCurveParameter("Polyline", "P", "Polyline of points along traced curve, for more creative processing.", GH_ParamAccess.list);
        }

        /// <summary>TODO The jump.</summary>
        private int jump = 15;
        /// <summary>TODO The blur.</summary>
        private int blur;
        /// <summary>TODO The max file.</summary>
        private int maxFile = 3;
        /// <summary>TODO The debugging.</summary>
        private bool debugging;

        /// <summary>Gets or sets a value indicating whether debug.</summary>
        private bool debug
        {
            get => this.debugging;
            set
            {
                this.debugging = value;
                this.Message = this.debugging ? "Showing work..." : string.Empty;
            }
        }

        /// <summary>TODO The times.</summary>
        [NotNull] private List<string> times;

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }
            string filename = string.Empty;

            if (!da.GetData(0, ref filename)) { return; }

            string exp = string.Empty;

            if (!da.GetData(1, ref exp)) { return; }
            GH_ExpressionParser eP = new GH_ExpressionParser(true);

            exp = GH_ExpressionSyntaxWriter.RewriteForEvaluator(exp);
            eP.CacheSymbols(exp);

            // Read photo into raw curves list
            List<Curve> jCurves = ReadPhoto.trace(filename, this.blur, this.jump, this.debug, out this.times);

            // Add height to paths using expression
            for (int i = 0; i < jCurves.Count; i++)
            {
                double cp = 0;
                double cl = jCurves[i]?.GetLength() ?? 0;
                Polyline pl = new Polyline();
                jCurves[i]?.TryGetPolyline(out pl);
                for (int j = 0; j < pl.Count; j++)
                {
                    if (j > 0) { cp += (pl[j] - pl[j - 1]).Length / cl; }
                    eP.AddVariable("x", cp);
                    double h = eP.Evaluate()?._Double ?? 0.0;
                    pl[j] = new Point3d(pl[j].X, pl[j].Y, h);
                }

                jCurves[i] = new PolylineCurve(pl);
            }

            // Turn polylines into nice smooth curves
            List<Curve> tCurves = new List<Curve>();
            foreach (Curve c in jCurves)
            {
                List<Point3d> op = new List<Point3d>();
                c.TryGetPolyline(out Polyline pl);
                for (int j = 1; j < pl.Count; j++)
                {
                    if ((pl[j - 1] - pl[j]).Length > 5)
                    {
                        op.Add(0.5 * pl[j - 1] + 0.5 * pl[j]);
                    }

                    if (j < pl.Count - 1)
                    {
                        double ang = Vector3d.VectorAngle(pl[j] - pl[j - 1], pl[j + 1] - pl[j]);
                        if (ang > Math.PI / 2.1) { op.Add(pl[j]); }
                    }

                    op.Add(pl[j]);
                }

                tCurves.Add(Curve.CreateControlPointCurve(op));
            }

            da.SetDataList(0, tCurves);
            da.SetDataList(1, jCurves);
        }

        /// <inheritdoc />
        /// <summary>TODO The append additional component menu items.</summary>
        /// <param name="menu">TODO The menu.</param>
        protected override void AppendAdditionalComponentMenuItems([CanBeNull] ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            // Menu_AppendNumber(menu, "Mega Pixels", this.MaxFile, "Largest file to process (in megapixels)");
            Menu_AppendSeparator(menu);

            Menu_AppendItem(menu, "Tracing Settings");

            // Menu_AppendNumber(menu, "Jump", this.Jump,"Distance to connect edges (in pixels)");
            // Menu_AppendNumber(menu, "Blur", this.Blur,"Radius of blur (in pixels)");
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Debug", this.debugClicked, true, this.debug);

            if (!this.debug) { return; }

            foreach (string s in this.times) { Menu_AppendItem(menu, s); }
            Menu_AppendItem(menu, "Copy Data", this.copyDataClicked);
        }

        // ReSharper disable once UnusedMember.Local
        /// <summary>TODO The menu append number.</summary>
        /// <param name="menu">TODO The menu.</param>
        /// <param name="name">TODO The name.</param>
        /// <param name="val">TODO The val.</param>
        /// <param name="desc">TODO The desc.</param>
        /// <returns>The <see cref="NumericUpDown"/>.</returns>
        [NotNull]
        private NumericUpDown menuAppendNumber([NotNull] ToolStrip menu, [CanBeNull] string name, int val, [CanBeNull] string desc)
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
            uD.ValueChanged += this.traceSettings;
            uD.Width = 50;

            Label l = new Label
                {
                    Text = name
                };

            mI.Controls.Add(l);
            mI.Controls.Add(uD);

            mI.Height = uD.Height + 6;

            ToolStripItem tSi = new ToolStripControlHost(mI)
                {
                    ToolTipText = desc
                };

            menu.Items.Add(tSi);

            return uD;
        }

        /// <summary>TODO The copy data clicked.</summary>
        /// <param name="sender">TODO The sender.</param>
        /// <param name="e">TODO The e.</param>
        private void copyDataClicked([NotNull] object sender, [CanBeNull] EventArgs e)
        {
            System.Text.StringBuilder traceData = new System.Text.StringBuilder();

            foreach (string s in this.times) { traceData.AppendLine(s); }

            Clipboard.SetText(traceData.ToString());
        }

        /// <summary>TODO The debug clicked.</summary>
        /// <param name="sender">TODO The sender.</param>
        /// <param name="e">TODO The e.</param>
        private void debugClicked([NotNull] object sender, [CanBeNull] EventArgs e)
        {
            this.RecordUndoEvent("Trace_Debug");
            this.debug = !this.debug;
            this.ExpireSolution(true);
        }

        /// <summary>TODO The trace settings.</summary>
        /// <param name="sender">TODO The sender.</param>
        /// <param name="e">TODO The e.</param>
        private void traceSettings([NotNull] object sender, [CanBeNull] EventArgs e)
        {
            NumericUpDown ud = (NumericUpDown)sender;
            this.RecordUndoEvent(ud.Name);
            switch (ud.Name)
            {
                case "Blur":
                    this.blur = (int)ud.Value;
                    break;
                case "Jump":
                    this.jump = (int)ud.Value;
                    break;
                case "Mega Pixels":
                    this.maxFile = (int)ud.Value;
                    break;
            }
        }

        // Need to save and recover the trace settings
        /// <inheritdoc />
        /// <summary>TODO The write.</summary>
        /// <param name="writer">TODO The writer.</param>
        /// <returns>The <see cref="T:System.Boolean" />.</returns>
        public override bool Write([CanBeNull] GH_IO.Serialization.GH_IWriter writer)
        {
            if (writer == null) { return base.Write(null); }

            // First add our own fields.
            writer.SetInt32("MaxFile", this.maxFile);
            writer.SetInt32("Jump", this.jump);
            writer.SetInt32("Blur", this.blur);
            writer.SetBoolean("Debug", this.debug);

            // Then call the base class implementation.
            return base.Write(writer);
        }

        /// <inheritdoc />
        /// <summary>TODO The read.</summary>
        /// <param name="reader">TODO The reader.</param>
        /// <returns>The <see cref="T:System.Boolean" />.</returns>
        public override bool Read([CanBeNull] GH_IO.Serialization.GH_IReader reader)
        {
            if (reader == null) { return false; }

            // First read our own fields.
            if (reader.ItemExists("MaxFile"))
            { this.maxFile = reader.GetInt32("MaxFile"); }
            if (reader.ItemExists("Jump"))
            { this.jump = reader.GetInt32("Jump"); }
            if (reader.ItemExists("Blur"))
            { this.blur = reader.GetInt32("Blur"); }
            if (reader.ItemExists("Debug"))
            { this.debug = reader.GetBoolean("Debug"); }

            // Then call the base class implementation.
            return base.Read(reader);
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override Bitmap Icon => Properties.Resources.phototrace;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{7270759F-B5DA-46BB-B459-C98250ABB995}");
    }
}