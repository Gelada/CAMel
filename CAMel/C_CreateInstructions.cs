using System;
using System.Collections.Generic;

using Grasshopper.Kernel;

using CAMel.Types;
using CAMel.Types.Machine;

namespace CAMel
{
    public class C_CreateInstructions : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateInstructions class.
        /// </summary>
        public C_CreateInstructions()
            : base("Create Instructions", "Instructions",
                "Create machine instructions from a list of machine operations, or tool paths and a machine",
                "CAMel", " ToolPaths")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "name", GH_ParamAccess.item,string.Empty);
            pManager.AddGenericParameter("Operations", "MO", "Machine Operations to apply\n Will attempt to process any reasonable collection.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Start Point", "SP", "Starting moves, can gather data from all sorts of scraps that imply a point. Will use (0,0,1) for direction when Points are used alone.", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddGenericParameter("End Point", "EP", "Ending moves, can gather data from all sorts of scraps that imply a point. Will use (0,0,1) for direction when Points are used alone.", GH_ParamAccess.list);
            pManager[3].Optional = true;
            pManager.AddParameter(new GH_MachinePar(), "Machine", "M", "Machine", GH_ParamAccess.item);
            pManager[4].WireDisplay = GH_ParamWireDisplay.faint;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_MachineInstructionPar(),"Instructions", "I", "Machine Instructions", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<MachineOperation> MO = new List<MachineOperation>();
            List<Object> tempMO = new List<Object>();
            List<Object> sP = new List<Object>();
            List<Object> eP = new List<Object>();

            IMachine M = null;
            string name = string.Empty;

            if (!DA.GetData(0, ref name)) { return; }
            if (!DA.GetDataList(1, tempMO)) { return; }
            DA.GetDataList(2, sP);
            DA.GetDataList(3, eP);
            if (!DA.GetData(4, ref M)) { return; }

            int ignores = 0;
            MO = MachineOperation.toOperations(CAMel_Goo.cleanGooList(tempMO), out ignores);

            MachineInstruction Inst = null;

            if (MO.Count > 0)
            {
                object cleanSP = CAMel_Goo.cleanGooList((object)sP);
                object cleanEP = CAMel_Goo.cleanGooList((object)eP);
                Inst = new MachineInstruction(name, M, MO, ToolPath.toPath(cleanSP), ToolPath.toPath(cleanEP));
                if (ignores > 1)
                { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "A total of " + ignores.ToString() + " invalid elements (probably nulls) were ignored."); }
                else if (ignores == 1)
                { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "An invalid element (probably a null) was ignored."); }
            }
            else
            { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input paramter MO failed to collect usable Machine Operations"); }
            
            DA.SetData(0, new GH_MachineInstruction(Inst));
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
                return Properties.Resources.createinstructions;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{B14610C2-E090-49B2-BAA5-ED329562E9B2}"); }
        }
    }
}