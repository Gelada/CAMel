namespace CAMel.GH
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using CAMel.Types;

    using CsvHelper;
    using CsvHelper.Configuration;

    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Parameters;

    using JetBrains.Annotations;

    // Mappings for csv helper
    /// <inheritdoc />
    /// <summary>TODO The material tool map.</summary>
    [UsedImplicitly]
    public sealed class MaterialToolMap : CsvClassMap<MaterialToolBuilder>
    {
        /// <summary>Initializes a new instance of the <see cref="MaterialToolMap"/> class.</summary>
        public MaterialToolMap()
        {
            this.Map(m => m.matName)?.Name("Material");
            this.Map(m => m.toolName)?.Name("Tool");
            this.Map(m => m.toolNumber)?.Name("Tool Number");
            this.Map(m => m.toolWidth)?.Name("Tool Width");
            this.Map(m => m.insertWidth)?.Name("Insert Width");
            this.Map(m => m.toolLength)?.Name("Length");
            this.Map(m => m.speed)?.Name("Speed");
            this.Map(m => m.feedCut)?.Name("Feed Rate");
            this.Map(m => m.feedPlunge)?.Name("Plunge Rate");
            this.Map(m => m.cutDepth)?.Name("Cut Depth");
            this.Map(m => m.finishDepth)?.Name("Finish Depth");
            this.Map(m => m.tolerance)?.Name("Tolerance");
            this.Map(m => m.minStep)?.Name("Min Step");
            this.Map(m => m.shape)?.Name("Shape");
            this.Map(m => m.sideLoad)?.Name("Side Load");
            this.Map(m => m.pathJump)?.Name("PathJump");
        }
    }

    /// <inheritdoc />
    /// <summary>TODO The c_ read tool file.</summary>
    [UsedImplicitly]
    public class C_ReadToolFile : GH_Component
    {
        /// <inheritdoc />
        /// <summary>
        /// Reads a file of Material Tools
        /// </summary>
        public C_ReadToolFile()
            : base(
                "Read Tool File", "ReadMT",
                "Read in a .csv file with material and tool details",
                "CAMel", " Hardware") { }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams([NotNull] GH_InputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new Param_FilePath(), "File", "F", "File containing Material Tool Details", GH_ParamAccess.item);
        }

        /// <inheritdoc />
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams([NotNull] GH_OutputParamManager pManager)
        {
            if (pManager == null) { throw new ArgumentNullException(); }
            pManager.AddParameter(new GH_MaterialToolPar(), "MaterialTools", "MTs", "All Material Tools from the .csv file", GH_ParamAccess.list);
        }

        /// <inheritdoc />
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="da">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance([NotNull] IGH_DataAccess da)
        {
            if (da == null) { throw new ArgumentNullException(); }

            string file = string.Empty;

            if (!da.GetData(0, ref file)) { return; }

            this.Message = Path.GetFileNameWithoutExtension(file);

            HashSet<MaterialToolBuilder> mTbs = new HashSet<MaterialToolBuilder>();

            using (StreamReader fileReader = new StreamReader(file))
            {
                CsvReader csv = new CsvReader(fileReader);
                if (csv.Configuration != null)
                {
                    csv.Configuration.WillThrowOnMissingField = false;
                    csv.Configuration.RegisterClassMap<MaterialToolMap>();
                    mTbs.UnionWith(csv.GetRecords<MaterialToolBuilder>() ?? new List<MaterialToolBuilder>());
                }
            }

            List<MaterialTool> mTs = mTbs.Select(mTb => new MaterialTool(mTb)).ToList();

            da.SetDataList(0, mTs);
        }

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.readmaterialtool;

        /// <inheritdoc />
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("{9496CF36-6030-4680-9DF1-0552C38FAB12}");
    }
}