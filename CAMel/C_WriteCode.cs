using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

using Grasshopper.Kernel;

using CAMel.Types;

namespace CAMel
{
    public enum WriteState
    {
        NoPath, Writing, Finished, Cancelled, Waiting
    }

    public class C_WriteCode : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_WriteCode()
            : base("Write CNC Code", "Write",
                "Write CNC Code",
                "CAMel", "CNC Code")
        {
            this._filePath = string.Empty;
            this._saveCode = new CodeInfo();
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

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            List<String> ig = new List<string> { "Nothing to Ignore." };
            pManager.AddParameter(new GH_MachineInstructionPar(), "Machine Instructions", "MI", "Complete set of machine instructions to convert to Code for the machine", GH_ParamAccess.item);
            pManager.AddTextParameter("Ignore", "Ig", "List of strings giving errors to turn into warnings", GH_ParamAccess.list,ig);
            pManager.AddTextParameter("File Path", "FP", "File Path to save code to.", GH_ParamAccess.item, string.Empty);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Code", "Code", "Code for the machine", GH_ParamAccess.item);
            pManager.AddTextParameter("Ranges", "R", "Ranges of movement", GH_ParamAccess.item);
            pManager.AddTextParameter("Warnings and Errors", "E", "Warnings and Errors reported by the code", GH_ParamAccess.item);
        }

        // Need to save and recover the extension
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            // First add our own field.
            writer.SetString("Extension", this.extension);
            // Then call the base class implementation.
            return base.Write(writer);
        }
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            // First read our own field.
            if (reader.ItemExists("Extension"))
            {
                this.extension = reader.GetString("Extension");
            }
            // Then call the base class implementation.
            return base.Read(reader);
        }

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            Menu_AppendItem(menu, "File Extension");
            Menu_AppendTextItem(menu, this.extension, menuExtensionClick, menuExtensionChange, true);
        }

        private void menuExtensionClick(object sender, EventArgs e)
        {
        }

        private void menuExtensionChange(object sender, string text)
        {
            RecordUndoEvent("Extension");
            this.extension = text;
            OnDisplayExpired(true);
        }

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();

            // Cancel a write thread if it is running
            if (this.writeFileThread.IsBusy)
            {
                this.writeFileThread.CancelAsync();
                this.setOffWriting = false;
            }
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess da)
        {
            MachineInstruction mI = new MachineInstruction();
            if (!da.GetData(0, ref mI)) { return; }

            List<String> ignore = new List<string>();
            da.GetDataList(1, ignore);

            lock (this._saveCode)
            {
                this._saveCode = new CodeInfo(mI.mach);
                MachineInstruction procMI;
                try { procMI = mI.processAdditions(mI.mach); }
                catch (InvalidOperationException e)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                    da.SetData(2, e.Message);
                    return;
                }
                procMI.writeCode(ref this._saveCode);
            }

            // Detect Errors and warnings
            da.SetData(2, "");
            string warn = "";
            if (this._saveCode.hasWarnings(ignore))
            {
                warn = this._saveCode.getWarnings(ignore);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, warn);
                da.SetData(2, warn);
            }
            if (this._saveCode.hasErrors(ignore))
            {
                string error = this._saveCode.getErrors(ignore);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
                da.SetData(2, error + warn);
                return;
            }

            da.SetData(0, this._saveCode.ToString());
            da.SetData(1, this._saveCode.getRangesString());

            // Write Code to file
            if (da.GetData(2, ref this._filePath) && this._filePath != string.Empty)
            {
                this._filePath = Path.GetDirectoryName(this._filePath);
                this._filePath = Path.Combine(this._filePath, mI.name + "." + this.extension);
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

        private BackgroundWorker writeFileThread { get; set; }
        private CodeInfo _saveCode;
        private string _filePath;
        private string _extension;
        private string extension
        {
            get { return this._extension; }
            set
            {
                this._extension = value;
                this.Message = "." + this._extension;
            }
        }
        private bool setOffWriting { get; set; }

        public WriteState ws;
        public double writeProgress { get; private set; }

        private void bwWriteFile(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bW = (BackgroundWorker)sender;
            
            const int saveBlockSize = 40000;
            lock (this._saveCode)
            {
                this.ws = WriteState.Writing;
                try { File.Delete(this._filePath); }
                catch (Exception) { }
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

        private void bwCompletedFileWrite(object sender, RunWorkerCompletedEventArgs e)
        {
            if(e.Cancelled)
            {
                cancelWrite();
            } else
            {
                finishWrite();
            }
            // Restart writing if asked.
            if(this.setOffWriting)
            {
                this.setOffWriting = false;
                this.writeFileThread.RunWorkerAsync();
            }
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
            ((WriteCodeAttributes)this.Attributes).setFileSize(fileSize);
            //this.OnDisplayExpired(true);
        }

        private void bwProgressWithFile(object sender, ProgressChangedEventArgs e)
        {
            updateWriteProgress(e.ProgressPercentage / 100.0);
        }

        private void updateWriteProgress(double progress)
        {
            this.writeProgress = progress;
            //this.OnDisplayExpired(true);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Properties.Resources.cncwriter;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{908e79f0-4698-4642-9158-b90c8d9df83a}"); }
        }
    }
}