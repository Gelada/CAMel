// Heavily influenced by the click-able preview from https://github.com/mazhuravlev/grasshopper-addons

using System;
using System.Collections.Generic;
using System.Linq;
using CAMel.Types;
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using JetBrains.Annotations;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace CAMel.GH
{
    [UsedImplicitly]
    public class C_OrganisePaths : GH_Component // locally implements IGH_PreviewObject
    {
        private class AugCurve
        {
            internal Curve c { get; set; }
            internal double key { get; set; }
            internal Guid id { get; set; }
            internal double side { get; set; }

            internal AugCurve([NotNull] Curve c, Guid id)
            {
                this.c = c;
                this.id = id;
                this.key = double.NaN;
                this.side = -1;
            }
        }

        private bool _inActiveDocument;
        private bool _enabled;

        internal bool clickQ() => this._enabled && this._inActiveDocument;

        [UsedImplicitly] private readonly PathClick _click;
        private GH_Document _doc;

        [NotNull] private List<AugCurve> _curves;
        [NotNull] private SortedSet<double> _allKeys;

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_OrganisePaths() : base("Organise Paths", "OrgPth", "Reorder a collection of curves", "CAMel", " ToolPaths")
        {
            this._click = new PathClick(this);
            this._curves = new List<AugCurve>();
            this._allKeys = new SortedSet<double>();
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

        protected override void BeforeSolveInstance()
        {
            this._doc = OnPingDocument();
            if (Instances.ActiveCanvas?.Document != null)
            {
                this._inActiveDocument = Instances.ActiveCanvas.Document == this._doc &&
                                         Instances.ActiveCanvas.Document.Context == GH_DocumentContext.Loaded;

                Instances.ActiveCanvas.Document.ContextChanged -= contextChanged;
                Instances.ActiveCanvas.Document.ContextChanged += contextChanged;
            }

            base.BeforeSolveInstance();
        }

        private void contextChanged([CanBeNull] object sender, [NotNull] GH_DocContextEventArgs e)
        {
            if (e == null) { throw new ArgumentNullException(); }
            this._inActiveDocument = e.Document == this._doc && e.Context == GH_DocumentContext.Loaded;
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
            this._enabled = false;
            if (!da.GetDataList("Paths", paths) || paths.Count == 0) { return; }
            RhinoDoc uDoc = RhinoDoc.ActiveDoc;
            if (uDoc?.Objects == null) { return; }

            // Insist on reference curves
            foreach (GH_Curve p in paths)
            {
                if (p == null || p.IsReferencedGeometry) { continue; }
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                    "Only referenced curves can be organised with this component. If you wish to organise grasshopper curves, first bake.");
                return;
            }


            // Check for current keys stored in the rhino file
            // set the keys for the curves read in
            this._allKeys = new SortedSet<double>();
            this._curves = new List<AugCurve>();
            foreach (GH_Curve curve in paths)
            {
                if(curve?.Value!=null) { this._curves.Add(new AugCurve(curve.Value, curve.ReferenceID)); }
            }

            foreach (RhinoObject ro in uDoc.Objects)
            {
                double key = ro.getKey();
                if (double.IsNaN(key)) { continue; }
                this._allKeys.Add(key);

                foreach (AugCurve c in this._curves)
                {
                    if (c == null || c.id != ro.Id)
                    { continue; }
                    c.key = key;
                    c.side = ro.Attributes.getSide();
                }
            }

            // restore sanity if no values were found.
            if (this._allKeys.Count == 0) { this._allKeys.Add(0); }

            // Add keys to new paths with open paths at the start and closed paths at the end
            foreach (AugCurve c in this._curves)
            {
                if (c == null){ continue; }
                this._enabled = true;
                if (!double.IsNaN(c.key)) { continue;}
                if (c.c.IsClosed)
                {
                    c.key = this._allKeys.Max + 1;
                    this._allKeys.Add(this._allKeys.Max + 1);
                    RhinoObject ro = uDoc.Objects.Find(c.id);
                    ro?.setKey(c.key);
                    ro?.CommitChanges();
                }
                else
                {
                    c.side = 0;
                    c.key = this._allKeys.Min - 1;
                    this._allKeys.Add(this._allKeys.Min - 1);
                    RhinoObject ro = uDoc.Objects.Find(c.id);
                    ro?.setKey(c.key);
                    ro?.CommitChanges();
                }
            }

            this._curves.Sort(_CurveC);

            List<double> offSets = new List<double>();
            List<Curve> sorted = new List<Curve>();
            foreach (AugCurve c in this._curves)
            {
                double os = 0;
                if (c != null) { os = c.side; }
                sorted.Add(c?.c);
                offSets.Add(os);
            }
            da.SetDataList(0, sorted);
            da.SetDataList(1, offSets);
        }

        private class CurveComp : IComparer<AugCurve>
        {
            public int Compare(AugCurve x, AugCurve y)
            {
                return x?.key.CompareTo(y?.key) ?? 0;
            }
        }

        private static readonly CurveComp _CurveC = new CurveComp();

        private const int _DotSize = 11;
        private readonly Vector3d _dotShift = new Vector3d(1, 1, 1);
        internal bool find(Line l, [NotNull] RhinoViewport vP)
        {
            if (vP == null) { throw new ArgumentNullException(); }

            RhinoDoc uDoc = RhinoDoc.ActiveDoc;
            if (uDoc?.Objects == null) { return false; }

            int clicked = -1;

            List<int> sel = new List<int>();
            for (int i = 0; i < this._curves.Count; i++)
            {
                AugCurve c = this._curves[i];
                if (c == null) { continue; }
                RhinoObject ro = uDoc.Objects.Find(c.id);
                if ((ro?.IsSelected(true) ?? 0) > 0) { sel.Add(i); }

                vP.GetWorldToScreenScale(c.c.PointAtStart, out double pixelsPerUnit);
                double dist = l.DistanceTo(c.c.PointAtStart + _DotSize / pixelsPerUnit * this._dotShift, false);
                if (dist * pixelsPerUnit < _DotSize)
                {
                    clicked = i;
                }
            }

            // return if click did not attach to a path
            if (clicked < 0) { return false; }

            GetInteger gi;
            // if the clicked path is not selected or is the only thing selected, just deal with that.
            double side;
            if (sel.Count <= 1 || !sel.Contains(clicked))
            {
                AugCurve c = this._curves[clicked];
                if (c == null) { return false; }
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
                                    if (cC != counterClock.CurrentValue)
                                    {
                                        c.c.Reverse();
                                        c.side = -c.side;
                                    }
                                    return true;
                                case "Side":
                                    switch (gi.Option()?.CurrentListOptionIndex)
                                    {
                                        case 0:
                                            side = 0;
                                            break;
                                        case 1:
                                            if (cC) { side = -1; } else { side = 1; }
                                            break;
                                        case 2:
                                            if (cC) { side = 1; } else { side = -1; }
                                            break;
                                    }
                                    break;
                            }
                            continue;
                        }
                        if (getR == GetResult.Number || getR == GetResult.Nothing)
                        {
                            c.side = side;
                            if (cC != counterClock.CurrentValue)
                            {
                                c.c.Reverse();
                                c.side = -c.side;
                            }
                            reOrder(c, clicked, gi.Number());
                        }
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
                    if (getR == GetResult.Number || getR == GetResult.Nothing)
                    {
                        c.side = side;
                        if (flip.CurrentValue)
                        {
                            c.c.Reverse();
                            c.side = -c.side;
                        }
                        reOrder(c, clicked, gi.Number());
                    }
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
                            side = gi.Option().CurrentListOptionIndex;
                            break;
                        case "Direction":
                            direction = gi.Option().CurrentListOptionIndex;
                            break;
                    }
                    continue;
                }
                if (getR == GetResult.Number || getR == GetResult.Nothing)
                {
                    foreach (int i in sel)
                    {
                        AugCurve c = this._curves[i];
                        switch (side)
                        {
                            case 1:
                                if (c.c.IsClosed)
                                {
                                    if (c.c.ClosedCurveOrientation(-Vector3d.ZAxis)== CurveOrientation.CounterClockwise )
                                    { c.side = -1; } else { c.side = 1; }
                                }
                                break;
                            case 2:
                                if (c.c.IsClosed)
                                {
                                    if (c.c.ClosedCurveOrientation(-Vector3d.ZAxis) == CurveOrientation.CounterClockwise)
                                    { c.side = 1; }
                                    else { c.side = -1; }
                                }
                                break;
                            case 3:
                                c.side = -1;
                                break;
                            case 4:
                                c.side = 1;
                                break;
                        }
                        switch (direction)
                        {
                            case 1:
                                if (c.c.IsClosed && c.c.ClosedCurveOrientation(-Vector3d.ZAxis) ==
                                    CurveOrientation.Clockwise)
                                {
                                    c.c.Reverse();
                                    c.side = -c.side;
                                }
                                break;
                            case 2:
                                if (c.c.IsClosed && c.c.ClosedCurveOrientation(-Vector3d.ZAxis) ==
                                    CurveOrientation.CounterClockwise)
                                {
                                    c.c.Reverse();
                                    c.side = -c.side;
                                }
                                break;
                        }
                    }
                }

                if (getR == GetResult.Number) { reOrder(sel, gi.Number()-1); }
                return true;
            }
        }

        private void reOrder([NotNull] AugCurve c, int i, int newPos)
        {
            if (newPos == i + 1) { return; }
            double newKey;
            if (newPos <= 1) { newKey = this._allKeys.Min - 1.0; }
            else if (newPos >= this._curves.Count) { newKey = this._allKeys.Max + 1.0; }
            else
            {
                int uPos = newPos;
                if (newPos - 1 > i) { uPos++; }
                double aboveKey = this._curves[uPos - 1]?.key ?? double.NaN;
                double belowKey = this._allKeys
                    .GetViewBetween(double.NegativeInfinity, aboveKey - CAMel_Goo.Tolerance).Max;
                newKey = (aboveKey + belowKey) / 2.0;
            }

            this._allKeys.Add(newKey);
            c.key = newKey;
        }

        private void reOrder([NotNull] List<int> sel, int newPos)
        {
            if (sel.Count == 1 && sel[0] == newPos) { return; }
            Interval newKeys;
            int count = sel.Count;
            int uPos = newPos + count - sel.Count(x => x >= newPos + count);
            if (uPos <= count) { newKeys = new Interval(this._allKeys.Min - sel.Count - 1, this._allKeys.Min); }
            else if (uPos > this._curves.Count) { newKeys = new Interval(this._allKeys.Max, this._allKeys.Max + sel.Count + 1); }
            else
            {
                double aboveKey = this._curves[uPos]?.key ?? double.NaN;
                double belowKey = this._allKeys
                    .GetViewBetween(double.NegativeInfinity, aboveKey - CAMel_Goo.Tolerance).Max;
                newKeys = new Interval(belowKey, aboveKey);
            }
            for (int i = 0; i < sel.Count; i++)
            {
                double newKey = newKeys.ParameterAt((i + 1) / (double)(sel.Count + 1));
                this._curves[sel[i]].key = newKey;
                this._allKeys.Add(newKey);
            }
        }

        private double getSeam([NotNull] Curve c)
        {
            GetPoint gp = new GetPoint();
            gp.SetCommandPrompt("Set new seam");
            gp.Constrain(c, false);
            gp.AcceptNothing(true);
            gp.Get();
            if (gp.PointOnCurve(out double t) == null) { t = double.NaN; }
            return t;
        }

        [NotNull]
        private GetInteger setUp(int i)
        {
            GetInteger gi = new GetInteger();
            gi.SetCommandPrompt("Reorder path");
            gi.SetDefaultNumber(i + 1);
            gi.AcceptNothing(true);
            gi.SetCommandPromptDefault((i + 1).ToString());
            return gi;
        }

        [NotNull]
        private GetInteger getClosed(int i, bool cC, double side, out OptionToggle counterClock)
        {
            GetInteger gi = setUp(i);
            counterClock = new OptionToggle(cC, "Clockwise", "CounterClockwise");
            gi.AddOptionToggle("Direction", ref counterClock);
            List<string> sideL = new List<string> { "Centre", "Inside", "Outside" };
            int dVal = 0;
            if (cC && side < 0 || !cC && side > 0) { dVal = 1; } // cutting inside
            if (cC && side > 0 || !cC && side < 0) { dVal = 2; } // cutting outside
            gi.AddOptionList("Side", sideL,dVal);
            gi.AddOption("MoveSeam");

            return gi;
        }

        [NotNull]
        private GetInteger getOpen(int i, double side, out OptionToggle flip)
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

        [NotNull]
        private GetInteger getMultiple(int i)
        {
            GetInteger gi = setUp(i);
            gi.ClearDefault();
            gi.SetCommandPromptDefault("");
            List<string> dir = new List<string> {"Leave", "CounterClockAll", "ClockAll"};
            gi.AddOptionList("Direction", dir, 0);
            List<string> side = new List<string> { "Leave", "InsideAll", "OutsideAll", "LeftAll", "RightAll" };
            gi.AddOptionList("Side", side, 0);

            return gi;
        }

        internal void changeRefCurves()
        {
            RhinoDoc uDoc = RhinoDoc.ActiveDoc;
            if (uDoc?.Objects == null) { return; }

            foreach (AugCurve c in this._curves)
            {
                RhinoObject ro = uDoc.Objects.Find(c.id);

                // Save side and key information
                ro?.Attributes.setSide(c.side);
                ro?.Attributes.setKey(c.key);

                // Check for curve direction and seam
                if (ro is CurveObject co && c != null && co.CurveGeometry != null)
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
                    } else
                    {
                        if (c.c.PointAtStart != co.CurveGeometry.PointAtStart) { co.CurveGeometry.Reverse(); }
                    }

                }
                ro?.CommitChanges();
            }
        }

        //Return a BoundingBox that contains all the geometry you are about to draw.
        public override BoundingBox ClippingBox => BoundingBox.Empty;

        //Draw all meshes in this method.
        public override void DrawViewportMeshes([CanBeNull] IGH_PreviewArgs args) { }

        //Draw all wires and points in this method.
        public override void DrawViewportWires([CanBeNull] IGH_PreviewArgs args)
        {
            if (args?.Viewport == null) { return; }
            base.DrawViewportWires(args);

            if (!this._enabled || args.Viewport == null || args.Display == null) { return; }

            for (int i = 0; i < this._curves.Count; i++)
            {
                if (this._curves[i]?.c == null) { continue; }
                args.Viewport.GetWorldToScreenScale(this._curves[i].c.PointAtStart, out double pixelsPerUnit);

                System.Drawing.Color lineC = args.WireColour;
                if (this.Attributes != null && this.Attributes.Selected) { lineC = args.WireColour_Selected; }
                args.Display.DrawCurve(this._curves[i].c, lineC);

                args.Display.DrawDot(this._curves[i].c.PointAtStart + _DotSize / pixelsPerUnit * this._dotShift, (i + 1).ToString());

                Line dir = new Line(this._curves[i].c.PointAtStart, this._curves[i].c.TangentAtStart * 50.0 / pixelsPerUnit);
                args.Display.DrawArrow(dir, System.Drawing.Color.AntiqueWhite);
            }
        }

        public override bool Write([CanBeNull] GH_IWriter writer)
        {
            // If we were handling persistent data (no connecting wires) update it so it has the latest values
            Param_Curve p = this.Params?.Input?[0] as Param_Curve;

            if (p?.PersistentData == null || this.Params.Input[0].SourceCount != 0) { return base.Write(writer); }

            p.PersistentData.ClearData();
            p.SetPersistentData(this._curves);
            return base.Write(writer);
        }

        public override void RemovedFromDocument([CanBeNull] GH_Document document)
        {
            this._enabled = false;
            ExpirePreview(true);
            base.RemovedFromDocument(document);
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.organisepaths;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{DC9920CF-7C48-4A75-B279-89A0C132E564}");
    }
}