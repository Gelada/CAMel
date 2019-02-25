// Heavily influenced by the click-able preview from https://github.com/mazhuravlev/grasshopper-addons

using System;
using System.Windows.Forms;
using CAMel.GH;
using JetBrains.Annotations;
//using Rhino;
using Rhino.Geometry;
//using Rhino.Input;
using Rhino.UI;

namespace CAMel.Types
{
    public class PathClick : MouseCallback
    {
        private readonly C_OrganisePaths _op;

        public PathClick([NotNull] C_OrganisePaths op)
        {
            this._op = op;
            this.Enabled = true;
        }

        protected override void OnMouseDown([NotNull] MouseCallbackEventArgs e)
        {
            if( e.View?.ActiveViewport == null) { throw new ArgumentNullException();}
            if (e.Button == MouseButtons.Left && this._op != null && this._op.clickQ())
            {
                // Ignore if a  getter is active
                if (Rhino.Input.RhinoGet.InGet(Rhino.RhinoDoc.ActiveDoc)) { return; }

                // Ignore if nothing is found
                Line mouseLine = e.View.ActiveViewport.ClientToWorld(e.ViewportPoint);
                if (!this._op.found(mouseLine, e.View.ActiveViewport)) { return; }

                // Clear the click and expire solution
                e.Cancel = true;
                this._op.changeRefCurves();
                this._op.ExpireSolution(true);
            }
            else
            {
                base.OnMouseDown(e);
            }
        }
    }
}