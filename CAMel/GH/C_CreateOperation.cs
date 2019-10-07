using System;
using System.Collections.Generic;
using System.Linq;
using CAMel.Types;
using Grasshopper.Kernel;
using JetBrains.Annotations;

namespace CAMel.GH
{
    [UsedImplicitly]
    public class C_CreateOperation : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the CreateToolPath class.
        /// </summary>
        public C_CreateOperation()
            : base("Create Operation ", "Operation",
                "Create an operation from a list of toolpaths",
                "CAMel", " ToolPaths") { }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddTextParameter("Name", "N", "Name of operation", GH_ParamAccess.item, string.Empty);
            pManager.AddGenericParameter("Toolpaths", "TP", "The list of toolpaths to use for the operation.\nWill attempt to process various reasonable collections.", GH_ParamAccess.list);
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_MachineOperationPar(), "Machine Operation", "MO", "A machine operation.", GH_ParamAccess.item);
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }

            List<object> tPs = new List<object>();
            string name = string.Empty;

            if (!da.GetData(0, ref name)) { return; }
            if (!da.GetDataList(1, tPs)) { return; }

            List<MachineOperation> mOs = MachineOperation.toOperations(CAMel_Goo.cleanGooList(tPs), out int ignores);
            if (mOs.Count > 0)
            {
                if (ignores > 1)
                { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "A total of " + ignores + " invalid elements (probably nulls) were ignored."); }
                else if (ignores == 1)
                { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "An invalid element (probably a null) was ignored."); }
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input parameter MO failed to collect usable Machine Operations");
                return;
            }
            List<GH_MachineOperation> ghMOs = mOs.Select(mO => new GH_MachineOperation(mO)).ToList();

            da.SetDataList(0, ghMOs);
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.createoperations;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{D72DFE0B-7D61-4130-B564-6EEE2A8BDA99}");
    }
}