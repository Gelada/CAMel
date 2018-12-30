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
            pManager.AddTextParameter("pre Code", "prC", "List of additional CNC codes to run before the points. The code will run on the same line. Use a newline to run on the next line, but this will mess with line numbering.", GH_ParamAccess.list,"");
            pManager.AddTextParameter("post Code", "poC", "List of additional CNC codes to run after the points. The code will run on the same line. Use a newline to run on the next line, but this will mess with line numbering.", GH_ParamAccess.list, "");
            pManager.AddTextParameter("Name", "N", "Name of path", GH_ParamAccess.item,"");
            pManager.AddGenericParameter("Material/Tool", "MT", "The MaterialTool detailing how the tool should move through the material", GH_ParamAccess.item);
            pManager.AddGenericParameter("Material Form", "MF", "The MaterialForm giving the position of the material", GH_ParamAccess.item);
            pManager.AddNumberParameter("Additions", "TPA", "Additional operations to apply to the path, given by a list of numbers:\n"+
                "{Insert, Retract, Stepdown, Drop Start, Drop Middle, Drop End, 3Axis Height Offset}\n" +
                "all but drop middle are boolean with 0 being false.", GH_ParamAccess.list, TPAdef);
            pManager.AddTextParameter("Code", "C", "Addition CNC code to run before this path.", GH_ParamAccess.item,"");

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("ToolPath", "TP", "Complete ToolPath", GH_ParamAccess.item);
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
            string name = "";
            MaterialTool MT = null;
            IMaterialForm MF = null;
            string Co = "";
            List<double> TPAd = new List<double>();

            if (!DA.GetDataList(0, pts)) { return; }
            if (!DA.GetDataList(1, dirs)) { return; }
            if (!DA.GetDataList(2, SF)) { return; }
            if (!DA.GetDataList(3, preCode)) { return; }
            if (!DA.GetDataList(4, postCode)) { return; }
            if (!DA.GetData(5, ref name)) { return; }
            if (!DA.GetData(6, ref MT)) { return; }
            if (!DA.GetData(7, ref MF)) { return; }
            if (!DA.GetDataList(8, TPAd)) { return; } 
            if (!DA.GetData(9, ref Co)) { return; }

            // Process the TPA vector

            ToolPathAdditions TPA = new ToolPathAdditions();

            // Warn if too short or too long.

            if(TPAd.Count > 7)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Tool Path Additions too long, ignoring extra values.");
                TPAd.RemoveRange(7, TPAd.Count - 7);
            }
            if(TPAd.Count < 7)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Tool Path Additions too short, missing values assumed to be false.");
            }

            // extract data for the length available.
            // TODO move this into the toolpath additions class.

            switch (TPAd.Count)
            {
                case 7:
                    if (TPAd[6] > 0) { TPA.threeAxisHeightOffset = true; }
                    else { TPA.threeAxisHeightOffset = false; }
                    goto case 6;
                case 6:
                    if (TPAd[5] > 0) { TPA.sdDropEnd = true; }
                    else { TPA.sdDropEnd = false; }
                    goto case 5;
                case 5:
                    TPA.sdDropMiddle = TPAd[4];
                    goto case 4;
                case 4:
                    if (TPAd[3] > 0) { TPA.sdDropStart = true; }
                    else { TPA.sdDropStart = false; }
                    goto case 3;
                case 3:
                    if (TPAd[2] > 0) { TPA.stepDown = true; }
                    else { TPA.stepDown = false; }
                    goto case 2;
                case 2:
                    if (TPAd[1] > 0) { TPA.retract = true; }
                    else { TPA.retract = false; }
                    goto case 1;
                case 1:
                    if (TPAd[0] > 0) { TPA.insert = true; }
                    else { TPA.insert = false; }
                    break;
            }

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

            DA.SetData(0, TP);
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