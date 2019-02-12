// Heavily influenced by the clickable preview from https://github.com/mazhuravlev/grasshopper-addons

using System.Windows.Forms;

using Rhino.Geometry;
using Rhino.UI;

namespace CAMel
{
    public class PathClick : MouseCallback
    {
        private readonly C_OrganisePaths _op;

        public PathClick(C_OrganisePaths op)
        {
            this._op = op;
            this.Enabled = true;
        }

        protected override void OnMouseDown(MouseCallbackEventArgs e)
        {
            if (e.Button == MouseButtons.Left && this._op != null && this._op.clickQ())
            {
                Line mouseLine = e.View.ActiveViewport.ClientToWorld(e.ViewportPoint);
                if (this._op.found(mouseLine, e.View.ActiveViewport))
                {
                    e.Cancel = true;
                    this._op.ExpireSolution(true);
                }
            }
            else
            {
                base.OnMouseDown(e);
            }
        }
    }
}