using System;
using System.Collections.Generic;
using CAMel.Types;
using CAMel.Types.Machine;
using Grasshopper.Kernel;
using JetBrains.Annotations;

namespace CAMel.GH
{
    [UsedImplicitly]
    public class C_CreateInstructions : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the CreateInstructions class.
        /// </summary>
        public C_CreateInstructions()
            : base("Create Instructions", "Instructions",
                "Create machine instructions from a list of machine operations, or tool paths and a machine",
                "CAMel", " ToolPaths")
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddTextParameter("Name", "N", "name", GH_ParamAccess.item,string.Empty);
            pManager.AddGenericParameter("Operations", "MO", "Machine Operations to apply\n Will attempt to process any reasonable collection.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Start Point", "SP", "Starting moves, can gather data from all sorts of scraps that imply a point. Will use (0,0,1) for direction when Points are used alone.", GH_ParamAccess.list);
            // ReSharper disable once PossibleNullReferenceException
            pManager[2].Optional = true;
            pManager.AddGenericParameter("End Point", "EP", "Ending moves, can gather data from all sorts of scraps that imply a point. Will use (0,0,1) for direction when Points are used alone.", GH_ParamAccess.list);
            // ReSharper disable once PossibleNullReferenceException
            pManager[3].Optional = true;
            pManager.AddParameter(new GH_MachinePar(), "Machine", "M", "Machine", GH_ParamAccess.item);
            // ReSharper disable once PossibleNullReferenceException
            pManager[4].WireDisplay = GH_ParamWireDisplay.faint;
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_MachineInstructionPar(),"Instructions", "I", "Machine Instructions", GH_ParamAccess.item);
        }

        private double _nameCount;

        protected override void BeforeSolveInstance()
        {
            this._nameCount = 1;
            base.BeforeSolveInstance();
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }
            List<object> tempMo = new List<object>();
            List<object> sP = new List<object>();
            List<object> eP = new List<object>();

            IMachine m = null;
            string name = string.Empty;

            if (!da.GetData(0, ref name)) { return; }
            if (!da.GetDataList(1, tempMo)) { return; }
            da.GetDataList(2, sP);
            da.GetDataList(3, eP);
            if (!da.GetData(4, ref m)) { return; }

            List<MachineOperation> mo = MachineOperation.toOperations(CAMel_Goo.cleanGooList(tempMo), out int ignores);

            MachineInstruction mi = null;

            if (mo.Count > 0)
            {
                object cleanSP = CAMel_Goo.cleanGooList(sP);
                object cleanEP = CAMel_Goo.cleanGooList(eP);
                // The start and end paths should be rapid moves
                ToolPath startP = ToolPath.toPath(cleanSP);
                foreach(ToolPoint tPt in startP) { tPt.feed = 0; }
                ToolPath endP = ToolPath.toPath(cleanEP);
                foreach (ToolPoint tPt in endP) { tPt.feed = 0; }

                string uName = makeName(name, da);

                mi = new MachineInstruction(uName, m, mo, startP, endP);
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

            da.SetData(0, new GH_MachineInstruction(mi));
        }

        [NotNullAttribute]
        private string makeName([NotNull] string name, [NotNull] IGH_DataAccess da)
        {
            string path = "";

            // Deal with tree coming in if there is one name
            // otherwise assume something sensible is happening
            if (this.Params?.Input?[1]?.VolatileData?.PathCount > 1 &&
                this.Params?.Input?[0]?.VolatileDataCount == 1)
            {
                path = " " + this._nameCount;
                this._nameCount++;
            }
            return name + path;
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.createinstructions;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{B14610C2-E090-49B2-BAA5-ED329562E9B2}");
    }
}