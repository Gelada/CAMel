using System;
using System.Collections.Generic;

using Rhino.Geometry;

using Grasshopper.Kernel;

using CAMel.Types;
using CAMel.Types.Machine;

namespace CAMel
{
    public class C_PocketNC : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_PocketNC()
            : base("PocketNC Machine", "PocketNC",
                "PocketNC 5 Axis Machine",
                "CAMel", " Hardware")
        {
        }

        // put this item in the second batch (Machines)
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Version", "V", "Machine version, 0 (V1 old spindle), 1 (V1 new spindle) or 2", GH_ParamAccess.item, 2);
            pManager.AddNumberParameter("B-table offset", "Bt", "Distance from B-table to centre of A axis rotation.", GH_ParamAccess.item, 0.836);
            pManager.AddNumberParameter("B maximum", "Bmax", "Maximum value (+ or -) that the B axis can take. This can be adjusted in the machine settings.", GH_ParamAccess.item, 9999);
            pManager.AddBooleanParameter("Tool Compensation", "TC", "Use the machine's tool length compensation?", GH_ParamAccess.item, true);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material Tools", "MTs", "Material Tool pairs used by the machine", GH_ParamAccess.list);
            pManager[4].WireDisplay = GH_ParamWireDisplay.faint;
            pManager[4].Optional = true;
            pManager.AddTextParameter("Header", "H", "Code Header", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Footer", "F", "Code Footer", GH_ParamAccess.item, string.Empty);
            pManager.AddNumberParameter("Path Jump", "PJ", "Maximum allowed distance between paths in material", GH_ParamAccess.item, 0);
            
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_MachinePar(), "Machine", "M", "Details for a PocketNC 5-axis machine", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            string head = string.Empty;
            string foot = string.Empty;
            double pj = 0;
            double bT = 0;
            double bMax = 0;
            bool tLc = true;
            int v = 0;
            List<MaterialTool> mTs = new List<MaterialTool>();

            if (!da.GetData(0, ref v)) { return; }
            if (v != 0 && v != 1 && v != 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Only two version of the PocketNC known. Use 0 for old spindle.");
                return;
            }
            if (v != 2) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "All testing done on a V2 machine, please be careful."); }

            if (!da.GetData(1, ref bT)) { return; }
            if (!da.GetData(2, ref bMax)) { return; }
            if (!da.GetData(3, ref tLc)) { return; }
            da.GetDataList(4, mTs);
            if (!da.GetData(5, ref head)) { return; }
            if (!da.GetData(6, ref foot)) { return; }
            if (!da.GetData(7, ref pj)) { return; }

            double aMin = 0, aMax = Math.PI / 2.0;

            string version;
            switch (v)
            {
                case 0:
                    version = "PocketNC V1 (old spindle)";
                    aMin = -5 * Math.PI / 180.0;
                    aMax = 95 * Math.PI / 180.0;
                    break;
                case 1:
                    version = "PocketNC V1 (new spindle)";
                    aMin = -5 * Math.PI / 180.0;
                    aMax = 95 * Math.PI / 180.0;
                    break;
                case 2:
                    version = "PocketNC V2";
                    aMin = -25 * Math.PI / 180.0;
                    aMax = 135 * Math.PI / 180.0;
                    break;
                default:
                    version = "Unknown";
                    break;
            }

            Vector3d pivot = new Vector3d();
            string uFoot = foot;

            if (tLc)
            {
                switch (v)
                {
                    case 0: pivot = new Vector3d(0, 0, 0); break;
                    case 1: pivot = new Vector3d(0, 0, 0); break;
                    case 2: pivot = new Vector3d(0, 0, 0); break;
                }
                uFoot = "G49 (Clear tool length compensation)\n" + foot;
            }
            else
            {
                switch (v)
                {
                    case 0: pivot = new Vector3d(0, 0, 3.6); break;
                    case 1: pivot = new Vector3d(0, 0, 3.0 - bT); break;
                    case 2: pivot = new Vector3d(0, 0, 3.0 - bT); break;
                }
            }

            PocketNC m = new PocketNC(version, head, uFoot, pivot, aMin, aMax, bMax, tLc, pj, mTs);
 
            da.SetData(0, new GH_Machine(m));
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
                return Properties.Resources.create5axis;
            }
        }

       

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{A6E20644-AA34-4400-B87E-EEBA8BDF3720}"); }
        }
    }
}