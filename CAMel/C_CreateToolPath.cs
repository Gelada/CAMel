using System;
using System.Collections.Generic;

using Rhino.Geometry;

using Grasshopper.Kernel;

using CAMel.Types;
using CAMel.Types.MaterialForm;

namespace CAMel
{
    public class C_CreateToolPath : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateToolPath class.
        /// </summary>
        public C_CreateToolPath()
            : base("Create Tool Path", "ToolPath",
                "Create a toolpath from lists of points and directions (if > 3axis)",
                "CAMel", " ToolPaths")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Path", "P", "List of toolpoint locations", GH_ParamAccess.list);
            pManager.AddVectorParameter("Directions", "D", "List of vectors giving tool direction", GH_ParamAccess.list,new Vector3d(0,0,1));
            pManager.AddVectorParameter("Speed and Feed", "SF", "List of vectors giving speed (X) and feed (Y) at each toolpoint.", GH_ParamAccess.list, new Vector3d(-1, -1, 0));
            pManager.AddTextParameter("pre Code", "prC", "List of additional CNC codes to run before the points. The code will run on the same line. Use a newline at end to run on the previous line.", GH_ParamAccess.list,string.Empty);
            pManager.AddTextParameter("post Code", "poC", "List of additional CNC codes to run after the points. The code will run on the same line. Use a newline at start to run on the next line.", GH_ParamAccess.list, string.Empty);
            pManager.AddTextParameter("Name", "N", "Name of path", GH_ParamAccess.item,string.Empty);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.item);
            pManager[6].Optional = true; // MatTool
            pManager[6].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddParameter(new GH_MaterialFormPar(), "Material Form", "MF", "The MaterialForm giving the position of the material", GH_ParamAccess.item);
            pManager[7].Optional = true; // MatForm
            pManager[7].WireDisplay = GH_ParamWireDisplay.faint;
            var tPApar = new GH_ToolPathAdditionsPar();
            tPApar.SetPersistentData(new GH_ToolPathAdditions(ToolPathAdditions.basicDefault));
            pManager.AddParameter(tPApar, "Additions", "TPA", "Additional operations to apply to the path before cutting. \n" +
                "Left click and choose \"Manage ToolPathAdditions Collection\" to create.", GH_ParamAccess.item);
            pManager[8].Optional = true; // ToolPathAdditions
            pManager.AddTextParameter("Code", "C", "Addition CNC code to run before this path.", GH_ParamAccess.item,string.Empty);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_ToolPathPar(), "ToolPath", "TP", "Complete ToolPath", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            List<Point3d> pts = new List<Point3d>();
            List<Vector3d> dirs = new List<Vector3d>();
            List<Vector3d> sF = new List<Vector3d>();
            List<String> preCode = new List<String>();
            List<String> postCode = new List<String>();
            string name = string.Empty;
            MaterialTool mT = null;
            IMaterialForm mF = null;
            string co = string.Empty;
            ToolPathAdditions tPa = new ToolPathAdditions();

            if (!da.GetDataList(0, pts)) { return; }
            if (!da.GetDataList(1, dirs)) { return; }
            if (!da.GetDataList(2, sF)) { return; }
            if (!da.GetDataList(3, preCode)) { return; }
            if (!da.GetDataList(4, postCode)) { return; }
            if (!da.GetData(5, ref name)) { return; }
            da.GetData(6, ref mT);
            da.GetData(7, ref mF);
            da.GetData(8, ref tPa);
            if (!da.GetData(9, ref co)) { return; }

            ToolPath tP = new ToolPath(name, mT, mF, tPa);

            Vector3d usedir;
            Vector3d useSF;
            String usePreCo, usePostCo;

            if ((dirs.Count == 1 || dirs.Count == pts.Count) &&
                (sF.Count == 1 || sF.Count == pts.Count) &&
                (preCode.Count == 1 || preCode.Count == pts.Count)&&
                (postCode.Count == 1 || postCode.Count == pts.Count))
            {
                for (int i = 0; i < pts.Count; i++)
                {
                    if (dirs.Count == 1) { usedir = dirs[0]; }
                    else { usedir = dirs[i]; }

                    if (sF.Count == 1) { useSF = sF[0]; }
                    else { useSF = sF[i]; }

                    if (preCode.Count == 1) { usePreCo = preCode[0]; }
                    else { usePreCo = preCode[i]; }

                    if (postCode.Count == 1) { usePostCo = postCode[0]; }
                    else { usePostCo = postCode[i]; }

                    tP.Add(new ToolPoint(pts[i], usedir, usePreCo,usePostCo, useSF.X, useSF.Y));
                }
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The lists of directions and speeds/feeds must be a single item or the same length as the list of points.");
            }

            da.SetData(0, new GH_ToolPath(tP));
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
                return Properties.Resources.createtoolpath;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{bd9a76f0-e75a-44e3-ad24-4bc6168ffe8f}"); }
        }
    }
}