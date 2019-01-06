using System;
using System.Collections.Generic;

using Grasshopper.Kernel;

using CAMel.Types;
using CAMel.Types.Machine;

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
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tool List", "L", "List of Material Tool Details or Machine with list of Material Tools", GH_ParamAccess.list);
            pManager.AddTextParameter("Material", "M", "Material to cut", GH_ParamAccess.item,"");
            pManager.AddTextParameter("Tool", "T", "Tool to use", GH_ParamAccess.item,"");
        }
                

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_MaterialToolPar(),"MaterialTools", "MT", "Correct from the .csv file", GH_ParamAccess.item);
        }

        private void vlUpdate(Grasshopper.Kernel.Special.GH_ValueList vL, ref SortedSet<string> items)
        {
            string selected = String.Empty;
            bool newList = false;
            foreach (Grasshopper.Kernel.Special.GH_ValueListItem vLi in vL.ListItems)
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
                    var vLi = new Grasshopper.Kernel.Special.GH_ValueListItem(mat, "\"" + mat + "\"");
                    if (mat == selected) { vLi.Selected = true; }
                    vL.ListItems.Add(vLi);
                }
                vL.ExpireSolution(true);
            }
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string matName = string.Empty;
            string toolName = string.Empty;
            List<object> oMTs = new List<object>();

            if (!DA.GetDataList(0, oMTs)) { return; }
            DA.GetData(1, ref matName);
            DA.GetData(2, ref toolName);

            oMTs = (List<object>)CAMel_Goo.cleanGooList((object)oMTs);

            var readMTs = new HashSet<MaterialTool>();

            foreach(object ob in oMTs)
            {
                if(ob is MaterialTool) { readMTs.Add((MaterialTool)ob); }
                if(ob is IMachine) { readMTs.UnionWith(((IMachine)ob).MTs); }
            }

            var materials = new SortedSet<string>();
            var tools = new SortedSet<string>();

            bool found = false;
            MaterialTool MT = null;
            foreach (MaterialTool iMT in readMTs)
            {
                materials.Add(iMT.matName);
                tools.Add(iMT.toolName);

                if (iMT.toolName == toolName && iMT.matName == matName)
                {
                    if (found)
                    { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "More than one material tool combination found, using first."); }
                    else { found = true; MT = iMT; }
                }
            }

            // Want to populate with new options, but not lose old data. 
            foreach (IGH_Param source in this.Params.Input[1].Sources)
            {
                if (source is Grasshopper.Kernel.Special.GH_ValueList)
                { vlUpdate((Grasshopper.Kernel.Special.GH_ValueList)source, ref materials); }
            }
            foreach (IGH_Param source in this.Params.Input[2].Sources)
            {
                if (source is Grasshopper.Kernel.Special.GH_ValueList)
                { vlUpdate((Grasshopper.Kernel.Special.GH_ValueList)source, ref tools); }
            }

            if (!found) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No material tool combination found."); }
            else { DA.SetData(0, new GH_MaterialTool(MT)); }
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
                return Properties.Resources.readmaterialtool;
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