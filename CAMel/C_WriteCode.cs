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
        No_file, Writing, Finished, Cancelled, Waiting
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
            this.filePath = "";
            this.SaveCode = new CodeInfo();
            this.WS = WriteState.No_file;
            this.writeProgress = 0;

            this.WriteFileThread = new BackgroundWorker();
            this.WriteFileThread.DoWork += this.BW_WriteFile;
            this.WriteFileThread.RunWorkerCompleted += this.BW_completedFileWrite;
            this.WriteFileThread.ProgressChanged += BW_progressWithFile;
            this.WriteFileThread.WorkerReportsProgress = true;
            this.WriteFileThread.WorkerSupportsCancellation = true;


        }

        public override void CreateAttributes()
        {
            m_attributes = new WriteCodeAttributes(this);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            List<String> Ig = new List<string>();
            Ig.Add("Nothing to Ignore.");
            pManager.AddGenericParameter("Machine Instructions", "MI", "Complete set of machine instructions to convert to Code for the machine", GH_ParamAccess.item);
            pManager.AddTextParameter("Ignore", "Ig", "List of strings giving errors to turn into warnings", GH_ParamAccess.list,Ig);
            pManager.AddTextParameter("File Path", "FP", "File Path to save code to.", GH_ParamAccess.item, "");
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Code", "Code", "Code for the machine", GH_ParamAccess.item);
            pManager.AddTextParameter("Ranges", "R", "Ranges of movement", GH_ParamAccess.item);
        }

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();

            // Cancel a write thread if it is running
            if (this.WriteFileThread.IsBusy)
            {
                this.WriteFileThread.CancelAsync();
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
            if (!DA.GetData(0, ref MI))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter MI failed to collect data.");
                return;
            }

            List<String> ignore = new List<string>();
            DA.GetDataList(1, ignore);

            lock (this.SaveCode)
            {
                this.SaveCode = new CodeInfo(MI.Mach);

                MachineInstruction procMI = MI.ProcessAdditions();

                procMI.WriteCode(ref this.SaveCode);
            }
            // Detect Errors and warnings

            // TODO report errors and warnings in an output parameter

            if (this.SaveCode.HasErrors(ignore))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, this.SaveCode.GetErrors(ignore));
            if (this.SaveCode.HasWarnings(ignore))
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, this.SaveCode.GetWarnings(ignore));

            // Extract Ranges

            Dictionary<String, Interval> Ranges = this.SaveCode.GetRanges();
            string rOut = "";

            foreach (string k in Ranges.Keys)
            {
                rOut = rOut + "\n" + k + ": " + Ranges[k].T0.ToString("0.00") + " to " + Ranges[k].T1.ToString("0.00");
            }

            DA.SetData(0, this.SaveCode.ToString());
            DA.SetData(1, rOut);

            lock(this.filePath)
            {
                if(DA.GetData(2, ref this.filePath) && this.filePath != "")
                {
                    // queue up file write
                    if (!this.WriteFileThread.IsBusy)
                    {
                        this.WriteFileThread.RunWorkerAsync();
                    }
                    else {
                        this.setOffWriting = true;
                    }
                } 
                else
                {
                    this.WS = WriteState.No_file;
                    this.writeProgress = 0;
                    this.OnDisplayExpired(true);
                }
            }
        }

        private BackgroundWorker WriteFileThread { get; set; }
        private CodeInfo SaveCode;
        private string filePath;
        private bool setOffWriting { get; set; }

        public WriteState WS;
        public double writeProgress { get; private set; }

        private void BW_WriteFile(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker BW = (BackgroundWorker)sender;
            
            const int saveBlockSize = 40000;
            lock (this.filePath) lock (this.SaveCode)
                {
                    this.WS = WriteState.Writing;
                    try { File.Delete(filePath); }
                    catch (Exception) { }
                    BW.ReportProgress(0);

                    using (StreamWriter SW = new StreamWriter(filePath))
                    {
                        for (int i = 0; i < SaveCode.Length; i += saveBlockSize)
                        {
                            if (BW.CancellationPending)
                            {
                                e.Cancel = true;
                                break;
                            }
                            SW.Write(SaveCode.ToString(i, saveBlockSize));
                            BW.ReportProgress((int)Math.Floor(100.0 * i / (double)SaveCode.Length));
                        }
                    }
                }
        }

        private void BW_completedFileWrite(object sender, RunWorkerCompletedEventArgs e)
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
                this.WriteFileThread.RunWorkerAsync();
            }
        }

        private void cancelWrite()
        {
            File.Delete(filePath);
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

        private void BW_progressWithFile(object sender, ProgressChangedEventArgs e)
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