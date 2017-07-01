using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types;

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
            pManager.AddTextParameter("Name", "N", "name", GH_ParamAccess.item,"");
            pManager.AddGenericParameter("Operations", "MO", "Machine Operations to apply\n A list of toolpaths will be packaged into a single operation.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Machine", "M", "Machine", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Instructions", "I", "Machine Instructions", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<MachineOperation> MO = new List<MachineOperation>();
            List<IToolPointContainer> tempMO = new List<IToolPointContainer>();

            Machine M = new Machine();
            string name = "";

            if (!DA.GetData(0, ref name)) return;
            if (!DA.GetDataList(1, tempMO)) return;
            if (!DA.GetData(2, ref M)) return;

            // scan to find types

            Boolean hasTP = false, hasMO = false;

            foreach (IToolPointContainer tpc in tempMO)
                if (tpc != null)
                {
                    switch (tpc.TypeName)
                    {
                        case "MachineOperation":
                            hasMO = true;
                            break;
                        case "ToolPath":
                            hasTP = true;
                            break;
                        default:
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Objects other than Machine Operations and ToolPaths are ignored.");
                            break;
                    }
                }
            MachineOperation op = new MachineOperation();

            int Invalids = 0;

            if (!hasMO && hasTP) // Process a list of ToolPaths into a Machine Operation
            {
                foreach (IToolPointContainer tpc in tempMO)
                {
                    if (tpc == null)
                        Invalids++;
                    else if (tpc.TypeName == "ToolPath") op.TPs.Add((ToolPath)tpc);
                }
                if (op.TPs.Count > 0) MO.Add(op);
            }
            else if (hasTP || hasMO) // Mix Machine operations and toolpaths each turned into their own operation. 
            {
                foreach (IToolPointContainer tpc in tempMO)
                {
                    if (tpc == null)
                        Invalids++;
                    else
                    {
                        switch (tpc.TypeName)
                        {
                            case "MachineOperation":
                                MO.Add((MachineOperation)tpc);
                                break;
                            case "ToolPath":
                                MO.Add(new MachineOperation());
                                MO[MO.Count - 1].TPs.Add((ToolPath)tpc);
                                break;
                            default:
                                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Objects other than Machine Operations and ToolPaths are ignored.");
                                break;
                        }
                    }
                }
            }

            MachineInstruction Inst = null;

            if (MO.Count > 0)
            {
                Inst = new MachineInstruction(name, M);
                Inst.MOs = MO;
                if (Invalids > 1) 
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "A total of "+Invalids.ToString()+" invalid elements (probably nulls) were ignored.");
                else if (Invalids > 0)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "An invalid element (probably a null) was ignored.");
            }
            else AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input paramter MO failed to collect usable data");
            DA.SetData(0, Inst);
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