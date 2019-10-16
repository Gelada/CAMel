// Heavily influenced by the click-able preview from https://github.com/mazhuravlev/grasshopper-addons

namespace CAMel.GH
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using CAMel.Types;

    using GH_IO.Serialization;

    using Grasshopper;
    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Parameters;
    using Grasshopper.Kernel.Types;

    using JetBrains.Annotations;

    using Rhino;
    using Rhino.Display;
    using Rhino.DocObjects;
    using Rhino.Geometry;
    using Rhino.Input;
    using Rhino.Input.Custom;

    /// <inheritdoc />
    /// <summary>TODO The c_ organise paths.</summary>
    [UsedImplicitly]
    public class C_OrganisePaths : GH_Component // locally implements IGH_PreviewObject
    {
        /// <summary>TODO The aug curve.</summary>
        private class AugCurve
        {
            /// <summary>Gets the c.</summary>
            [NotNull]
            internal Curve c { get; }
            /// <summary>Gets or sets the key.</summary>
            internal double key { get; set; }
            /// <summary>Gets the id.</summary>
            internal Guid id { get; }
            /// <summary>Gets or sets the side.</summary>
            internal double side { get; set; }

            /// <summary>Initializes a new instance of the <see cref="AugCurve"/> class.</summary>
            /// <param name="c">TODO The c.</param>
            /// <param name="id">TODO The id.</param>
            internal AugCurve([NotNull] Curve c, Guid id)
            {
                this.c = c;
                this.id = id;
                this.key = double.NaN;
                this.side = -1;
            }
        }

        /// <summary>TODO The in active document.</summary>
        private bool inActiveDocument;
        /// <summary>TODO The enabled.</summary>
        private bool enabled;

        /// <summary>TODO The click q.</summary>
        /// <returns>The <see cref="bool"/>.</returns>
        internal bool clickQ() => this.enabled && this.inActiveDocument;

        /// <summary>TODO The click.</summary>
        [UsedImplicitly] private readonly PathClick click;
        /// <summary>TODO The doc.</summary>
        private GH_Document doc;

        /// <summary>TODO The curves.</summary>
        [ItemNotNull, NotNull] private List<AugCurve> curves;
        /// <summary>TODO The all keys.</summary>
        [NotNull] private SortedSet<double> allKeys;

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_OrganisePaths()
            : base(
                "Organise Paths", "OrgPth",
                "Reorder a collection of curves",
                "CAMel", " ToolPaths")
        {
            this.click = new PathClick(this);
            this.curves = new List<AugCurve>();
            this.allKeys = new SortedSet<double>();
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddCurveParameter("Paths", "P", "Paths to reorder", GH_ParamAccess.list);
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddCurveParameter("Reordered", "R", "Reordered Paths", GH_ParamAccess.list);
            pManager.AddNumberParameter("Offsets", "O", "Offsets for individual curves.", GH_ParamAccess.list);
        }

        /// <inheritdoc />
        /// <summary>TODO The before solve instance.</summary>
        protected override void BeforeSolveInstance()
        {
            this.doc = this.OnPingDocument();
            if (Instances.ActiveCanvas?.Document != null)
            {
                this.inActiveDocument = Instances.ActiveCanvas.Document == this.doc &&
                                        Instances.ActiveCanvas.Document.Context == GH_DocumentContext.Loaded;

                Instances.ActiveCanvas.Document.ContextChanged -= this.contextChanged;
                Instances.ActiveCanvas.Document.ContextChanged += this.contextChanged;
            }

            base.BeforeSolveInstance();
        }

        /// <summary>TODO The context changed.</summary>
        /// <param name="sender">TODO The sender.</param>
        /// <param name="e">TODO The e.</param>
        /// <exception cref="ArgumentNullException"></exception>
        private void contextChanged([CanBeNull] object sender, [NotNull] GH_DocContextEventArgs e)
        {
            if (e == null) { throw new ArgumentNullException(); }
            this.inActiveDocument = e.Document == this.doc && e.Context == GH_DocumentContext.Loaded;
        }
        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }

            List<GH_Curve> paths = new List<GH_Curve>();
            this.enabled = false;
            RhinoDoc uDoc = RhinoDoc.ActiveDoc;
            if (!da.GetDataList("Paths", paths) || paths.Count == 0 || uDoc?.Objects == null) { return; }

            // Insist on reference curves
            if (paths.Any(p => p?.IsReferencedGeometry == false))
            {
                this.AddRuntimeMessage(
                    GH_RuntimeMessageLevel.Error,
                    "Only referenced curves can be organised with this component. If you wish to organise grasshopper curves, first bake.");
                return;
            }

            // Check for current keys stored in the rhino file
            // set the keys for the curves read in
            this.allKeys = new SortedSet<double>();
            this.curves = new List<AugCurve>();
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (GH_Curve curve in paths)
            {
                if (curve?.Value != null) { this.curves.Add(new AugCurve(curve.Value, curve.ReferenceID)); }
            }

            foreach (RhinoObject ro in uDoc.Objects)
            {
                double key = ro.getKey();
                if (double.IsNaN(key)) { continue; }
                this.allKeys.Add(key);

                foreach (AugCurve c in this.curves.Where(c => c != null && c.id == ro.Id))
                {
                    c.key = key;
                    c.side = ro.Attributes.getSide();
                }
            }

            // restore sanity if no values were found.
            if (this.allKeys.Count == 0) { this.allKeys.Add(0); }

            // Add keys to new paths with open paths at the start and closed paths at the end
            foreach (AugCurve c in this.curves.Where(c => c != null))
            {
                this.enabled = true;
                if (!double.IsNaN(c.key)) { continue; }
                if (c.c.IsClosed)
                {
                    c.key = this.allKeys.Max + 1;
                    this.allKeys.Add(this.allKeys.Max + 1);
                    RhinoObject ro = uDoc.Objects.Find(c.id);
                    ro?.setKey(c.key);
                    ro?.CommitChanges();
                }
                else
                {
                    c.side = 0;
                    c.key = this.allKeys.Min - 1;
                    this.allKeys.Add(this.allKeys.Min - 1);
                    RhinoObject ro = uDoc.Objects.Find(c.id);
                    ro?.setKey(c.key);
                    ro?.CommitChanges();
                }
            }

            this.curves.Sort(CurveC);

            List<double> offSets = new List<double>();
            List<Curve> sorted = new List<Curve>();
            foreach (AugCurve c in this.curves)
            {
                double os = 0;
                if (c != null) { os = c.side; }
                sorted.Add(c?.c);
                offSets.Add(os);
            }

            da.SetDataList(0, sorted);
            da.SetDataList(1, offSets);
        }

        /// <inheritdoc />
        /// <summary>TODO The curve comp.</summary>
        private class CurveComp : IComparer<AugCurve>
        {
            /// <inheritdoc />
            /// <summary>TODO The compare.</summary>
            /// <param name="x">TODO The x.</param>
            /// <param name="y">TODO The y.</param>
            /// <returns>The <see cref="T:System.Int32" />.</returns>
            public int Compare(AugCurve x, AugCurve y) => x?.key.CompareTo(y?.key) ?? 0;
        }

        /// <summary>TODO The curve c.</summary>
        private static readonly CurveComp CurveC = new CurveComp();

        /// <summary>TODO The dot size.</summary>
        private const int DotSize = 11;
        /// <summary>TODO The dot shift.</summary>
        private readonly Vector3d dotShift = new Vector3d(1, 1, 1);
        /// <summary>TODO The find.</summary>
        /// <param name="l">TODO The l.</param>
        /// <param name="vP">TODO The v p.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        internal bool find(Line l, [NotNull] RhinoViewport vP)
        {
            if (vP == null) { throw new ArgumentNullException(); }

            RhinoDoc uDoc = RhinoDoc.ActiveDoc;
            if (uDoc?.Objects == null) { return false; }

            int clicked = -1;

            List<int> sel = new List<int>();
            for (int i = 0; i < this.curves.Count; i++)
            {
                AugCurve c = this.curves[i];
                RhinoObject ro = uDoc.Objects.Find(c.id);
                if ((ro?.IsSelected(true) ?? 0) > 0) { sel.Add(i); }

                vP.GetWorldToScreenScale(c.c.PointAtStart, out double pixelsPerUnit);
                double dist = l.DistanceTo(c.c.PointAtStart + DotSize / pixelsPerUnit * this.dotShift, false);
                if (dist * pixelsPerUnit < DotSize) { clicked = i; }
            }

            // return if click did not attach to a path
            if (clicked < 0) { return false; }

            GetInteger gi;

            // if the clicked path is not selected or is the only thing selected, just deal with that.
            double side;
            if (sel.Count <= 1 || !sel.Contains(clicked))
            {
                AugCurve c = this.curves[clicked];
                side = c.side;
                if (c.c.IsClosed)
                {
                    bool cC = c.c.ClosedCurveOrientation(-Vector3d.ZAxis) == CurveOrientation.CounterClockwise;
                    gi = getClosed(clicked, cC, c.side, out OptionToggle counterClock);
                    while (true)
                    {
                        GetResult getR = gi.Get();
                        if (getR == GetResult.Option)
                        {
                            switch (gi.Option()?.EnglishName)
                            {
                                case "MoveSeam":
                                    double t = getSeam(c.c);
                                    c.c.setNewSeam(t);
                                    c.side = side;
                                    if (cC == counterClock.CurrentValue) { return true; }
                                    c.c.Reverse();
                                    c.side = -c.side;
                                    return true;
                                case "Side":
                                    switch (gi.Option()?.CurrentListOptionIndex)
                                    {
                                        case 0:
                                            side = 0;
                                            break;
                                        case 1:
                                            if (cC) { side = -1; }
                                            else { side = 1; }
                                            break;
                                        case 2:
                                            if (cC) { side = 1; }
                                            else { side = -1; }
                                            break;
                                    }

                                    break;
                            }

                            continue;
                        }

                        if (getR != GetResult.Number && getR != GetResult.Nothing) { return true; }
                        c.side = side;
                        if (cC != counterClock.CurrentValue)
                        {
                            c.c.Reverse();
                            c.side = -c.side;
                        }

                        this.reOrder(c, clicked, gi.Number());
                        return true;
                    }
                }

                // the path is not closed
                gi = getOpen(clicked, c.side, out OptionToggle flip);
                while (true)
                {
                    GetResult getR = gi.Get();
                    if (getR == GetResult.Option)
                    {
                        switch (gi.Option()?.EnglishName)
                        {
                            case "Side":
                                switch (gi.Option()?.CurrentListOptionIndex)
                                {
                                    case 0:
                                        side = 0;
                                        break;
                                    case 1:
                                        side = -1;
                                        break;
                                    case 2:
                                        side = 1;
                                        break;
                                }

                                break;
                        }

                        continue;
                    }

                    if (getR != GetResult.Number && getR != GetResult.Nothing) { return true; }
                    c.side = side;
                    if (flip.CurrentValue)
                    {
                        c.c.Reverse();
                        c.side = -c.side;
                    }

                    this.reOrder(c, clicked, gi.Number());
                    return true;
                }
            }

            // Now deal with a larger selection
            gi = getMultiple(clicked);
            side = 0;
            double direction = 0;
            while (true)
            {
                GetResult getR = gi.Get();
                if (getR == GetResult.Option)
                {
                    switch (gi.Option()?.EnglishName)
                    {
                        case "Side":
                            side = gi.Option()?.CurrentListOptionIndex ?? 0;
                            break;
                        case "Direction":
                            direction = gi.Option()?.CurrentListOptionIndex ?? 0;
                            break;
                    }

                    continue;
                }

                if (getR == GetResult.Number || getR == GetResult.Nothing)
                {
                    foreach (AugCurve c in sel.Select(i => this.curves[i]))
                    {
                        switch (side)
                        {
                            case 1:
                                if (c?.c.IsClosed ?? false)
                                {
                                    if (c.c.ClosedCurveOrientation(-Vector3d.ZAxis) == CurveOrientation.CounterClockwise)
                                    { c.side = -1; }
                                    else { c.side = 1; }
                                }

                                break;
                            case 2:
                                if (c?.c.IsClosed ?? false)
                                {
                                    if (c.c.ClosedCurveOrientation(-Vector3d.ZAxis) != CurveOrientation.CounterClockwise
                                    ) { c.side = -1; }
                                    else { c.side = 1; }
                                }

                                break;
                            case 3:
                                if (c != null) { c.side = -1; }
                                break;
                            case 4:
                                if (c != null) { c.side = 1; }
                                break;
                        }

                        switch (direction)
                        {
                            case 1:
                                if ((c?.c.IsClosed ?? false) && c.c.ClosedCurveOrientation(-Vector3d.ZAxis) ==
                                    CurveOrientation.Clockwise)
                                {
                                    c.c.Reverse();
                                    c.side = -c.side;
                                }

                                break;
                            case 2:
                                if ((c?.c.IsClosed ?? false) && c.c.ClosedCurveOrientation(-Vector3d.ZAxis) ==
                                    CurveOrientation.CounterClockwise)
                                {
                                    c.c.Reverse();
                                    c.side = -c.side;
                                }

                                break;
                        }
                    }
                }

                if (getR == GetResult.Number) { this.reOrder(sel, gi.Number() - 1); }
                return true;
            }
        }

        /// <summary>TODO The re order.</summary>
        /// <param name="c">TODO The c.</param>
        /// <param name="i">TODO The i.</param>
        /// <param name="newPos">TODO The new pos.</param>
        private void reOrder([NotNull] AugCurve c, int i, int newPos)
        {
            if (newPos == i + 1) { return; }
            double newKey;
            if (newPos <= 1) { newKey = this.allKeys.Min - 1.0; }
            else if (newPos >= this.curves.Count) { newKey = this.allKeys.Max + 1.0; }
            else
            {
                int uPos = newPos;
                if (newPos - 1 > i) { uPos++; }
                double aboveKey = this.curves[uPos - 1].key;
                double belowKey = this.allKeys
                    .GetViewBetween(double.NegativeInfinity, aboveKey - CAMel_Goo.Tolerance).Max;
                newKey = (aboveKey + belowKey) / 2.0;
            }

            this.allKeys.Add(newKey);
            c.key = newKey;
        }

        /// <summary>TODO The re order.</summary>
        /// <param name="sel">TODO The sel.</param>
        /// <param name="newPos">TODO The new pos.</param>
        private void reOrder([NotNull] IList<int> sel, int newPos)
        {
            if (sel.Count == 1 && sel[0] == newPos) { return; }
            Interval newKeys;
            int count = sel.Count;
            int uPos = newPos + count - sel.Count(x => x >= newPos + count);
            if (uPos <= count) { newKeys = new Interval(this.allKeys.Min - sel.Count - 1, this.allKeys.Min); }
            else if (uPos >= this.curves.Count) { newKeys = new Interval(this.allKeys.Max, this.allKeys.Max + sel.Count + 1); }
            else
            {
                double aboveKey = this.curves[uPos].key;
                double belowKey = this.allKeys
                    .GetViewBetween(double.NegativeInfinity, aboveKey - CAMel_Goo.Tolerance).Max;
                newKeys = new Interval(belowKey, aboveKey);
            }

            for (int i = 0; i < sel.Count; i++)
            {
                double newKey = newKeys.ParameterAt((i + 1) / (double)(sel.Count + 1));
                this.curves[sel[i]].key = newKey;
                this.allKeys.Add(newKey);
            }
        }

        /// <summary>TODO The get seam.</summary>
        /// <param name="c">TODO The c.</param>
        /// <returns>The <see cref="double"/>.</returns>
        private static double getSeam([NotNull] Curve c)
        {
            GetPoint gp = new GetPoint();
            gp.SetCommandPrompt("Set new seam");
            gp.Constrain(c, false);
            gp.AcceptNothing(true);
            gp.Get();
            if (gp.PointOnCurve(out double t) == null) { t = double.NaN; }
            return t;
        }

        /// <summary>TODO The set up.</summary>
        /// <param name="i">TODO The i.</param>
        /// <returns>The <see cref="GetInteger"/>.</returns>
        [NotNull]
        private static GetInteger setUp(int i)
        {
            GetInteger gi = new GetInteger();
            gi.SetCommandPrompt("Reorder path");
            gi.SetDefaultNumber(i + 1);
            gi.AcceptNothing(true);
            gi.SetCommandPromptDefault((i + 1).ToString());
            return gi;
        }

        /// <summary>TODO The get closed.</summary>
        /// <param name="i">TODO The i.</param>
        /// <param name="cC">TODO The c c.</param>
        /// <param name="side">TODO The side.</param>
        /// <param name="counterClock">TODO The counter clock.</param>
        /// <returns>The <see cref="GetInteger"/>.</returns>
        [NotNull]
        private static GetInteger getClosed(int i, bool cC, double side, out OptionToggle counterClock)
        {
            GetInteger gi = setUp(i);
            counterClock = new OptionToggle(cC, "Clockwise", "CounterClockwise");
            gi.AddOptionToggle("Direction", ref counterClock);
            List<string> sideL = new List<string> { "Centre", "Inside", "Outside" };
            int dVal = 0;
            if (cC && side < 0 || !cC && side > 0) { dVal = 1; } // cutting inside
            if (cC && side > 0 || !cC && side < 0) { dVal = 2; } // cutting outside
            gi.AddOptionList("Side", sideL, dVal);
            gi.AddOption("MoveSeam");

            return gi;
        }

        /// <summary>TODO The get open.</summary>
        /// <param name="i">TODO The i.</param>
        /// <param name="side">TODO The side.</param>
        /// <param name="flip">TODO The flip.</param>
        /// <returns>The <see cref="GetInteger"/>.</returns>
        [NotNull]
        private static GetInteger getOpen(int i, double side, out OptionToggle flip)
        {
            GetInteger gi = setUp(i);
            flip = new OptionToggle(false, "Leave", "Flip");
            gi.AddOptionToggle("Direction", ref flip);
            List<string> sideL = new List<string> { "Centre", "Left", "Right" };
            int dVal = 0;
            if (side < 0) { dVal = 1; } // cutting left
            if (side > 0) { dVal = 2; } // cutting right
            gi.AddOptionList("Side", sideL, dVal);

            return gi;
        }

        /// <summary>TODO The get multiple.</summary>
        /// <param name="i">TODO The i.</param>
        /// <returns>The <see cref="GetInteger"/>.</returns>
        [NotNull]
        private static GetInteger getMultiple(int i)
        {
            GetInteger gi = setUp(i);
            gi.ClearDefault();
            gi.SetCommandPromptDefault("");
            List<string> dir = new List<string> { "Leave", "CounterClockAll", "ClockAll" };
            gi.AddOptionList("Direction", dir, 0);
            List<string> side = new List<string>
                {
                    "Leave",
                    "InsideAll",
                    "OutsideAll",
                    "LeftAll",
                    "RightAll"
                };
            gi.AddOptionList("Side", side, 0);

            return gi;
        }

        /// <summary>TODO The change ref curves.</summary>
        internal void changeRefCurves()
        {
            RhinoDoc uDoc = RhinoDoc.ActiveDoc;
            if (uDoc?.Objects == null) { return; }

            foreach (AugCurve c in this.curves)
            {
                RhinoObject ro = uDoc.Objects.Find(c.id);

                // Save side and key information
                ro?.Attributes.setSide(c.side);
                ro.setKey(c.key);

                // Check for curve direction and seam
                if (ro is CurveObject co && co.CurveGeometry != null)
                {
                    if (co.CurveGeometry.IsClosed && c.c.IsClosed)
                    {
                        // reverse source curve
                        if (co.CurveGeometry.ClosedCurveOrientation(-Vector3d.ZAxis) !=
                            c.c.ClosedCurveOrientation(-Vector3d.ZAxis)) { co.CurveGeometry.Reverse(); }
                        double t = c.c.getNewSeam();
                        if (!double.IsNaN(t))
                        {
                            c.c.ChangeClosedCurveSeam(t);
                            co.CurveGeometry.ChangeClosedCurveSeam(t);
                            c.c.setNewSeam(double.NaN);
                        }
                    }
                    else
                    {
                        if (c.c.PointAtStart != co.CurveGeometry.PointAtStart) { co.CurveGeometry.Reverse(); }
                    }
                }

                ro?.CommitChanges();
            }
        }

        /// <inheritdoc />
        public override BoundingBox ClippingBox => BoundingBox.Empty;

        /// <inheritdoc />
        public override void DrawViewportMeshes([CanBeNull] IGH_PreviewArgs args) { }

        /// <inheritdoc />
        public override void DrawViewportWires([CanBeNull] IGH_PreviewArgs args)
        {
            if (args?.Viewport == null) { return; }
            base.DrawViewportWires(args);

            if (!this.enabled || args.Viewport == null || args.Display == null) { return; }

            for (int i = 0; i < this.curves.Count; i++)
            {
                if (this.curves[i]?.c == null) { continue; }
                args.Viewport.GetWorldToScreenScale(this.curves[i].c.PointAtStart, out double pixelsPerUnit);

                System.Drawing.Color lineC = args.WireColour;
                if (this.Attributes?.Selected == true) { lineC = args.WireColour_Selected; }
                args.Display.DrawCurve(this.curves[i].c, lineC);

                args.Display.DrawDot(this.curves[i].c.PointAtStart + DotSize / pixelsPerUnit * this.dotShift, (i + 1).ToString());

                Line dir = new Line(this.curves[i].c.PointAtStart, this.curves[i].c.TangentAtStart * 50.0 / pixelsPerUnit);
                args.Display.DrawArrow(dir, System.Drawing.Color.AntiqueWhite);
            }
        }

        /// <inheritdoc />
        public override bool Write([CanBeNull] GH_IWriter writer)
        {
            // If we were handling persistent data (no connecting wires) update it so it has the latest values
            Param_Curve p = this.Params?.Input?[0] as Param_Curve;

            if (p?.PersistentData == null || this.Params.Input[0].SourceCount != 0) { return base.Write(writer); }

            p.PersistentData.ClearData();
            p.SetPersistentData(this.curves);
            return base.Write(writer);
        }

        /// <inheritdoc />
        public override void RemovedFromDocument([CanBeNull] GH_Document document)
        {
            this.enabled = false;
            this.ExpirePreview(true);
            base.RemovedFromDocument(document);
        }

        /// <inheritdoc />
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.organisepaths;

        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("{DC9920CF-7C48-4A75-B279-89A0C132E564}");
    }
}