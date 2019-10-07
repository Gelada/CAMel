using System;
using CAMel.Types.Machine;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.Types
{
    // Different end mill shapes to consider
    public enum EndShape
    {
        Ball,
        Square,
        V,
        Other,
        Error
    }

    [UsedImplicitly]
    public class MaterialToolBuilder
    {
        public string matName { get; [UsedImplicitly] set; } // Name of the material
        public string toolName { get; [UsedImplicitly] set; } // Name of the tool
        public int toolNumber { get; [UsedImplicitly] set; } // Number of the tool
        public double speed { get; [UsedImplicitly] set; } // speed of spindle (assumed unset for negative values)
        public double feedCut { get; [UsedImplicitly] set; } // feed rate for cutting (assumed unset for negative values)
        public double feedPlunge { get; [UsedImplicitly] set; } // feed rate for plunging (assumed unset for negative values)
        public double cutDepth { get; [UsedImplicitly] set; } // maximum material to cut away (assumed unset for negative values)
        public double finishDepth { get; [UsedImplicitly] set; } // thickness to cut in a finish pass
        public double toolWidth { get; [UsedImplicitly] set; } // width of tool (assumed unset for negative values)
        public double insertWidth { get; [UsedImplicitly] set; } // width needed to insert into material
        public double toolLength { get; [UsedImplicitly] set; } // length from the tip of the tool to the spindle
        public string shape { get; [UsedImplicitly] set; } // End shape of the tool
        public double sideLoad { get; [UsedImplicitly] set; } // Suggested side load for the tool.
        public double tolerance { get; [UsedImplicitly] set; } // The maximum permitted distance of approximation from curve
        public double minStep { get; [UsedImplicitly] set; } // shortest path permitted
        public double pathJump { get; [UsedImplicitly] set; } // maximum jump between toolpaths in material
    }

    // Settings for a particular material and tool
    public class MaterialTool : ICAMelBase
    {
        [NotNull] public string matName { get; } // Name of the materialMaterialToolReader
        [NotNull] public string toolName { get; } // Name of the tool
        public int toolNumber { get; } // Number of the tool
        public double speed { get; } // speed of spindle (assumed unset for negative values)
        public double feedCut { get; } // feed rate for cutting (assumed unset for negative values)
        public double feedPlunge { get; } // feed rate for plunging (assumed unset for negative values)
        public double cutDepth { get; } // maximum material to cut away (assumed unset for negative values)
        public double finishDepth { get; } // thickness to cut in a finish pass
        public double toolWidth { get; } // width of tool (assumed unset for negative values)
        public double insertWidth { get; } // width needed to insert into material
        public double toolLength { get; } // length from the tip of the tool to the spindle
        private EndShape shape { get; } // End shape of the tool
        public double sideLoad { get; } // Suggested side load for the tool.
        public double pathJump { get; } // maximum jump between toolpaths in material

        // settings for curve approximation

        public double tolerance { get; } // The maximum permitted distance of approximation from curve
        public double minStep { get; } // shortest path permitted

        // Adding anything here needs significant support:
        //  Add to MaterialToolBuilder
        //  Add to Constructors
        //  Add to csv mapping
        //  Add to create Material Tool

        // Everything, with defaults
        public MaterialTool([CanBeNull] string mat, [CanBeNull] string tool, int toolN, double speed, double feedCut, double feedPlunge, double cutDepth, double finishDepth = 0, double width = -1, double iWidth = -1, double tL = 0, EndShape eS = EndShape.Other, double tol = 0, double mS = 0, double sideLoad = 0.7, double pathJump = -1.0)
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
            EndShape eS;
            switch (mT.shape)
            {
                case "Ball":
                    eS = EndShape.Ball;
                    break;
                case "Square":
                    eS = EndShape.Square;
                    break;
                case "V":
                    eS = EndShape.V;
                    break;
                case "Other":
                    eS = EndShape.Other;
                    break;
                default:
                    eS = EndShape.Error;
                    break;
            }
            this.shape = eS;
            this.sideLoad = mT.sideLoad;
            this.pathJump = mT.pathJump;
        }

        [NotNull]
        public static MaterialTool changeFinishDepth([NotNull] MaterialTool mT, double fd) => new MaterialTool(
            mT.matName, mT.toolName, mT.toolNumber, mT.speed,
            mT.feedCut, mT.feedPlunge, mT.cutDepth, fd,
            mT.toolWidth, mT.insertWidth, mT.toolLength, mT.shape, mT.tolerance, mT.minStep, mT.sideLoad, mT.pathJump);

        [NotNull] public static readonly MaterialTool Empty = new MaterialTool(null, null, -1, -1, -1, -1, -1, -1);

        public string TypeDescription => "Details of a Material and Tool";

        public string TypeName => "MaterialTool";

        public override string ToString() => "Tool " + this.toolNumber + ", " + this.toolName + " into " + this.matName;

        /// <summary>
        /// Offset toolpoint so that it does not gouge an angled path.
        /// </summary>
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
            osTp.pt += cutOffset(m.toolDir(tP), norm);

            return osTp;
        }
        // Find the path offset so the cutting surface of the tool is on the path
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
                    if (Vector3d.VectorAngle(uDir, uNorm) < .01)
                    {
                        os = new Vector3d(0, 0, 0);
                    }
                    else
                    {
                        // find the normal to the plane give by the tool direction and the norm
                        Vector3d plN = Vector3d.CrossProduct(uNorm, uDir);
                        // Now want a vector on that plane orthogonal to tool direction
                        os = this.toolWidth * Vector3d.CrossProduct(uDir, plN) / 2;
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
    }

    // Grasshopper Type Wrapper
    public sealed class GH_MaterialTool : CAMel_Goo<MaterialTool>
    {
        [UsedImplicitly]
        public GH_MaterialTool() { this.Value = null; }
        // construct from unwrapped type
        public GH_MaterialTool([CanBeNull] MaterialTool mT) { this.Value = mT; }
        // Copy Constructor (just reference as MaterialTool is Immutable)
        public GH_MaterialTool([CanBeNull] GH_MaterialTool mT) { this.Value = mT?.Value; }
        // Duplicate
        [CanBeNull]
        public override IGH_Goo Duplicate() => new GH_MaterialTool(this);

        public override bool CastTo<T>(ref T target)
        {
            // ReSharper disable once InvertIf
            if (typeof(T).IsAssignableFrom(typeof(MaterialTool)))
            {
                object ptr = this.Value;
                target = (T) ptr;
                return true;
            }
            return false;
        }

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
    public class GH_MaterialToolPar : GH_Param<GH_MaterialTool>
    {
        public GH_MaterialToolPar() :
            base("Material/Tool", "MatTool", "Contains a collection of Material Tool Pairs", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid => new Guid("147c0724-2d2b-4316-a889-d59fbe748b58");

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.materialtool;
    }
}