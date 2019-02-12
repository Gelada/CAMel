// Heavily influenced by the clickable preview from https://github.com/mazhuravlev/grasshopper-addons

using System;
using System.Collections.Generic;

using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Display;
using Rhino.UI;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using GH_IO.Serialization;

using CAMel.Types;

namespace CAMel
{
    public class C_OrganisePaths : GH_Component, IGH_PreviewObject
    {
        internal bool InActiveDocument;
        private bool Enabled;

        internal bool clickQ() => Enabled && InActiveDocument;

        private PathClick _click;
        private GH_Document _doc;

        private List<Curve> _curves;
        private List<GH_Curve> _latestPaths;

        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_OrganisePaths() : base("OrganisePaths", "OrgPth", "Reorder a collection of curves", "CAMel", "Test")
        {
            _click = new PathClick(this);
            _curves = new List<Curve>();
            _latestPaths = new List<GH_Curve>();
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Paths", "P", "Paths to reorder", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Reordered", "R", "Reordered Paths", GH_ParamAccess.list);
        }

        protected override void BeforeSolveInstance()
        {
            _doc = OnPingDocument();
            InActiveDocument = Instances.ActiveCanvas.Document == _doc && Instances.ActiveCanvas.Document.Context == GH_DocumentContext.Loaded;

            Instances.ActiveCanvas.Document.ContextChanged -= ContextChanged;
            Instances.ActiveCanvas.Document.ContextChanged += ContextChanged;
            base.BeforeSolveInstance();
        }

        private void ContextChanged(object sender, GH_DocContextEventArgs e)
        {
            InActiveDocument = e.Document == _doc && e.Context == GH_DocumentContext.Loaded;
        }

        private const int dotSize = 11;
        private readonly Vector3d dotShift = new Vector3d(1, 1, 1);
        internal bool found(Line l, RhinoViewport vP)
        {
            double pixelsPerUnit;
            for(int i = 0;i<_curves.Count; i++) 
            {
                Curve c = _curves[i];
                vP.GetWorldToScreenScale(c.PointAtStart, out pixelsPerUnit);
                if(l.DistanceTo(c.PointAtStart+ dotSize / pixelsPerUnit * dotShift, false)*pixelsPerUnit < dotSize)
                {
                    string newP = string.Empty;
                    Dialogs.ShowEditBox("Reposition", "New position", (i+1).ToString(), false, out newP);
                    int newPos;
                    if (int.TryParse(newP, out newPos))
                    {
                        double newKey = 0;
                        if (newPos <= 1)
                        {
                            newPos = 1;
                            newKey = _curves[0].getKey() - 1.0;
                        }
                        else if (newPos >= _curves.Count)
                        {
                            newPos = _curves.Count;
                            newKey = _curves[_curves.Count-1].getKey() + 1.0;
                        }
                        else
                        {
                            newKey = (_curves[newPos - 2].getKey() + _curves[newPos - 1].getKey())/2.0;
                        }
                        c.setKey(newKey);
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            this.Enabled = true;
            var paths = new List<GH_Curve>();
            if (!DA.GetDataList("Paths", paths))
            {
                this.Enabled = false;
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
                    RhinoObject ro = RhinoDoc.ActiveDoc.Objects.Find(p.ReferenceID);
                    double key = ro.getKey();
                    if(!double.IsNaN(key)) { p.Value.setKey(key); }
                }
                double read = p.Value.getKey();
                if(!double.IsNaN(read))
                {
                    if(read < minK) { minK = read; }
                    if (read > maxK) { maxK = read; }
                }
            }

            // restore sanityif no values were found.
            if (Double.IsPositiveInfinity(minK)) { minK = 0; }
            if (Double.IsNegativeInfinity(maxK)) { maxK = 0; }

            // Add keys to new paths with open paths at the start and closed paths at the end
            foreach (GH_Curve p in paths)
            {
                if(p == null) { continue; }
                if (double.IsNaN(p.Value.getKey()))
                {
                    if (p.Value.IsClosed) { p.Value.setKey(maxK++); }
                    else { p.Value.setKey(minK--); }
                }
            }
            
            // Store keys and put the values into the referenced objects if they exist. 
            _curves = new List<Curve>();
            foreach(GH_Curve p in paths)
            {
                if(p == null) { continue; }
                if (p.IsReferencedGeometry)
                {
                    RhinoObject ro = RhinoDoc.ActiveDoc.Objects.Find(p.ReferenceID);
                    if (ro != null) { ro.setKey(p.Value.getKey()); }
                } 
                _curves.Add(p.Value);
            }

            _curves.Sort(CurveC);

            // Store the processed data
            if (this.Params.Input[0].SourceCount == 0)
            {
                this._latestPaths = paths;
            }
            DA.SetDataList(0, _curves);
        }

        class CurveComp : IComparer<Curve>
        {
            public int Compare(Curve x, Curve y)
            {
                return x.getKey().CompareTo(y.getKey());
            }
        }

        static CurveComp CurveC = new CurveComp();

        //Return a BoundingBox that contains all the geometry you are about to draw.
        public override BoundingBox ClippingBox
        {
            get
            {
                return BoundingBox.Empty;
            }
        }

        //Draw all meshes in this method.
        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
        }

        //Draw all wires and points in this method.
        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            base.DrawViewportWires(args);
            if (this.Enabled)
            {
                double pixelsPerUnit;

                for (int i = 0; i < _curves.Count; i++)
                {
                    args.Viewport.GetWorldToScreenScale(_curves[i].PointAtStart, out pixelsPerUnit);

                    System.Drawing.Color lineC = args.WireColour;
                    if (this.Attributes.Selected) { lineC = args.WireColour_Selected; }
                    args.Display.DrawCurve(_curves[i], lineC);

                    args.Display.DrawDot(_curves[i].PointAtStart + dotSize / pixelsPerUnit * dotShift, (i + 1).ToString());

                    Line dir = new Line(_curves[i].PointAtStart, _curves[i].TangentAtStart * 50.0 / pixelsPerUnit);
                    args.Display.DrawArrow(dir, System.Drawing.Color.AntiqueWhite);
                }
            }
        }

        public override bool Write(GH_IWriter writer)
        {
            // If we were handling persistent data (no connecting wires) update it so it has the latest values
            if (this.Params.Input[0].SourceCount == 0)
            {
                ((Grasshopper.Kernel.Parameters.Param_Curve)this.Params.Input[0]).PersistentData.ClearData();
                ((Grasshopper.Kernel.Parameters.Param_Curve)this.Params.Input[0]).SetPersistentData( this._latestPaths);
            }
            return base.Write(writer);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            this.Enabled = false;
            ExpirePreview(true);
            base.RemovedFromDocument(document);
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
                return Properties.Resources.create2axis;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{DC9920CF-7C48-4A75-B279-89A0C132E564}"); }
        }
    }
}