using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
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
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            List<double> TPAdef = new List<double>();
            for (int i = 0; i < 7; i++) { TPAdef.Add(0); }
            TPAdef[0] = 1; // Insert
            TPAdef[1] = 1; // Retract
            TPAdef[2] = 1; // Stepdown
            TPAdef[3] = 1; // Drop Start
            TPAdef[4] = 1; // Drop Middle
            TPAdef[5] = 1; // Drop End
            pManager.AddPointParameter("Path", "P", "List of toolpoint locations", GH_ParamAccess.list);
            pManager.AddVectorParameter("Directions", "D", "List of vectors giving tool direction", GH_ParamAccess.list,new Vector3d(0,0,1));
            pManager.AddVectorParameter("Speed and Feed", "SF", "List of vectors giving speed (X) and feed (Y) at each toolpoint.", GH_ParamAccess.list, new Vector3d(-1, -1, 0));
            pManager.AddTextParameter("pre Code", "prC", "List of additional CNC codes to run before the points. The code will run on the same line. Use a newline at end to run on the previous line.", GH_ParamAccess.list,string.Empty);
            pManager.AddTextParameter("post Code", "poC", "List of additional CNC codes to run after the points. The code will run on the same line. Use a newline at start to run on the next line.", GH_ParamAccess.list, string.Empty);
            pManager.AddTextParameter("Name", "N", "Name of path", GH_ParamAccess.item,string.Empty);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MaterialFormPar(), "Material Form", "MF", "The MaterialForm giving the position of the material", GH_ParamAccess.item);
            pManager.AddParameter(new GH_ToolPathAdditionsPar(), "Additions", "TPA", "Additional operations to apply to the path before cutting. \n" +
                "Left click and choose \"Manage ToolPathAdditions Collection\" to create.", GH_ParamAccess.item);
            pManager.AddTextParameter("Code", "C", "Addition CNC code to run before this path.", GH_ParamAccess.item,string.Empty);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_ToolPathPar(), "ToolPath", "TP", "Complete ToolPath", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> pts = new List<Point3d>();
            List<Vector3d> dirs = new List<Vector3d>();
            List<Vector3d> SF = new List<Vector3d>();
            List<String> preCode = new List<String>();
            List<String> postCode = new List<String>();
            string name = string.Empty;
            MaterialTool MT = null;
            IMaterialForm MF = null;
            string Co = string.Empty;
            ToolPathAdditions TPA = new ToolPathAdditions();

            if (!DA.GetDataList(0, pts)) { return; }
            if (!DA.GetDataList(1, dirs)) { return; }
            if (!DA.GetDataList(2, SF)) { return; }
            if (!DA.GetDataList(3, preCode)) { return; }
            if (!DA.GetDataList(4, postCode)) { return; }
            if (!DA.GetData(5, ref name)) { return; }
            if (!DA.GetData(6, ref MT)) { return; }
            if (!DA.GetData(7, ref MF)) { return; }
            if (!DA.GetData(8, ref TPA)) { return; } 
            if (!DA.GetData(9, ref Co)) { return; }

            ToolPath TP = new ToolPath(name, MT, MF, TPA);

            Vector3d usedir;
            Vector3d useSF;
            String usePreCo, usePostCo;

            if ((dirs.Count == 1 || dirs.Count == pts.Count) &&
                (SF.Count == 1 || SF.Count == pts.Count) &&
                (preCode.Count == 1 || preCode.Count == pts.Count)&&
                (postCode.Count == 1 || postCode.Count == pts.Count))
            {
                for (int i = 0; i < pts.Count; i++)
                {
                    if (dirs.Count == 1) { usedir = dirs[0]; }
                    else { usedir = dirs[i]; }

                    if (SF.Count == 1) { useSF = SF[0]; }
                    else { useSF = SF[i]; }

                    if (preCode.Count == 1) { usePreCo = preCode[0]; }
                    else { usePreCo = preCode[i]; }

                    if (postCode.Count == 1) { usePostCo = postCode[0]; }
                    else { usePostCo = postCode[i]; }

                    TP.Add(new ToolPoint(pts[i], usedir, usePreCo,usePostCo, useSF.X, useSF.Y));
                }
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The lists of directions and speeds/feeds must be a single item or the same length as the list of points.");
            }

            DA.SetData(0, new GH_ToolPath(TP));
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