using System;

using Rhino.Geometry;

using Grasshopper.Kernel;

using CAMel.Types;
using CAMel.Types.MaterialForm;

namespace CAMel
{
    enum SurfaceType
	{
	   Brep, Mesh   
	}
    public class C_Surfacing : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_Surfacing()
            : base("Surfacing Operation", "Surface",
                "Create a Machine Operations to create a surface.",
                "CAMel", " Operations")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Surface", "S","The surface, brep or mesh to carve", GH_ParamAccess.item);
            pManager.AddParameter(new GH_SurfacePathPar(),"Rough Path", "R", "Information to create roughing path", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool Rough", "MTR", "The material to cut and the tool to do it for roughing", GH_ParamAccess.item);
            pManager[2].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddParameter(new GH_SurfacePathPar(), "Finish Path", "F", "Information to create finishing path", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MaterialToolPar(), "Material/Tool Finish", "MTF", "The material to cut and the tool to do it for finishing", GH_ParamAccess.item);
            pManager[4].WireDisplay = GH_ParamWireDisplay.faint;
            pManager.AddParameter(new GH_MaterialFormPar(), "Material Form", "MF", "The MaterialForm giving the position of the material", GH_ParamAccess.item);
            pManager[5].WireDisplay = GH_ParamWireDisplay.faint;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_MachineOperationPar(), "Rough", "R", "Roughing Operation", GH_ParamAccess.item);
            pManager.AddParameter(new GH_MachineOperationPar(), "Finish", "F", "Finishing Operation", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            SurfaceType ST;
            GeometryBase G = null;
            Mesh M = null;
            Brep B = null;
            SurfacePath R = null;
            SurfacePath F = null;
            MaterialTool MTR = null;
            MaterialTool MTF = null;
            IMaterialForm MF = null;

            if (!DA.GetData(0, ref G)) { return; }
            if(G.GetType() == typeof(Mesh))
            {
                ST = SurfaceType.Mesh;
                M = (Mesh)G;
            } else if(G.GetType() == typeof(Brep))
            {
                B = (Brep)G;
                ST = SurfaceType.Brep;
            } else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The surface parameter must be a Brep, Surface or Mesh");
                return;
            }
            if (!DA.GetData(1, ref R)) { return; }
            if (!DA.GetData(2, ref MTR)) { return; }
            if (!DA.GetData(3, ref F)) { return; }
            if (!DA.GetData(4, ref MTF)) { return; }
            if (!DA.GetData(5, ref MF)) { return; }

            MachineOperation Rough = new MachineOperation();
            MachineOperation Finish = new MachineOperation();

            ToolPathAdditions AddRough = new ToolPathAdditions
            {
                // Additions for Roughing toolpath
                insert = true,
                retract = true,
                stepDown = true,
                sdDropStart = true,
                sdDropMiddle = 8 * MF.safeDistance,
                sdDropEnd = true,
                threeAxisHeightOffset = false
            };
            MTR = MaterialTool.changeFinishDepth(MTR, MTR.cutDepth); // ignore finish depth for roughing

            ToolPathAdditions AddFinish = new ToolPathAdditions
            {
                // Additions for Finishing toolpath
                insert = true,
                retract = true,
                stepDown = false,
                sdDropStart = false,
                sdDropMiddle = 0.0,
                sdDropEnd = false,
                threeAxisHeightOffset = false
            };

            switch (ST)
            {
                case SurfaceType.Brep:
                    Rough = R.generateOperation(B, MTF.finishDepth, MTR, MF, AddRough);
                    Finish = F.generateOperation(B, 0.0, MTF, MF, AddFinish);
                    break;
                case SurfaceType.Mesh:
                    Rough = R.generateOperation(M, MTF.finishDepth, MTR, MF, AddRough);
                    Finish = F.generateOperation(M, 0.0, MTF, MF, AddFinish);
                    break;
            }
            Rough.name = "Rough " + Rough.name;
            Finish.name = "Finish " + Finish.name;

            DA.SetData(0, new GH_MachineOperation(Rough));
            DA.SetData(1, new GH_MachineOperation(Finish));
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
                return Properties.Resources.pathsurfacing;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{993EE7E5-6D31-4DFD-B4C4-813098BCB628}"); }
        }
    }
}