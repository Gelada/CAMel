using System;
using System.Collections.Generic;
using System.Drawing;

using Rhino.Geometry;

using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace CAMel
{
    public class MyComponent1 : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public MyComponent1()
          : base("MyComponent1", "Nickname",
              "Description",
              "Category", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh set 1", "M1", "meshs 1", GH_ParamAccess.list);
            pManager.AddMeshParameter("Mesh set 2", "M2", "meshs 2", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("output", "o", "output", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Mesh> m1 = new List<Mesh>();
            List<Mesh> m2 = new List<Mesh>();
            if (!DA.GetDataList(0, m1)) return;
            if (!DA.GetDataList(1, m2)) return;

            Mesh[] returnMesh = Mesh.CreateBooleanDifference(m1, m2);
            List<Mesh> returnM = new List<Mesh>();
            foreach(Mesh m in returnMesh){
                returnM.Add(m);
            }
            DA.SetDataList(0, returnM);
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
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{d415d360-d9ab-457e-ad5a-eda5c209c57b}"); }
        }
    }
}