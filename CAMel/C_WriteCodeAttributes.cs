using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.GUI.Canvas;

namespace CAMel
{

    public class WriteCodeAttributes : GH_ComponentAttributes {
        public WriteCodeAttributes(C_WriteCode owner) :
            base(owner)
        {
            this.WriteMessages = new Dictionary<WriteState, string>
            {
                { WriteState.Cancelled, @"Cancelled" },
                { WriteState.Finished, @"File Written" },
                { WriteState.No_path, @"No Path" },
                { WriteState.Writing, @"Writing..." },
                { WriteState.Waiting, @"Waiting" }
            };
        }
        
        private Dictionary<WriteState, String> WriteMessages;
        
        public void setFileSize(long l)
        {
            double lMB = l / 1000000.0; // Use smallest MB possibility so numbers are not larger. 
            WriteMessages[WriteState.Finished] = lMB.ToString(".0")+" MB Written";
        }

        protected override void Layout()
        {
            base.Layout();

            System.Drawing.Rectangle rec0 = GH_Convert.ToRectangle(this.Bounds);

            rec0.Height += 18;
            System.Drawing.Rectangle rec1 = rec0;
            rec1.Y = rec1.Bottom - 25;
            rec1.Height = 26;
            rec1.Inflate(-4, -4);

            this.Bounds = rec0;
            this.progressBounds = rec1;
        }
        private System.Drawing.Rectangle progressBounds { get; set; }

        protected override void Render(GH_Canvas canvas, System.Drawing.Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);

            if (channel == GH_CanvasChannel.Objects)
            {
                GH_Capsule progress_ol = GH_Capsule.CreateCapsule(
                    this.progressBounds, 
                    GH_Palette.Blue, 
                     0, 1);

                System.Drawing.Rectangle ProgressPercent = this.progressBounds;
                ProgressPercent.Width = 
                    (int)(this.progressBounds.Width * ((C_WriteCode)this.Owner).writeProgress);

                GH_Capsule progress = GH_Capsule.CreateCapsule(
                    ProgressPercent,
                    GH_Palette.Brown,
                    0, 1);
                string message = this.WriteMessages[((C_WriteCode)this.Owner).WS];
                if(((C_WriteCode)this.Owner).WS == WriteState.Writing)
                {
                    message = message + " " + ((int)(((C_WriteCode)this.Owner).writeProgress*100)).ToString() + "%";
                }

                GH_Capsule text_cap = GH_Capsule.CreateTextCapsule(
                    this.progressBounds,
                    this.progressBounds,
                    GH_Palette.Transparent,
                    message,
                    0,1);

                progress_ol.Render(graphics, this.Selected, this.Owner.Locked, false);
                if (((C_WriteCode)this.Owner).writeProgress > 0)
                { progress.Render(graphics, this.Selected, this.Owner.Locked, false); }
                text_cap.Render(graphics, this.Selected, this.Owner.Locked, false);
                progress_ol.Dispose();
                progress.Dispose();
                text_cap.Dispose();
            }
        }
    }
}