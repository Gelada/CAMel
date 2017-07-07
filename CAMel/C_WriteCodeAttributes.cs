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
            WriteMessages = new Dictionary<WriteState, string>();
            WriteMessages.Add(WriteState.Cancelled, @"Cancelled");
            WriteMessages.Add(WriteState.Finished, @"File Written");
            WriteMessages.Add(WriteState.No_file, @"No File");
            WriteMessages.Add(WriteState.Writing, @"Writing...");
            WriteMessages.Add(WriteState.Waiting, @"Waiting");
        }

        private Dictionary<WriteState, String> WriteMessages;

        protected override void Layout()
        {
            base.Layout();

            System.Drawing.Rectangle rec0 = GH_Convert.ToRectangle(Bounds);

            rec0.Height += 18;
            System.Drawing.Rectangle rec1 = rec0;
            rec1.Y = rec1.Bottom - 25;
            rec1.Height = 26;
            rec1.Inflate(-4, -4);

            this.Bounds = rec0;
            this.ProgressBounds = rec1;
        }
        private System.Drawing.Rectangle ProgressBounds { get; set; }

        protected override void Render(GH_Canvas canvas, System.Drawing.Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);

            if (channel == GH_CanvasChannel.Objects)
            {
                GH_Capsule progress_ol = GH_Capsule.CreateCapsule(
                    ProgressBounds, 
                    GH_Palette.Blue, 
                     0, 1);

                System.Drawing.Rectangle ProgressPercent = this.ProgressBounds;
                ProgressPercent.Width = 
                    (int)(this.ProgressBounds.Width * ((C_WriteCode)Owner).writeProgress);

                GH_Capsule progress = GH_Capsule.CreateCapsule(
                    ProgressPercent,
                    GH_Palette.Brown,
                    0, 1);
                string message = WriteMessages[((C_WriteCode)Owner).WS];
                if(((C_WriteCode)Owner).WS == WriteState.Writing)
                {
                    message = message + " " + ((int)(((C_WriteCode)Owner).writeProgress*100)).ToString() + "%";
                }

                GH_Capsule text_cap = GH_Capsule.CreateTextCapsule(
                    ProgressBounds,
                    ProgressBounds,
                    GH_Palette.Transparent,
                    message,
                    0,1);

                progress_ol.Render(graphics, Selected, Owner.Locked, false);
                if (((C_WriteCode)Owner).writeProgress > 0)
                { progress.Render(graphics, Selected, Owner.Locked, false); }
                text_cap.Render(graphics, Selected, Owner.Locked, false);
                progress_ol.Dispose();
                progress.Dispose();
                text_cap.Dispose();
            }
        }
    }
}