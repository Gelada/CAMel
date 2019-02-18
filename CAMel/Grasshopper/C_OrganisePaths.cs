// Heavily influenced by the click-able preview from https://github.com/mazhuravlev/grasshopper-addons

using System;
using System.Collections.Generic;
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
using Rhino.UI;

namespace CAMel.Grasshopper
{
    [UsedImplicitly]
    public class C_OrganisePaths : GH_Component // locally implements IGH_PreviewObject
    {
        private bool _inActiveDocument;
        private bool _enabled;

        internal bool clickQ() => this._enabled && this._inActiveDocument;

        // ReSharper disable once NotAccessedField.Local
        private readonly PathClick _click;
        private GH_Document _doc;

        [NotNull] private List<Curve> _curves;
        private List<GH_Curve> _latestPaths;

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_OrganisePaths() : base("Organise Paths", "OrgPth", "Reorder a collection of curves", "CAMel", "Test")
        {
            this._click = new PathClick(this);
            this._curves = new List<Curve>();
            this._latestPaths = new List<GH_Curve>();
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

        private const int _DotSize = 11;
        private readonly Vector3d _dotShift = new Vector3d(1, 1, 1);
        internal bool found(Line l, [NotNull] RhinoViewport vP)
        {
            if (vP == null) { throw new ArgumentNullException(); }
            for (int i = 0;i< this._curves.Count; i++)
            {
                Curve c = this._curves[i];
                if (c == null) { continue; }
                vP.GetWorldToScreenScale(c.PointAtStart, out double pixelsPerUnit);

                if (!(l.DistanceTo(c.PointAtStart + _DotSize / pixelsPerUnit * this._dotShift, false) * pixelsPerUnit <
                      _DotSize)) { continue; }

                Dialogs.ShowEditBox("Reposition", "New position", (i+1).ToString(), false, out string newP);

                if (!int.TryParse(newP, out int newPos)) { return true; }

                double newKey;
                if (newPos <= 1)
                { newKey = this._curves[0].getKey() - 1.0; }
                else if (newPos >= this._curves.Count)
                { newKey = this._curves[this._curves.Count-1].getKey() + 1.0; }
                else
                { newKey = (this._curves[newPos - 2].getKey() + this._curves[newPos - 1].getKey())/2.0;}

                c.setKey(newKey);
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }
            this._enabled = true;
            List<GH_Curve> paths = new List<GH_Curve>();
            if (!da.GetDataList("Paths", paths))
            {
                this._enabled = false;
                return;
            }

            double minK = double.PositiveInfinity;
            double maxK = double.NegativeInfinity;

            // Check for current keys, if missing use stored keys
            foreach(GH_Curve p in paths)
            {
                if (p==null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Null curve ignored.");
                    continue;
                }
                if (double.IsNaN(p.Value.getKey()) && p.IsReferencedGeometry)
                {
                    if (RhinoDoc.ActiveDoc?.Objects != null)
                    {
                        RhinoObject ro = RhinoDoc.ActiveDoc.Objects.Find(p.ReferenceID);
                        double key = ro.getKey();
                        if(!double.IsNaN(key)) { p.Value.setKey(key); }
                    }
                }
                double read = p.Value.getKey();
                if (double.IsNaN(read)) { continue; }
                if(read < minK) { minK = read; }
                if (read > maxK) { maxK = read; }
            }

            // restore sanity if no values were found.
            if (double.IsPositiveInfinity(minK)) { minK = 0; }
            if (double.IsNegativeInfinity(maxK)) { maxK = 0; }

            // Add keys to new paths with open paths at the start and closed paths at the end
            foreach (GH_Curve p in paths)
            {
                if(p?.Value == null) { continue; }
                if (double.IsNaN(p.Value.getKey())) { p.Value.setKey(p.Value.IsClosed ? maxK++ : minK--); }
            }

            // Store keys and put the values into the referenced objects if they exist.
            this._curves = new List<Curve>();
            foreach(GH_Curve p in paths)
            {
                if(p == null) { continue; }
                if (p.IsReferencedGeometry)
                {
                    if (RhinoDoc.ActiveDoc?.Objects != null)
                    {
                        RhinoObject ro = RhinoDoc.ActiveDoc.Objects.Find(p.ReferenceID);
                        ro?.setKey(p.Value.getKey());
                    }
                }
                this._curves.Add(p.Value);
            }

            this._curves.Sort(_CurveC);

            // Store the processed data
            if (this.Params?.Input?[0] != null && this.Params.Input[0].SourceCount == 0)
            {
                this._latestPaths = paths;
            }
            da.SetDataList(0, this._curves);
        }

        private class CurveComp : IComparer<Curve>
        {
            public int Compare(Curve x, Curve y)
            {
                return x.getKey().CompareTo(y.getKey());
            }
        }

        private static readonly CurveComp _CurveC = new CurveComp();

        //Return a BoundingBox that contains all the geometry you are about to draw.
        public override BoundingBox ClippingBox => BoundingBox.Empty;

        //Draw all meshes in this method.
        public override void DrawViewportMeshes([CanBeNull] IGH_PreviewArgs args) { }

        //Draw all wires and points in this method.
        public override void DrawViewportWires([CanBeNull] IGH_PreviewArgs args)
        {
            if (  args?.Viewport == null) { return; }
            base.DrawViewportWires(args);

            if (!this._enabled || args.Viewport == null || args.Display ==null) { return; }

            for (int i = 0; i < this._curves.Count; i++)
            {
                if (this._curves[i] == null) { continue;}
                args.Viewport.GetWorldToScreenScale(this._curves[i].PointAtStart, out double pixelsPerUnit);

                System.Drawing.Color lineC = args.WireColour;
                if (this.Attributes != null && this.Attributes.Selected) { lineC = args.WireColour_Selected; }
                args.Display.DrawCurve(this._curves[i], lineC);

                args.Display.DrawDot(this._curves[i].PointAtStart + _DotSize / pixelsPerUnit * this._dotShift, (i + 1).ToString());

                Line dir = new Line(this._curves[i].PointAtStart, this._curves[i].TangentAtStart * 50.0 / pixelsPerUnit);
                args.Display.DrawArrow(dir, System.Drawing.Color.AntiqueWhite);
            }
        }

        public override bool Write([CanBeNull] GH_IWriter writer)
        {
            // If we were handling persistent data (no connecting wires) update it so it has the latest values
            Param_Curve p = this.Params?.Input?[0] as Param_Curve;

            if (p?.PersistentData == null || this.Params.Input[0].SourceCount != 0) { return base.Write(writer); }

            p.PersistentData.ClearData();
            p.SetPersistentData( this._latestPaths);
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
        protected override System.Drawing.Bitmap Icon => Properties.Resources.create2axis;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{DC9920CF-7C48-4A75-B279-89A0C132E564}");
    }
}