using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.GUI.Base;
using Rhino.Geometry;
using CAMel.Types;

namespace CAMel
{
    public enum WriteState
    {
        No_path, Writing, Finished, Cancelled, Waiting
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
            this.filePath = string.Empty;
            this.SaveCode = new CodeInfo();
            this.extension = "ngc";
            this.WS = WriteState.No_path;
            this.writeProgress = 0;

            this.writeFileThread = new BackgroundWorker();
            this.writeFileThread.DoWork += this.bwWriteFile;
            this.writeFileThread.RunWorkerCompleted += this.bwCompletedFileWrite;
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
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            List<String> Ig = new List<string> { "Nothing to Ignore." };
            pManager.AddParameter(new GH_MachineInstructionPar(), "Machine Instructions", "MI", "Complete set of machine instructions to convert to Code for the machine", GH_ParamAccess.item);
            pManager.AddTextParameter("Ignore", "Ig", "List of strings giving errors to turn into warnings", GH_ParamAccess.list,Ig);
            pManager.AddTextParameter("File Path", "FP", "File Path to save code to.", GH_ParamAccess.item, string.Empty);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
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
            base.RecordUndoEvent("Extension");
            this.extension = text;
            this.OnDisplayExpired(true);
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
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            MachineInstruction MI = new MachineInstruction();
            if (!DA.GetData(0, ref MI)) { return; }

            List<String> ignore = new List<string>();
            DA.GetDataList(1, ignore);

            lock (this.SaveCode)
            {
                this.SaveCode = new CodeInfo(MI.mach);
                MachineInstruction procMI;
                try { procMI = MI.processAdditions(MI.mach); }
                catch (InvalidOperationException e)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                    DA.SetData(2, e.Message);
                    return;
                }
                procMI.writeCode(ref this.SaveCode);
            }

            // Detect Errors and warnings
            DA.SetData(2, "");
            string warn = "", error = "";
            if (this.SaveCode.hasWarnings(ignore))
            {
                warn = this.SaveCode.getWarnings(ignore);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, warn);
                DA.SetData(2, warn);
            }
            if (this.SaveCode.hasErrors(ignore))
            {
                error = this.SaveCode.getErrors(ignore);
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
                DA.SetData(2, error + warn);
                return;
            }

            DA.SetData(0, this.SaveCode.ToString());
            DA.SetData(1, this.SaveCode.getRangesString());

            // Write Code to file
            if (DA.GetData(2, ref this.filePath) && this.filePath != string.Empty)
            {
                this.filePath = Path.GetDirectoryName(this.filePath);
                this.filePath = Path.Combine(this.filePath, MI.name + "." + this.extension);
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
                this.WS = WriteState.No_path;
                this.writeProgress = 0;
                this.OnDisplayExpired(true);
            }
        }

        private BackgroundWorker writeFileThread { get; set; }
        private CodeInfo SaveCode;
        private string filePath;
        private string m_extension;
        public string extension
        {
            get { return this.m_extension; }
            set
            {
                this.m_extension = value;
                this.Message = "." + this.m_extension;
            }
        }
        private bool setOffWriting { get; set; }

        public WriteState WS;
        public double writeProgress { get; private set; }

        private void bwWriteFile(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker BW = (BackgroundWorker)sender;
            
            const int saveBlockSize = 40000;
            lock (this.SaveCode)
            {
                this.WS = WriteState.Writing;
                try { File.Delete(this.filePath); }
                catch (Exception) { }
                BW.ReportProgress(0);

                using (StreamWriter SW = new StreamWriter(this.filePath))
                {
                    for (int i = 0; i < this.SaveCode.Length; i += saveBlockSize)
                    {
                        if (BW.CancellationPending)
                        {
                            e.Cancel = true;
                            break;
                        }
                        SW.Write(this.SaveCode.ToString(i, saveBlockSize));
                        BW.ReportProgress((int)Math.Floor(100.0 * i / (double)this.SaveCode.Length));
                    }
                }
            }
        }

        private void bwCompletedFileWrite(object sender, RunWorkerCompletedEventArgs e)
        {
            if(e.Cancelled)
            {
                this.cancelWrite();
            } else
            {
                this.finishWrite();
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
            File.Delete(this.filePath);
            this.WS = WriteState.Cancelled;
            this.writeProgress = 0;
            this.OnDisplayExpired(true);
        }

        private void finishWrite()
        {
            this.WS = WriteState.Finished;
            this.writeProgress = 1;
            //this.OnDisplayExpired(true);
        }

        private void bwProgressWithFile(object sender, ProgressChangedEventArgs e)
        {
            this.updateWriteProgress(e.ProgressPercentage / 100.0);
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