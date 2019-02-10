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
            _op = op;
            Enabled = true;
        }

        protected override void OnMouseDown(MouseCallbackEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _op != null && _op.clickQ())
            {
                Line mouseLine = e.View.ActiveViewport.ClientToWorld(e.ViewportPoint);
                if (_op.found(mouseLine, e.View.ActiveViewport))
                {
                    e.Cancel = true;
                    _op.ExpireSolution(true);
                }
            }
            else
            {
                base.OnMouseDown(e);
            }
        }
    }
}