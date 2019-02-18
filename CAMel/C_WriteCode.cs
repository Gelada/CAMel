using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
//using System.Windows.Forms.VisualStyles;
using Grasshopper.Kernel;

using CAMel.Types;
using JetBrains.Annotations;

namespace CAMel
{
    public enum WriteState
    {
        NoPath, Writing, Finished, Cancelled, Waiting
    }

    [UsedImplicitly]
    public class C_WriteCode : GH_Component
    {
        [NotNull] private BackgroundWorker writeFileThread { get; }
        private CodeInfo _saveCode;
        [NotNull] private string _filePath;
        private string _extension;
        [NotNull]
        private string extension
        {
            get => this._extension ?? string.Empty;
            set
            {
                this._extension = value;
                this.Message = "." + this._extension;
            }
        }
        private bool setOffWriting { get; set; }

        public WriteState ws;
        public double writeProgress { get; private set; }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_WriteCode()
            : base("Write CNC Code", "Write",
                "Write CNC Code",
                "CAMel", "CNC Code")
        {
            this._filePath = string.Empty;
            this.extension = "ngc";
            this.ws = WriteState.NoPath;
            this.writeProgress = 0;

            this.writeFileThread = new BackgroundWorker();
            this.writeFileThread.DoWork += bwWriteFile;
            this.writeFileThread.RunWorkerCompleted += bwCompletedFileWrite;
            this.writeFileThread.ProgressChanged += bwProgressWithFile;
            this.writeFileThread.WorkerReportsProgress = true;
            this.writeFileThread.WorkerSupportsCancellation = true;
        }

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
            pManager.AddTextParameter("Ignore", "Ig", "List of strings giving errors to turn into warnings", GH_ParamAccess.list,ig);
            pManager.AddTextParameter("File Path", "FP", "File Path to save code to.", GH_ParamAccess.item, string.Empty);
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

        // Need to save and recover the extension
        public override bool Write([CanBeNull] GH_IO.Serialization.GH_IWriter writer)
        {
            if (writer == null) { return base.Write(null); }
            // First add our own field.
            writer.SetString("Extension", this.extension);
            // Then call the base class implementation.
            return base.Write(writer);
        }
        public override bool Read([CanBeNull] GH_IO.Serialization.GH_IReader reader)
        {
            if (reader == null) { return false; }
            // First read our own field.
            if (reader.ItemExists("Extension")) { this.extension = reader.GetString("Extension") ?? string.Empty; }

            // Then call the base class implementation.
            return base.Read(reader);
        }

        protected override void AppendAdditionalComponentMenuItems([NotNull] ToolStripDropDown menu)
        {
            if (menu == null) { throw new ArgumentNullException(); }
            Menu_AppendItem(menu, "File Extension");
            Menu_AppendTextItem(menu, this.extension, menuExtensionClick, menuExtensionChange, true);
        }

        private static void menuExtensionClick([NotNull] object sender, [NotNull] EventArgs e)
        {
            if (sender == null || e == null) { throw new ArgumentNullException(); }
        }

        private void menuExtensionChange([NotNull] object sender, [CanBeNull] string text)
        {
            if (sender == null) { throw new ArgumentNullException(); }
            RecordUndoEvent("Extension");
            this.extension = text ?? string.Empty;
            OnDisplayExpired(true);
        }

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();

            // Cancel a write thread if it is running
            if (!this.writeFileThread.IsBusy) { return; }

            this.writeFileThread.CancelAsync();
            this.setOffWriting = false;
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
            if (!da.GetData(0, ref mI)) { return; }

            List<string> ignore = new List<string>();
            da.GetDataList(1, ignore);

            MachineInstruction procMI;
            try { procMI = mI.processAdditions(); }
            catch (InvalidOperationException e)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                da.SetData(2, e.Message);
                return;
            }

            this._saveCode = procMI.writeCode();

            // Detect Errors and warnings
            da.SetData(2, "");
            string warn = "";

            if (this._saveCode.hasErrors(ignore))
            {
                string error = this._saveCode.getErrors(ignore);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
                da.SetData(2, error + warn);
                return;
            }

            da.SetData(0, this._saveCode.ToString());
            da.SetData(1, this._saveCode.getRangesString());

            if (this._saveCode.hasWarnings(ignore))
            {
                warn = this._saveCode.getWarnings(ignore);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, warn);
                da.SetData(2, warn);
            }

            // Write Code to file
            if (da.GetData(2, ref this._filePath) && this._filePath != string.Empty)
            {
                this._filePath = Path.GetDirectoryName(this._filePath) ?? string.Empty;
                string filePath = this._filePath;
                if (filePath != null)
                { this._filePath = Path.Combine(filePath, mI.name + "." + this.extension); }

                // queue up file write
                if (!this.writeFileThread.IsBusy)
                {
                    this.writeFileThread.RunWorkerAsync();
                }
                else
                {
                    this.setOffWriting = true;
                }
            }
            else
            {
                this.ws = WriteState.NoPath;
                this.writeProgress = 0;
                OnDisplayExpired(true);
            }
        }

        private void bwWriteFile([NotNull] object sender, [NotNull] DoWorkEventArgs e)
        {
            if (sender == null || e == null) { throw new ArgumentNullException(); }
            BackgroundWorker bW = (BackgroundWorker)sender;

            const int saveBlockSize = 40000;
            if (this._saveCode == null) { return;}
            lock (this._saveCode)
            {
                this.ws = WriteState.Writing;

                if(File.Exists(this._filePath)) { File.Delete(this._filePath); }

                bW.ReportProgress(0);

                using (StreamWriter sW = new StreamWriter(this._filePath))
                {
                    for (int i = 0; i < this._saveCode.Length; i += saveBlockSize)
                    {
                        if (bW.CancellationPending)
                        {
                            e.Cancel = true;
                            break;
                        }
                        sW.Write(this._saveCode.ToString(i, saveBlockSize));
                        bW.ReportProgress((int)Math.Floor(100.0 * i / this._saveCode.Length));
                    }
                }
            }
        }

        private void bwCompletedFileWrite([NotNull] object sender, [NotNull] RunWorkerCompletedEventArgs e)
        {
            if (sender == null || e == null) { throw new ArgumentNullException(); }
            if (e.Cancelled)
            {
                cancelWrite();
            } else
            {
                finishWrite();
            }
            // Restart writing if asked.
            if (!this.setOffWriting) { return; }

            this.setOffWriting = false;
            this.writeFileThread.RunWorkerAsync();
        }

        private void cancelWrite()
        {
            File.Delete(this._filePath);
            this.ws = WriteState.Cancelled;
            this.writeProgress = 0;
            OnDisplayExpired(true);
        }

        private void finishWrite()
        {
            this.ws = WriteState.Finished;
            this.writeProgress = 1;
            long fileSize = new FileInfo(this._filePath).Length;
            ((WriteCodeAttributes) this.Attributes)?.setFileSize(fileSize);
            //this.OnDisplayExpired(true);
        }

        private void bwProgressWithFile([NotNull] object sender, [NotNull] ProgressChangedEventArgs e)
        {
            if (sender == null || e == null) { throw new ArgumentNullException(); }
            updateWriteProgress(e.ProgressPercentage / 100.0);
        }

        private void updateWriteProgress(double progress)
        {
            this.writeProgress = progress;
            //this.OnDisplayExpired(true);
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