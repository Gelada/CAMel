using System;
using System.Collections.Generic;
using System.Drawing;

using Rhino.Geometry;

using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace CAMel
{
    public class graphicsTest : GH_Component
    {
       public bool run = true;
        public bool hasRun;

        public graphicsTest() //: base("Curve Tangents", "CT", "Create a bunch of curve tangent lines", "CAMel", "Utilities")
            : base("Animator", "Animator",
              "Animates the paths of G-Code input to this component.",
              "CAMel", "Utilities")
        {

            SampleCount = 10;
            run = true;
            hasRun = false;
        }
        public override void CreateAttributes()
        {
            m_attributes = new CustomAttributes(this);
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curve", "C", "Curve to divide", GH_ParamAccess.item);
            pManager.AddNumberParameter("Length", "L", "Length of tangent lines", GH_ParamAccess.item, 10.0);
            //    pManager.AddGenericParameter("Machine Instruction", "MI", "placeholder", GH_ParamAccess.item);
            //   pManager.AddIntegerParameter("Step Number", "SN", "The iteration step of that machining animation to view", GH_ParamAccess.item, 0);
            //  pManager.AddIntegerParameter("Path Division", "PD", "The number of divisions to seperate the path into for the animation.", GH_ParamAccess.item, 1000);
        }
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("Tangents", "T", "Tangent lines", GH_ParamAccess.list);
            // pManager.AddMeshParameter("Material Shape", "MS", "The shape of the material as it is cut", GH_ParamAccess.item);
            // pManager.AddMeshParameter("ToolShape", "TS", "The mesh that will form the shape of the tool.", GH_ParamAccess.item);
        }
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (run == true)
            {
                Curve curve = null;
                double length = 0.0;

                if (!DA.GetData(0, ref curve)) return;
                if (!DA.GetData(1, ref length)) return;

                double[] ts = curve.DivideByCount(SampleCount, true);
                List<Line> lines = new List<Line>(ts.Length);
                foreach (double t in ts)
                {
                    Line line = new Line(curve.PointAt(t), length * curve.TangentAt(t));
                    lines.Add(line);
                }
                DA.SetDataList(0, lines);


                run = false;
                hasRun = true;
            }
            else
                hasRun = false;
        }
        public int SampleCount { get; set; }

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            writer.SetInt32("SampleCount", SampleCount);
            return base.Write(writer);
        }
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            SampleCount = Math.Max(1, reader.GetInt32("SampleCount"));
            return base.Read(reader);
        }
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{f0730111-4087-4f2d-a19c-b53b6546e2ae}"); }
        }
    }

    public class CustomAttributes : GH_ComponentAttributes
    {
        public CustomAttributes(graphicsTest owner) : base(owner) { }

        #region Custom layout logic
        private RectangleF PlayBounds { get; set; }
        private RectangleF SamplesSomeBounds { get; set; }
        //          private RectangleF SamplesManyBounds { get; set; }

        protected override void Layout()
        {
            base.Layout();
            //We'll extend the basic layout by adding three regions to the bottom of this component,
            PlayBounds = new RectangleF(Bounds.X, Bounds.Bottom, Bounds.Width, 20);
            SamplesSomeBounds = new RectangleF(Bounds.X, Bounds.Bottom + 20, Bounds.Width, 20);
            //              SamplesManyBounds = new RectangleF(Bounds.X, Bounds.Bottom + 40, Bounds.Width, 20);
            Bounds = new RectangleF(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height + 20);
        }
        #endregion

        #region Custom Mouse handling
        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                graphicsTest comp = Owner as graphicsTest;

                if (PlayBounds.Contains(e.CanvasLocation))
                {
                    //if (comp.SampleCount == 10) return GH_ObjectResponse.Handled;
                    comp.RecordUndoEvent("Play");
                    comp.SampleCount = 10;
                    comp.run = true;
                    comp.ExpireSolution(true);
                    //        comp.hasRun = false;
                    return GH_ObjectResponse.Handled;
                }

              if (SamplesSomeBounds.Contains(e.CanvasLocation))
                {
                    if (comp.SampleCount == 100) return GH_ObjectResponse.Handled;
                    comp.RecordUndoEvent("Some Samples");
                    comp.SampleCount = 100;
                    comp.ExpireSolution(true);
                    return GH_ObjectResponse.Handled;
                }

                //                  if (SamplesManyBounds.Contains(e.CanvasLocation))
                //               {
                //                  if (comp.SampleCount == 1000) return GH_ObjectResponse.Handled;
                //                   comp.RecordUndoEvent("Many Samples");
                //                 comp.SampleCount = 1000;
                //               comp.ExpireSolution(true);
                //             return GH_ObjectResponse.Handled;
                //       }
            }
            return base.RespondToMouseDown(sender, e);
        }
        #endregion

        #region Custom Render logic
        protected override void Render(GH_Canvas canvas, System.Drawing.Graphics graphics, GH_CanvasChannel channel)
        {
            switch (channel)
            {
                case GH_CanvasChannel.Objects:
                    //We need to draw everything outselves.
                    base.RenderComponentCapsule(canvas, graphics, true, false, false, true, true, true);

                    graphicsTest comp = Owner as graphicsTest;

                    var Center = new PointF(Bounds.X+25, Bounds.Bottom-30);
                    DrawRadioButton(graphics, Center, comp.hasRun);

                    GH_Capsule buttonFew = GH_Capsule.CreateCapsule(PlayBounds, comp.hasRun == true ? GH_Palette.Blue : GH_Palette.Pink);
                    buttonFew.Render(graphics, this.Selected, Owner.Locked, Owner.Hidden);
                    buttonFew.Dispose();

                    GH_Capsule buttonSome = GH_Capsule.CreateCapsule(SamplesSomeBounds, comp.SampleCount == 100 ? GH_Palette.Black : GH_Palette.White);
                    buttonSome.Render(graphics, this.Selected, Owner.Locked, Owner.Hidden);
                    buttonSome.Dispose();

                    //         GH_Capsule buttonMany = GH_Capsule.CreateCapsule(SamplesManyBounds, comp.SampleCount == 1000 ? GH_Palette.Black : GH_Palette.White);
                    //       buttonMany.Render(graphics, this.Selected, Owner.Locked, Owner.Hidden);
                    //     buttonMany.Dispose();

                    graphics.DrawString("▶", GH_FontServer.Standard, Brushes.Black, PlayBounds, GH_TextRenderingConstants.CenterCenter);
                    graphics.DrawString("Some", GH_FontServer.Standard, comp.SampleCount == 100 ? Brushes.White : Brushes.Black, SamplesSomeBounds, GH_TextRenderingConstants.CenterCenter);
                    //       graphics.DrawString("Many", GH_FontServer.Standard, comp.SampleCount == 1000 ? Brushes.White : Brushes.Black, SamplesManyBounds, GH_TextRenderingConstants.CenterCenter);

                    break;
                default:
                    base.Render(canvas, graphics, channel);
                    break;
            }
        }
        #endregion


        private void DrawRadioButton(Graphics graphics, PointF center, bool check)
        {
            if (check){
                graphics.FillEllipse(Brushes.Black, center.X - 6, center.Y - 6, 12, 12);
            }
            else
            {
                graphics.FillEllipse(Brushes.Black, center.X - 6, center.Y - 6, 12, 12);
                graphics.FillEllipse(Brushes.White, center.X - 4, center.Y - 4, 8, 8);
            }
        }
    }

}