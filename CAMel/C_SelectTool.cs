using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;

using CAMel.Types;
using CAMel.Types.Machine;
using Grasshopper.Kernel.Special;

namespace CAMel
{

    public class C_SelectTool : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_SelectTool()
            : base("Select Tool", "FindMT",
                "Find a Material Tool from a list",
                "CAMel", " Hardware")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tool List", "L", "List of Material Tool Details or Machine with list of Material Tools", GH_ParamAccess.list);
            pManager.AddTextParameter("Material", "M", "Material to cut", GH_ParamAccess.item,"");
            pManager.AddTextParameter("Tool", "T", "Tool to use", GH_ParamAccess.item,"");
        }


        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_MaterialToolPar(),"MaterialTools", "MT", "Correct from the .csv file", GH_ParamAccess.item);
        }

        private void vlUpdate(GH_ValueList vL, ref SortedSet<string> items)
        {
            string selected = String.Empty;
            bool newList = false;
            foreach (GH_ValueListItem vLi in vL.ListItems)
            {
                if (vLi.Name != "One" && vLi.Name != "Two" && vLi.Name != "Three" && vLi.Name != "Four")
                { items.Add(vLi.Name); }
                else { newList = true; }
                if(vLi.Selected) { selected = vLi.Name; }
            }

            if (newList || vL.ListItems.Count != items.Count)
            {
                vL.ListItems.Clear();
                foreach (string mat in items)
                {
                    var vLi = new GH_ValueListItem(mat, "\"" + mat + "\"");
                    if (mat == selected) { vLi.Selected = true; }
                    vL.ListItems.Add(vLi);
                }
                vL.ExpireSolution(true);
            }
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            string matName = string.Empty;
            string toolName = string.Empty;
            List<object> oMTs = new List<object>();

            if (!da.GetDataList(0, oMTs)) { return; }
            da.GetData(1, ref matName);
            da.GetData(2, ref toolName);

            oMTs = (List<object>)CAMel_Goo.cleanGooList(oMTs);

            var readMTs = new HashSet<MaterialTool>();

            foreach(object ob in oMTs)
            {
                if(ob is MaterialTool tool) { readMTs.Add(tool); }
                if(ob is IMachine machine) { readMTs.UnionWith(machine.mTs); }
            }

            SortedSet<string> materials = new SortedSet<string>();
            SortedSet<string> tools = new SortedSet<string>();

            bool found = false;
            MaterialTool mT = null;
            foreach (MaterialTool imT in readMTs)
            {
                materials.Add(imT.matName);
                tools.Add(imT.toolName);

                if (imT.toolName == toolName && imT.matName == matName)
                {
                    if (found)
                    { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "More than one material tool combination found, using first."); }
                    else { found = true; mT = imT; }
                }
            }

            // Want to populate with new options, but not lose old data.
            foreach (GH_ValueList source in this.Params.Input[1].Sources.OfType<GH_ValueList>())
            { vlUpdate(source, ref materials); }

            foreach (GH_ValueList source in this.Params.Input[2].Sources.OfType<GH_ValueList>())
            { vlUpdate(source, ref tools); }

            if (!found) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No material tool combination found."); }
            else { da.SetData(0, new GH_MaterialTool(mT)); }
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
                return Properties.Resources.selectmaterialtool;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{0508C572-7DBC-40A9-A563-91DFFF9C3AF5}"); }
        }
    }
}