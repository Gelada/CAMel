namespace CAMel.Types
{
    using System;

    using CAMel.Types.Machine;

    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Types;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    // Different end mill shapes to consider
    /// <summary>TODO The end shape.</summary>
    public enum EndShape
    {
        /// <summary>TODO The ball.</summary>
        Ball,
        /// <summary>TODO The square.</summary>
        Square,
        /// <summary>TODO The v.</summary>
        V,
        /// <summary>TODO The other.</summary>
        Other,
        /// <summary>TODO The error.</summary>
        Error
    }

    /// <summary>TODO The material tool builder.</summary>
    [UsedImplicitly]
    public class MaterialToolBuilder
    {
        /// <summary>Gets or sets the mat name.</summary>
        public string matName { get; [UsedImplicitly] set; } // Name of the material
        /// <summary>Gets or sets the tool name.</summary>
        public string toolName { get; [UsedImplicitly] set; } // Name of the tool
        /// <summary>Gets or sets the tool number.</summary>
        public int toolNumber { get; [UsedImplicitly] set; } // Number of the tool
        /// <summary>Gets or sets the speed.</summary>
        public double speed { get; [UsedImplicitly] set; } // speed of spindle (assumed unset for negative values)
        /// <summary>Gets or sets the feed cut.</summary>
        public double feedCut { get; [UsedImplicitly] set; } // feed rate for cutting (assumed unset for negative values)
        /// <summary>Gets or sets the feed plunge.</summary>
        public double feedPlunge { get; [UsedImplicitly] set; } // feed rate for plunging (assumed unset for negative values)
        /// <summary>Gets or sets the cut depth.</summary>
        public double cutDepth { get; [UsedImplicitly] set; } // maximum material to cut away (assumed unset for negative values)
        /// <summary>Gets or sets the finish depth.</summary>
        public double finishDepth { get; [UsedImplicitly] set; } // thickness to cut in a finish pass
        /// <summary>Gets or sets the tool width.</summary>
        public double toolWidth { get; [UsedImplicitly] set; } // width of tool (assumed unset for negative values)
        /// <summary>Gets or sets the insert width.</summary>
        public double insertWidth { get; [UsedImplicitly] set; } // width needed to insert into material
        /// <summary>Gets or sets the tool length.</summary>
        public double toolLength { get; [UsedImplicitly] set; } // length from the tip of the tool to the spindle
        /// <summary>Gets or sets the shape.</summary>
        public string shape { get; [UsedImplicitly] set; } // End shape of the tool
        /// <summary>Gets or sets the side load.</summary>
        public double sideLoad { get; [UsedImplicitly] set; } // Suggested side load for the tool.
        /// <summary>Gets or sets the tolerance.</summary>
        public double tolerance { get; [UsedImplicitly] set; } // The maximum permitted distance of approximation from curve
        /// <summary>Gets or sets the min step.</summary>
        public double minStep { get; [UsedImplicitly] set; } // shortest path permitted
        /// <summary>Gets or sets the path jump.</summary>
        public double pathJump { get; [UsedImplicitly] set; } // maximum jump between toolpaths in material
    }

    // Settings for a particular material and tool
    /// <inheritdoc />
    /// <summary>TODO The material tool.</summary>
    public class MaterialTool : ICAMelBase
    {
        /// <summary>Gets the mat name.</summary>
        [NotNull]
        public string matName { get; } // Name of the materialMaterialToolReader
        /// <summary>Gets the tool name.</summary>
        [NotNull]
        public string toolName { get; } // Name of the tool
        /// <summary>Gets the tool number.</summary>
        public int toolNumber { get; } // Number of the tool
        /// <summary>Gets the speed.</summary>
        public double speed { get; } // speed of spindle (assumed unset for negative values)
        /// <summary>Gets the feed cut.</summary>
        public double feedCut { get; } // feed rate for cutting (assumed unset for negative values)
        /// <summary>Gets the feed plunge.</summary>
        public double feedPlunge { get; } // feed rate for plunging (assumed unset for negative values)
        /// <summary>Gets the cut depth.</summary>
        public double cutDepth { get; } // maximum material to cut away (assumed unset for negative values)
        /// <summary>Gets the finish depth.</summary>
        public double finishDepth { get; } // thickness to cut in a finish pass
        /// <summary>Gets the tool width.</summary>
        public double toolWidth { get; } // width of tool (assumed unset for negative values)
        /// <summary>Gets the insert width.</summary>
        public double insertWidth { get; } // width needed to insert into material
        /// <summary>Gets the tool length.</summary>
        public double toolLength { get; } // length from the tip of the tool to the spindle
        /// <summary>Gets the shape.</summary>
        private EndShape shape { get; } // End shape of the tool
        /// <summary>Gets the side load.</summary>
        public double sideLoad { get; } // Suggested side load for the tool.
        /// <summary>Gets the path jump.</summary>
        public double pathJump { get; } // maximum jump between toolpaths in material

        // settings for curve approximation
        /// <summary>Gets the tolerance.</summary>
        public double tolerance { get; } // The maximum permitted distance of approximation from curve
        /// <summary>Gets the min step.</summary>
        public double minStep { get; } // shortest path permitted

        // Adding anything here needs significant support:
        //  Add to MaterialToolBuilder
        //  Add to Constructors
        //  Add to csv mapping
        //  Add to create Material Tool

        // Everything, with defaults
        /// <summary>Initializes a new instance of the <see cref="MaterialTool"/> class.</summary>
        /// <param name="mat">TODO The mat.</param>
        /// <param name="tool">TODO The tool.</param>
        /// <param name="toolN">TODO The tool n.</param>
        /// <param name="speed">TODO The speed.</param>
        /// <param name="feedCut">TODO The feed cut.</param>
        /// <param name="feedPlunge">TODO The feed plunge.</param>
        /// <param name="cutDepth">TODO The cut depth.</param>
        /// <param name="finishDepth">TODO The finish depth.</param>
        /// <param name="width">TODO The width.</param>
        /// <param name="iWidth">TODO The i width.</param>
        /// <param name="tL">TODO The t l.</param>
        /// <param name="eS">TODO The e s.</param>
        /// <param name="tol">TODO The tol.</param>
        /// <param name="mS">TODO The m s.</param>
        /// <param name="sideLoad">TODO The side load.</param>
        /// <param name="pathJump">TODO The path jump.</param>
        public MaterialTool(
            [CanBeNull] string mat, [CanBeNull] string tool, int toolN, double speed, double feedCut, double feedPlunge, double cutDepth, double finishDepth = 0, double width = -1, double iWidth = -1,
            double tL = 0, EndShape eS = EndShape.Other, double tol = 0, double mS = 0, double sideLoad = 0.7, double pathJump = -1.0)
        {
            this.matName = mat ?? string.Empty;
            this.toolName = tool ?? string.Empty;
            this.toolNumber = toolN;
            this.speed = speed;
            this.feedCut = feedCut;
            this.feedPlunge = feedPlunge;
            this.finishDepth = finishDepth;
            this.cutDepth = cutDepth;
            this.toolWidth = width;
            this.insertWidth = iWidth;
            this.toolLength = tL;
            this.tolerance = tol;
            this.minStep = mS;
            this.shape = eS;
            this.sideLoad = sideLoad;
            this.pathJump = pathJump;
        }

        /// <summary>Initializes a new instance of the <see cref="MaterialTool"/> class.</summary>
        /// <param name="mT">TODO The m t.</param>
        public MaterialTool([NotNull] MaterialToolBuilder mT)
        {
            this.matName = mT.matName ?? string.Empty;
            this.toolName = mT.toolName ?? string.Empty;
            this.toolNumber = mT.toolNumber;
            this.speed = mT.speed;
            this.feedCut = mT.feedCut;
            this.feedPlunge = mT.feedPlunge;
            this.finishDepth = mT.finishDepth;
            this.cutDepth = mT.cutDepth;
            this.toolWidth = mT.toolWidth;
            this.insertWidth = mT.insertWidth;
            this.toolLength = mT.toolLength;
            this.tolerance = mT.tolerance;
            this.minStep = mT.minStep;
            this.shape = getToolShape(mT.shape);
            this.sideLoad = mT.sideLoad;
            this.pathJump = mT.pathJump;
        }

        /// <summary>TODO The change finish depth.</summary>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="fd">TODO The fd.</param>
        /// <returns>The <see cref="MaterialTool"/>.</returns>
        [NotNull]
        public static MaterialTool changeFinishDepth([NotNull] MaterialTool mT, double fd) => new MaterialTool(
            mT.matName, mT.toolName, mT.toolNumber, mT.speed,
            mT.feedCut, mT.feedPlunge, mT.cutDepth, fd,
            mT.toolWidth, mT.insertWidth, mT.toolLength, mT.shape, mT.tolerance, mT.minStep, mT.sideLoad, mT.pathJump);

        /// <summary>TODO The empty.</summary>
        [NotNull] public static readonly MaterialTool Empty = new MaterialTool(null, null, -1, -1, -1, -1, -1, -1);

        /// <inheritdoc />
        public string TypeDescription => "Details of a Material and Tool";

        /// <inheritdoc />
        public string TypeName => "MaterialTool";

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        public override string ToString() => "Tool " + this.toolNumber + ", " + this.toolName + " into " + this.matName;

        /// <summary>Offset toolpoint so that it does not gouge an angled path.</summary>
        /// <param name="m">Machine processing path. </param>
        /// <param name="tP">ToolPoint to offset</param>
        /// <param name="travel">Direction of travel.</param>
        /// <param name="orthogonal">Vector orthogonal to offset plane.</param>
        /// <returns>The <see cref="ToolPoint"/>.</returns>
        [NotNull]
        public ToolPoint threeAxisHeightOffset([NotNull] IMachine m, [NotNull] ToolPoint tP, Vector3d travel, Vector3d orthogonal)
        {
            // We want to use cutOffset, so need to find the normal
            // That is the Vector at right angles to the travel direction
            // in the plane given by orthogonal

            // Do nothing if orthogonal does not give a plane
            if (Math.Abs(orthogonal.Length) < CAMel_Goo.Tolerance) { return tP; }

            // Rotate 90 degrees, and check we get the one closer to the tool direction
            Vector3d norm = travel;
            norm.Transform(Transform.Rotation(Math.PI / 2, orthogonal, new Point3d(0, 0, 0)));
            double testD = norm * m.toolDir(tP);
            if (testD < 0) { norm = -1 * norm; }

            ToolPoint osTp = tP.deepClone();

            // move tool so that it cuts at the toolpoint location and does not gouge.
            osTp.pt += this.cutOffset(m.toolDir(tP), norm);

            return osTp;
        }

        // Find the path offset so the cutting surface of the tool is on the path
        /// <summary>TODO The cut offset.</summary>
        /// <param name="dir">TODO The dir.</param>
        /// <param name="norm">TODO The norm.</param>
        /// <returns>The <see cref="Vector3d"/>.</returns>
        public Vector3d cutOffset(Vector3d dir, Vector3d norm)
        {
            Vector3d uDir = dir;
            Vector3d uNorm = norm;
            uDir.Unitize();
            uNorm.Unitize();
            Vector3d os;
            switch (this.shape)
            {
                case EndShape.Ball: // find correct position of ball centre and then push to tip.
                    os = this.toolWidth * (uNorm - uDir) / 2;
                    break;
                case EndShape.Square: // Cut with corner if the angle is greater than .01 radians
                                      // magic numbers for approx zero and small angles
                                      // magic number determining small angle
                                      // TODO make this more transparent
                    const double sa = 2 * Math.PI / 180.0;
                    const double na = .1 * Math.PI / 180.0;
                    double a = Vector3d.VectorAngle(uDir, uNorm);
                    if (a < na)
                    {
                        os = new Vector3d(0, 0, 0);
                    }
                    else
                    {
                        // find the normal to the plane give by the tool direction and the norm
                        Vector3d plN = Vector3d.CrossProduct(uNorm, uDir);
                        plN.Unitize();
                        // Now want a vector on that plane orthogonal to tool direction
                        os = Vector3d.CrossProduct(uDir, plN);
                        os.Unitize();
                        // Need the magnitude in that direction. This should be 
                        // tool radius, but that creates a jump away from the singularity, 
                        // so for small angles will scale this down.
                        double ur = this.toolWidth / 2.0;
                        if(a < sa) { ur = ur * (a-na) / (sa-na); }
                        os = os * ur;
                    }

                    break;
                case EndShape.V:
                    // Just use the tip. Beyond a certain angle this will not work
                    // TODO store angle of V end mill and use that and width
                    os = new Vector3d(0, 0, 0);
                    break;
                case EndShape.Other:
                    os = new Vector3d(0, 0, 0);
                    break;
                default:
                    os = new Vector3d(0, 0, 0);
                    break;
            }

            return os;
        }

        /// <summary>TODO The get tool shape.</summary>
        /// <param name="toolShape">TODO The tool shape.</param>
        /// <returns>The <see cref="EndShape"/>.</returns>
        public static EndShape getToolShape([CanBeNull] string toolShape)
        {
            switch (toolShape)
            {
                case "Ball":
                    return EndShape.Ball;
                case "Square":
                    return EndShape.Square;
                case "V":
                    return EndShape.V;
                case "Other":
                    return EndShape.Other;
                default:
                    return EndShape.Error;
            }
        }
    }

    // Grasshopper Type Wrapper
    /// <summary>TODO The g h_ material tool.</summary>
    public sealed class GH_MaterialTool : CAMel_Goo<MaterialTool>
    {
        /// <summary>Initializes a new instance of the <see cref="GH_MaterialTool"/> class.</summary>
        [UsedImplicitly]
        public GH_MaterialTool() => this.Value = null;

        // construct from unwrapped type
        /// <summary>Initializes a new instance of the <see cref="GH_MaterialTool"/> class.</summary>
        /// <param name="mT">TODO The m t.</param>
        public GH_MaterialTool([CanBeNull] MaterialTool mT) => this.Value = mT;

        // Copy Constructor (just reference as MaterialTool is Immutable)
        /// <summary>Initializes a new instance of the <see cref="GH_MaterialTool"/> class.</summary>
        /// <param name="mT">TODO The m t.</param>
        public GH_MaterialTool([CanBeNull] GH_MaterialTool mT) => this.Value = mT?.Value;

        /// <inheritdoc />
        [CanBeNull]
        public override IGH_Goo Duplicate() => new GH_MaterialTool(this);

        /// <inheritdoc />
        public override bool CastTo<T>(ref T target)
        {
            // ReSharper disable once InvertIf
            if (typeof(T).IsAssignableFrom(typeof(MaterialTool)))
            {
                object ptr = this.Value;
                target = (T)ptr;
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public override bool CastFrom([CanBeNull] object source)
        {
            switch (source) {
                case null: return false;

                // Cast From Unwrapped MT
                case MaterialTool mT:
                    this.Value = mT;
                    return true;
                default: return false;
            }
        }
    }

    // Grasshopper Parameter Wrapper
    /// <summary>TODO The g h_ material tool par.</summary>
    public class GH_MaterialToolPar : GH_Param<GH_MaterialTool>
    {
        /// <summary>Initializes a new instance of the <see cref="GH_MaterialToolPar"/> class.</summary>
        public GH_MaterialToolPar()
            : base(
                "Material/Tool", "MatTool",
                "Contains a collection of Material Tool Pairs",
                "CAMel", "  Params", GH_ParamAccess.item) { }

        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("147c0724-2d2b-4316-a889-d59fbe748b58");

        /// <inheritdoc />
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.materialtool;
    }
}