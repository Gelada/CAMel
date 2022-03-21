namespace CAMel.GH
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using CAMel.Types;

    using Grasshopper.Kernel;

    using JetBrains.Annotations;

    /// <inheritdoc />
    /// <summary>TODO The c_ write code.</summary>
    [UsedImplicitly]
    public class C_WriteCode : GH_Component
    {
        /// <summary>Gets or sets the bytes written.</summary>
        internal long bytesWritten { get; set; }
        internal int version { get; set; }
        internal int uVersion { get; set; }
        internal bool first { get; set; }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_WriteCode()
            : base(
                "Write CNC Code", "Write",
                "Write CNC Code",
                "CAMel", "CNC Code") => this.bytesWritten = 0;

        /// <inheritdoc />
        public override void CreateAttributes()
        {
            this.m_attributes = new WriteCodeAttributes(this);
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            List<string> ig = new List<string> { "Nothing to Ignore." };
            pManager.AddParameter(new GH_MachineInstructionPar(), "Machine Instructions", "MI", "Complete set of machine instructions to convert to Code for the machine", GH_ParamAccess.item);
            pManager.AddTextParameter("Ignore", "Ig", "List of strings giving errors to turn into warnings", GH_ParamAccess.list, ig);
            pManager.AddTextParameter("File Path", "FP", "File Path to save code to.", GH_ParamAccess.item, string.Empty);
            pManager.AddBooleanParameter("Write Code", "W", "If true will write text file.", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Reset Saves", "R", "Reset save counter to overwrite old versions", GH_ParamAccess.item, true);
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddTextParameter("Code", "Code", "Code for the machine", GH_ParamAccess.item);
            pManager.AddTextParameter("Ranges", "R", "Ranges of movement", GH_ParamAccess.item);
            pManager.AddTextParameter("Warnings and Errors", "E", "Warnings and Errors reported by the code", GH_ParamAccess.item);
        }

        /// <inheritdoc />
        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();
            this.first = true;
            this.uVersion = this.version;
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }
            MachineInstruction mI = null;
            if (!da.GetData("Machine Instructions", ref mI)) { return; }

            List<string> ignore = new List<string>();
            bool write = true, reset = true;
            da.GetDataList("Ignore", ignore);
            da.GetData("Write Code", ref write);
            da.GetData("Reset Saves", ref reset);

            MachineInstruction procMI;
            try { procMI = mI.processAdditions(); }
            catch (InvalidOperationException e)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                da.SetData(2, e.Message);
                return;
            }

            CodeInfo saveCode = procMI.writeCode();

            // Detect Errors and warnings
            da.SetData(2, "");
            string warn = "";

            if (saveCode.hasErrors(ignore))
            {
                string error = saveCode.getErrors(ignore);
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
                da.SetData(2, error + warn);
                return;
            }

            da.SetData(0, saveCode.ToString());
            da.SetData(1, saveCode.getRangesString());

            if (saveCode.hasWarnings(ignore))
            {
                warn = saveCode.getWarnings(ignore);
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, warn);
                da.SetData(2, warn);
            }

            string fPath = string.Empty;

            // Write Code to file
            if (da.GetData(2, ref fPath) && !string.IsNullOrEmpty(fPath))
            {
                fPath = Path.GetDirectoryName(fPath) ?? string.Empty;
                string filePath = fPath;
                string versionS = string.Empty;
                if(reset)
                {
                    version = 1;
                } else if(uVersion > 1) {
                    versionS = "_V" + uVersion.ToString();
                }

                if (write)
                {
                    fPath = Path.Combine(filePath, mI.name + versionS + "." + mI.m.extension);
                    if (File.Exists(fPath)) { File.Delete(fPath); }

                    if (first) { this.bytesWritten = 0; }
                    using (StreamWriter sW = new StreamWriter(fPath))
                    {
                        for (int i = 0; i < saveCode.Length; i += 40000)
                        { sW.Write(saveCode.ToString(i, 40000)); }
                    }
                    FileInfo file = new FileInfo(fPath);
                    if (file.Exists) { this.bytesWritten += file.Length; }
                    if(this.first) {
                        version++;
                        this.first = false;
                    }
                }
            }
            else { this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No path given."); }
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.cncwriter;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{908e79f0-4698-4642-9158-b90c8d9df83a}");
    }
}