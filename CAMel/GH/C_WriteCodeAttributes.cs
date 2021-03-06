﻿namespace CAMel.GH
{
    using System.Drawing;

    using Grasshopper.GUI.Canvas;
    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Attributes;

    using JetBrains.Annotations;

    /// <inheritdoc />
    /// <summary>TODO The write code attributes.</summary>
    public class WriteCodeAttributes : GH_ComponentAttributes
    {
        /// <summary>Initializes a new instance of the <see cref="WriteCodeAttributes"/> class.</summary>
        /// <param name="owner">TODO The owner.</param>
        public WriteCodeAttributes([NotNull] IGH_Component owner)
            : base(owner) { }

        /// <summary>TODO The total files.</summary>
        /// <param name="l">TODO The l.</param>
        /// <returns>The <see cref="string"/>.</returns>
        [NotNull]
        private static string totalFiles(long l)
        {
            double lMB = l / 1000000.0; // Use smallest MB possibility so numbers are not larger.
            if (l == 0) { return "Nothing Written"; }
            if (lMB > .99) { return lMB.ToString(".0") + " MB Written"; }
            return (1000 * lMB).ToString("0") + " KB Written";
        }

        /// <summary>TODO The layout.</summary>
        protected override void Layout()
        {
            base.Layout();

            Rectangle rec0 = GH_Convert.ToRectangle(this.Bounds);

            rec0.Height += 18;
            Rectangle rec1 = rec0;
            rec1.Y = rec1.Bottom - 25;
            rec1.Height = 26;
            rec1.Inflate(-4, -4);

            this.Bounds = rec0;
            this.progressBounds = rec1;
        }

        /// <summary>Gets or sets the progress bounds.</summary>
        private Rectangle progressBounds { get; set; }

        /// <summary>TODO The render.</summary>
        /// <param name="canvas">TODO The canvas.</param>
        /// <param name="graphics">TODO The graphics.</param>
        /// <param name="channel">TODO The channel.</param>
        protected override void Render([CanBeNull] GH_Canvas canvas, [CanBeNull] Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);

            if (channel != GH_CanvasChannel.Objects) { return; }

            if (this.Owner == null) { return; }

            string message = totalFiles(((C_WriteCode)this.Owner).bytesWritten);

            GH_Capsule textCap = GH_Capsule.CreateTextCapsule(
                this.progressBounds,
                this.progressBounds,
                GH_Palette.Transparent,
                message,
                0, 1);

            GH_Capsule progressOl = GH_Capsule.CreateCapsule(
                this.progressBounds,
                GH_Palette.Brown,
                0, 1);

            if (progressOl == null || textCap == null) { return; }

            progressOl.Render(graphics, this.Selected, this.Owner.Locked, false);
            textCap.Render(graphics, this.Selected, this.Owner.Locked, false);
            progressOl.Dispose();
            textCap.Dispose();
        }
    }
}