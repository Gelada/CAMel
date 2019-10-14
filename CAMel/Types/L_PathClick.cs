// Heavily influenced by the click-able preview from https://github.com/mazhuravlev/grasshopper-addons

namespace CAMel.Types
{
    using System;
    using System.Windows.Forms;

    using CAMel.GH;

    using JetBrains.Annotations;

    using Rhino.Geometry;
    using Rhino.UI;

    public class PathClick : MouseCallback
    {
        private readonly C_OrganisePaths op;

        public PathClick([NotNull] C_OrganisePaths op)
        {
            this.op = op;
            this.Enabled = true;
        }

        protected override void OnMouseDown([NotNull] MouseCallbackEventArgs e)
        {
            if (e.View?.ActiveViewport == null) { throw new ArgumentNullException(); }
            if (e.Button == MouseButtons.Left && this.op?.clickQ() == true)
            {
                // Ignore if a  getter is active
                if (Rhino.Input.RhinoGet.InGet(Rhino.RhinoDoc.ActiveDoc)) { return; }

                // Ignore if nothing is found
                Line mouseLine = e.View.ActiveViewport.ClientToWorld(e.ViewportPoint);
                if (!this.op.find(mouseLine, e.View.ActiveViewport)) { return; }

                // Clear the click and expire solution
                e.Cancel = true;
                this.op.changeRefCurves();
                this.op.ExpireSolution(true);
            }
            else
            {
                base.OnMouseDown(e);
            }
        }
    }
}