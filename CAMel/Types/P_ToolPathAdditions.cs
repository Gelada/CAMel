namespace CAMel.Types
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.Linq;
    using System.Windows.Forms;

    using GH_IO.Serialization;
    using GH_IO.Types;

    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Types;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    // Grasshopper Type Wrapper
    /// <summary>TODO The g h_ tool path additions.</summary>
    public sealed class GH_ToolPathAdditions : CAMel_Goo<ToolPathAdditions>
    {
        // Default Constructor
        // TODO change back to replaceable, but remove replaceable flag when any value is changed.
        /// <summary>Initializes a new instance of the <see cref="GH_ToolPathAdditions"/> class.</summary>
        [UsedImplicitly]
        public GH_ToolPathAdditions() => this.Value = new ToolPathAdditions();

        // Create from unwrapped version
        /// <summary>Initializes a new instance of the <see cref="GH_ToolPathAdditions"/> class.</summary>
        /// <param name="tP">TODO The t p.</param>
        public GH_ToolPathAdditions([CanBeNull] ToolPathAdditions tP) => this.Value = tP;

        // Copy Constructor
        /// <summary>Initializes a new instance of the <see cref="GH_ToolPathAdditions"/> class.</summary>
        /// <param name="tP">TODO The t p.</param>
        public GH_ToolPathAdditions([CanBeNull] GH_ToolPathAdditions tP) => this.Value = tP?.Value;
        /// <inheritdoc />
        public override bool CastFrom([CanBeNull] object source)
        {
            switch (source)
            {
                case null: return false;

                // Cast from unwrapped TPA
                case ToolPathAdditions tPa:
                    this.Value = tPa;
                    return true;
                default: return false;
            }
        }

        /// <inheritdoc />
        public override bool CastTo<T>(ref T target)
        {
            // ReSharper disable once InvertIf
            if (typeof(T).IsAssignableFrom(typeof(ToolPathAdditions)))
            {
                object ptr = this.Value;
                target = (T)ptr;
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        [CanBeNull]
        public override IGH_Goo Duplicate() => new GH_ToolPathAdditions(this);

        /// <inheritdoc />
        [NotNull]
        public override IGH_GooProxy EmitProxy() => new GH_ToolPathAdditionsProxy(this);

        /// <inheritdoc />
        public override bool Read([CanBeNull] GH_IReader reader)
        {
            if (reader == null) { return false; }
            try
            {
                ToolPathAdditions tPa = new ToolPathAdditions();
                if (reader.ItemExists("insert")) { tPa.insert = reader.GetBoolean("insert"); }
                if (reader.ItemExists("retract")) { tPa.retract = reader.GetBoolean("retract"); }
                if (reader.ItemExists("offset"))
                {
                    GH_Point3D pt = reader.GetPoint3D("offset");
                    tPa.offset = (Vector3d)CAMel_Goo.fromIO(pt);
                }

                if (reader.ItemExists("activate")) { tPa.activate = reader.GetInt32("activate"); }
                if (reader.ItemExists("stepDown")) { tPa.stepDown = reader.GetBoolean("stepDown"); }
                if (reader.ItemExists("sdDropStart")) { tPa.sdDropStart = reader.GetBoolean("sdDropStart"); }
                if (reader.ItemExists("sdDropMiddle")) { tPa.sdDropMiddle = reader.GetDouble("sdDropMiddle"); }
                if (reader.ItemExists("sdDropEnd")) { tPa.sdDropEnd = reader.GetBoolean("sdDropEnd"); }
                if (reader.ItemExists("onionCount"))
                {
                    int count = reader.GetInt32("onionCount");
                    tPa.onion = new List<double>();
                    for (int i = 0; i < count; i++)
                    { tPa.onion.Add(reader.GetDouble("onion", i)); }
                }

                if (reader.ItemExists("threeAxisHeightOffset")) { tPa.threeAxisHeightOffset = reader.GetBoolean("threeAxisHeightOffset"); }
                if (reader.ItemExists("tabbing")) { tPa.tabbing = reader.GetBoolean("tabbing"); }
                if (reader.ItemExists("leadCurve"))
                {
                    double val = 0;
                    tPa.leadCurvature =
                        reader.TryGetDouble("leadCurve", ref val)
                            ? val.ToString(CultureInfo.InvariantCulture)
                            : reader.GetString("leadCurve") ?? string.Empty;
                }

                if (reader.ItemExists("machineOptions")) { tPa.machineOptions = reader.GetString("machineOptions") ?? string.Empty; }
                this.Value = tPa;
                bool m = base.Read(reader);

                return m;
            }
            catch (Exception ex) when (ex is OverflowException || ex is InvalidCastException || ex is NullReferenceException)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public override bool Write([CanBeNull] GH_IWriter writer)
        {
            if (this.Value == null || writer == null) { return base.Write(writer); }

            writer.SetBoolean("insert", this.Value.insert);
            writer.SetBoolean("retract", this.Value.retract);
            writer.SetInt32("activate", this.Value.activate);
            writer.SetPoint3D("offset", CAMel_Goo.toIO(this.Value.offset));
            writer.SetBoolean("stepDown", this.Value.stepDown);
            writer.SetBoolean("sdDropStart", this.Value.sdDropStart);
            writer.SetDouble("sdDropMiddle", this.Value.sdDropMiddle);
            writer.SetBoolean("sdDropEnd", this.Value.sdDropEnd);
            for (int i = 0; i < this.Value.onion.Count; i++)
            { writer.SetDouble("onion", i, this.Value.onion[i]); }
            writer.SetInt32("onionCount", this.Value.onion.Count);
            writer.SetBoolean("threeAxisHeightOffset", this.Value.threeAxisHeightOffset);
            writer.SetBoolean("tabbing", this.Value.tabbing);
            writer.SetString("leadCurve", this.Value.leadCurvature);
            writer.SetString("machineOptions", this.Value.machineOptions);

            return base.Write(writer);
        }
    }

    /// <summary>TODO The g h_ tool path additions par.</summary>
    public class GH_ToolPathAdditionsPar : GH_PersistentParam<GH_ToolPathAdditions>
    {
        /// <summary>Initializes a new instance of the <see cref="GH_ToolPathAdditionsPar"/> class.</summary>
        public GH_ToolPathAdditionsPar()
            : base(
                "Tool Path Additions", "ToolPathAdditions",
                "Extra work that a ToolPath can do as it is processed for cutting.",
                "CAMel", "  Params") { }

        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("421A7CE5-4206-4628-964F-1A3810899556");

        /// <inheritdoc />
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.toolpathadditions;

        /// <inheritdoc />
        public override void AppendAdditionalMenuItems([CanBeNull] ToolStripDropDown menu)
        {
            // Do our own thing as we do not really implement
            // set 1 and set multiple.
            this.Menu_AppendWireDisplay(menu);
            this.Menu_AppendDisconnectWires(menu);
            this.Menu_AppendPrincipalParameter(menu);
            this.Menu_AppendReverseParameter(menu);
            this.Menu_AppendFlattenParameter(menu);
            this.Menu_AppendGraftParameter(menu);
            this.Menu_AppendSimplifyParameter(menu);
            Menu_AppendSeparator(menu);
            this.Menu_AppendManageCollection(menu);
            Menu_AppendSeparator(menu);
            this.Menu_AppendDestroyPersistent(menu);
            this.Menu_AppendInternaliseData(menu);
            this.Menu_AppendExtractParameter(menu);
        }

        /// <inheritdoc />
        protected override GH_GetterResult Prompt_Plural(ref List<GH_ToolPathAdditions> values) => GH_GetterResult.success;

        // ReSharper disable once RedundantAssignment
        /// <summary>TODO The prompt_ singular.</summary>
        /// <param name="value">TODO The value.</param>
        /// <returns>The <see cref="GH_GetterResult"/>.</returns>
        protected override GH_GetterResult Prompt_Singular([CanBeNull] ref GH_ToolPathAdditions value)
        {
            // Give a reasonable generic
            value = new GH_ToolPathAdditions(ToolPathAdditions.basicDefault);
            return GH_GetterResult.success;
        }
    }

    /// <summary>TODO The g h_ tool path additions proxy.</summary>
    public class GH_ToolPathAdditionsProxy : GH_GooProxy<GH_ToolPathAdditions>
    {
        /// <summary>Initializes a new instance of the <see cref="GH_ToolPathAdditionsProxy"/> class.</summary>
        /// <param name="obj">TODO The obj.</param>
        public GH_ToolPathAdditionsProxy([CanBeNull] GH_ToolPathAdditions obj)
            : base(obj) { }

        /// <summary>Gets or sets the activate.</summary>
        [Category(" General"),
         Description("Activate the tool, at the start of end of the path (0 off !0 on) or specify the quality of cutting (machine dependant)."),
         DisplayName(" Activate/Quality"), RefreshProperties(RefreshProperties.All),
         UsedImplicitly]
        public int activate
        {
            get => this.Owner?.Value?.activate ?? ToolPathAdditions.basicDefault.activate;
            set
            {
                if (this.Owner == null) { return; }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPathAdditions(); }
                ToolPathAdditions tPa = this.Owner.Value;
                tPa.activate = value;
                this.Owner.Value = tPa;
            }
        }

        /// <summary>Gets or sets a value indicating whether insert.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [Category(" General"),
         Description("Add an insert to the start of the toolpath to beging cutting. "),
         DisplayName(" Insert"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public bool insert
        {
            get => this.Owner?.Value?.insert ?? ToolPathAdditions.basicDefault.insert;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPathAdditions(); }
                ToolPathAdditions tPa = this.Owner.Value;
                tPa.insert = value;
                this.Owner.Value = tPa;
            }
        }

        /// <summary>Gets or sets the lead curve.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [CanBeNull, Category(" General"),
         Description("Curvature on lead in and out, higher values give a tighter turn, use negatives for the inside and positive for outside the curve."),
         DisplayName("Lead Curvature"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public string leadCurve
        {
            get => this.Owner?.Value?.leadCurvature ?? ToolPathAdditions.basicDefault.leadCurvature;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPathAdditions(); }
                ToolPathAdditions tPa = this.Owner.Value;
                tPa.leadCurvature = value ?? string.Empty;
                this.Owner.Value = tPa;
            }
        }

        /// <summary>Gets or sets the machine options.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [CanBeNull, Category(" General"),
         Description("Specific options for a machine, be careful might not be standard between machines."),
         DisplayName("Machine Options"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public string machineOptions
        {
            get => this.Owner?.Value?.machineOptions ?? ToolPathAdditions.basicDefault.machineOptions;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPathAdditions(); }
                ToolPathAdditions tPa = this.Owner.Value;
                tPa.machineOptions = value ?? string.Empty;
                this.Owner.Value = tPa;
            }
        }

        /// <summary>Gets or sets the offset.</summary>
        [Category(" General"),
         Description("Offset, number or vector as x, y, z. Positive number offsets right going anticlockwise of XY plane. For vector, length gives the amount on right, going anticlockwise. "),
         DisplayName(" Offset"), RefreshProperties(RefreshProperties.All), UsedImplicitly, NotNull]
        public string offset
        {
            get
            {
                Vector3d os = this.Owner?.Value?.offset ?? ToolPathAdditions.basicDefault.offset;
                if (os.IsParallelTo(Vector3d.ZAxis, 0.0001) != 0) { return (os * Vector3d.ZAxis).ToString(CultureInfo.InvariantCulture); }
                if (Math.Abs(os.SquareLength) < CAMel_Goo.Tolerance) { return "0"; }
                return os.X + ", " + os.Y + ", " + os.Z;
            }

            set
            {
                if (this.Owner == null) { return; }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPathAdditions(); }
                ToolPathAdditions tPa = this.Owner.Value;
                Vector3d os = tPa.offset;
                string[] split = value.Split(',');
                if (split.Length == 1 && double.TryParse(split[0], out double val)) { os = val * Vector3d.ZAxis; }
                else if (split.Length == 3 && double.TryParse(split[0], out double x) && double.TryParse(split[1], out double y) && double.TryParse(split[2], out double z)) { os = new Vector3d(x, y, z); }
                tPa.offset = os;
                this.Owner.Value = tPa;
            }
        }

        /// <summary>Gets or sets the onion.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [CanBeNull, Category(" Step Down"),
         Description("Height above toolpath to cut the finish path, for onion skinning. Can be a comma separated list. "),
         DisplayName("Onion Skin"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public string onion
        {
            get => CAMel_Goo.doubleToCsv(this.Owner?.Value?.onion ?? ToolPathAdditions.basicDefault.onion, "0.####");
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPathAdditions(); }
                ToolPathAdditions tPa = this.Owner.Value;
                tPa.onion = CAMel_Goo.cSvToDouble(value);
                this.Owner.Value = tPa;
            }
        }

        /// <summary>Gets or sets a value indicating whether retract.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [Category(" General"),
         Description("Add a retract to the end of the toolpath to finish cutting. "),
         DisplayName(" Retract"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public bool retract
        {
            get => this.Owner?.Value?.retract ?? ToolPathAdditions.basicDefault.retract;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPathAdditions(); }
                ToolPathAdditions tPa = this.Owner.Value;
                tPa.retract = value;
                this.Owner.Value = tPa;
            }
        }

        /// <summary>Gets or sets a value indicating whether sd drop end.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [Category(" Step Down"),
         Description("When stepping down drop the end of paths where roughing is complete"),
         DisplayName("Drop End"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public bool sdDropEnd
        {
            get => this.Owner?.Value?.sdDropEnd ?? ToolPathAdditions.basicDefault.sdDropEnd;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPathAdditions(); }
                ToolPathAdditions tPa = this.Owner.Value;
                tPa.sdDropEnd = value;
                this.Owner.Value = tPa;
            }
        }

        /// <summary>Gets or sets the sd drop middle.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [Category(" Step Down"),
         Description("When stepping down drop the middle of paths where roughing is complete, if longer than this. Set as negative for automatic value."),
         DisplayName("Drop Middle"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public double sdDropMiddle
        {
            get => this.Owner?.Value?.sdDropMiddle ?? ToolPathAdditions.basicDefault.sdDropMiddle;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPathAdditions(); }
                ToolPathAdditions tPa = this.Owner.Value;
                tPa.sdDropMiddle = value;
                this.Owner.Value = tPa;
            }
        }

        /// <summary>Gets or sets a value indicating whether sd drop start.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [Category(" Step Down"),
         Description("When stepping down drop the start of paths where roughing is complete."),
         DisplayName("Drop Start"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public bool sdDropStart
        {
            get => this.Owner?.Value?.sdDropStart ?? ToolPathAdditions.basicDefault.sdDropStart;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPathAdditions(); }
                ToolPathAdditions tPa = this.Owner.Value;
                tPa.sdDropStart = value;
                this.Owner.Value = tPa;
            }
        }

        /// <summary>Gets or sets a value indicating whether stepdown.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [Category(" Step Down"),
         Description("Create a sequence of paths stepping down through the material."),
         DisplayName(" Step down"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public bool stepdown
        {
            get => this.Owner?.Value?.stepDown ?? ToolPathAdditions.basicDefault.stepDown;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPathAdditions(); }
                ToolPathAdditions tPa = this.Owner.Value;
                tPa.stepDown = value;
                this.Owner.Value = tPa;
            }
        }

        /// <summary>Gets or sets a value indicating whether tabbing.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [Category(" Tabbing"),
         Description("Add bumps to the cut (mainly useful for cutting 2d parts) NOT IMPLEMENTED"),
         DisplayName("Tabbing"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public bool tabbing
        {
            get => this.Owner?.Value?.tabbing ?? ToolPathAdditions.basicDefault.tabbing;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPathAdditions(); }
                ToolPathAdditions tPa = this.Owner.Value;
                tPa.tabbing = value;
                this.Owner.Value = tPa;
            }
        }

        /// <summary>Gets or sets a value indicating whether three axis height offset.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [Category(" General"),
         Description("Take account of tool width for 3axis cutting, ensuring the path is followed by the active cutting surface of the tool, not just the tip."),
         DisplayName("3Axis Height Offset"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public bool threeAxisHeightOffset
        {
            get => this.Owner?.Value?.threeAxisHeightOffset ?? ToolPathAdditions.basicDefault.threeAxisHeightOffset;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPathAdditions(); }
                ToolPathAdditions tPa = this.Owner.Value;
                tPa.threeAxisHeightOffset = value;
                this.Owner.Value = tPa;
            }
        }
    }

    // Features we might add to the path
    /// <summary>TODO The tool path additions.</summary>
    public class ToolPathAdditions : ICAMelBase
    {
        /// <summary>TODO The replaceable.</summary>
        private bool replaceable;
        /// <summary>TODO The m options.</summary>
        [NotNull] private BasicParser mOptions;
        /// <summary>TODO The insert 1.</summary>
        private bool insert1;
        /// <summary>TODO The offset 1.</summary>
        private Vector3d offset1;
        /// <summary>TODO The onion 1.</summary>
        [NotNull] private List<double> onion1;
        /// <summary>TODO The retract 1.</summary>
        private bool retract1;
        /// <summary>TODO The sd drop end 1.</summary>
        private bool sdDropEnd1;
        /// <summary>TODO The sd drop middle 1.</summary>
        private double sdDropMiddle1;
        /// <summary>TODO The sd drop start 1.</summary>
        private bool sdDropStart1;
        /// <summary>TODO The step down 1.</summary>
        private bool stepDown1;
        /// <summary>TODO The tabbing 1.</summary>
        private bool tabbing1;
        /// <summary>TODO The three axis height offset 1.</summary>
        private bool threeAxisHeightOffset1;
        /// <summary>Initializes a new instance of the <see cref="ToolPathAdditions"/> class.</summary>
        public ToolPathAdditions()
        {
            this.replaceable = false;
            this.insert = false;
            this.retract = false;
            this.offset = new Vector3d(0, 0, 0);
            this.activate = 0;
            this.stepDown = false;
            this.sdDropStart = false;
            this.sdDropMiddle = 0;
            this.sdDropEnd = false;
            this.onion1 = new List<double> { 0 };
            this.threeAxisHeightOffset = false;
            this.tabbing = false;
            this.leadComm = new BpCommand(string.Empty);
            this.mOptions = new BasicParser(string.Empty);
        }

        // Adding anything here needs significant support:
        //  Add to Constructors
        //  Add to defaults
        //  Add checker to .any
        //  Add to replace
        //  Add serialization and deserialization
        //  Add to the proxy editor
        /// <summary>Initializes a new instance of the <see cref="ToolPathAdditions"/> class.</summary>
        /// <param name="tPa">TODO The t pa.</param>
        private ToolPathAdditions([NotNull] ToolPathAdditions tPa)
        {
            this.leadComm = tPa.leadComm;
            this.replaceable = tPa.replaceable;
            this.insert = tPa.insert;
            this.retract = tPa.retract;
            this.offset = tPa.offset;
            this.activate = tPa.activate;
            this.stepDown = tPa.stepDown;
            this.sdDropStart = tPa.sdDropStart;
            this.sdDropMiddle = tPa.sdDropMiddle;
            this.sdDropEnd = tPa.sdDropEnd;
            this.onion1 = new List<double>();
            this.onion.AddRange(tPa.onion);
            this.threeAxisHeightOffset = tPa.threeAxisHeightOffset;
            this.tabbing = tPa.tabbing;
            this.mOptions = tPa.mOptions;
        }

        /// <summary>TODO The basic default.</summary>
        [NotNull]
        public static ToolPathAdditions basicDefault => new ToolPathAdditions
            {
                replaceable = false,
                insert = true,
                retract = true,
                offset = new Vector3d(0, 0, 0),
                activate = 0,
                stepDown = true,
                sdDropStart = true,
                sdDropMiddle = -1,
                sdDropEnd = true,
                onion = new List<double> { 0 },
                threeAxisHeightOffset = false,
                tabbing = false,
                leadComm = new BpCommand(string.Empty),
                machineOptions = string.Empty
            };

        /// <summary>TODO The temp.</summary>
        [NotNull]
        public static ToolPathAdditions temp => new ToolPathAdditions
            {
                replaceable = true,
                insert = false,
                retract = false,
                offset = new Vector3d(0, 0, 0),
                activate = 0,
                stepDown = false,
                sdDropStart = true,
                sdDropMiddle = -1,
                sdDropEnd = true,
                onion = new List<double> { 0 },
                threeAxisHeightOffset = false,
                tabbing = false,
                leadCurvature = string.Empty,
                machineOptions = string.Empty
            };

        /// <summary>TODO The two axis default.</summary>
        [NotNull]
        public static ToolPathAdditions twoAxisDefault => new ToolPathAdditions
            {
                replaceable = false,
                insert = true,
                retract = true,
                offset = new Vector3d(0, 0, 0),
                activate = 1,
                stepDown = false,
                sdDropStart = true,
                sdDropMiddle = -1,
                sdDropEnd = true,
                onion = new List<double> { 0 },
                threeAxisHeightOffset = false,
                tabbing = false,
                leadComm = new BpCommand("U 1"),
                machineOptions = string.Empty
            };

        /// <summary>Gets or sets the activate.</summary>
        public int activate { get; set; }
        /// <summary>TODO The any.</summary>
        public bool any =>
            this.insert ||
            this.retract ||
            this.offset.Length > 0 ||
            this.stepDown ||
            this.threeAxisHeightOffset ||
            this.tabbing ||
            this.onion.Count == 1 && Math.Abs(this.onion[0]) > CAMel_Goo.Tolerance ||
            this.onion.Count > 1 ||
            this.machineOptions != string.Empty;

        /// <summary>Gets or sets a value indicating whether insert.</summary>
        public bool insert
        {
            get => this.insert1;
            set
            {
                this.insert1 = value;
                this.replaceable = false;
            }
        }

        /// <summary>Gets the lead comm.</summary>
        [NotNull]
        public BpCommand leadComm { get; private set; }

        /// <summary>Gets or sets the lead curvature.</summary>
        [NotNull]
        public string leadCurvature // information to create leads
        {
            get => this.leadComm.ToString();
            set
            {
                this.leadComm = new BpCommand(value);
                this.replaceable = false;
            }
        }

        /// <summary>Gets or sets the machine options.</summary>
        [NotNull]
        public string machineOptions
        {
            get => this.mOptions.ToString();
            set
            {
                this.mOptions = new BasicParser(value);
                this.replaceable = false;
            }
        }

        // Quality or activation information for the toolpath.
        /// <summary>Gets or sets the offset.</summary>
        public Vector3d offset
        {
            get => this.offset1;
            set
            {
                this.offset1 = value;
                this.replaceable = false;
            }
        }

        /// <summary>Gets or sets the onion.</summary>
        [NotNull]
        public List<double> onion
        {
            get => this.onion1;
            set
            {
                this.onion1 = value;
                this.replaceable = false;
            }
        }

        /// <summary>Gets or sets a value indicating whether retract.</summary>
        public bool retract
        {
            get => this.retract1;
            set
            {
                this.retract1 = value;
                this.replaceable = false;
            }
        }

        /// <summary>Gets or sets a value indicating whether sd drop end.</summary>
        public bool sdDropEnd
        {
            get => this.sdDropEnd1;
            set
            {
                this.sdDropEnd1 = value;
                this.replaceable = false;
            }
        }

        /// <summary>Gets or sets the sd drop middle.</summary>
        public double sdDropMiddle
        {
            get => this.sdDropMiddle1;
            set
            {
                this.sdDropMiddle1 = value;
                this.replaceable = false;
            }
        }

        /// <summary>Gets or sets a value indicating whether sd drop start.</summary>
        public bool sdDropStart
        {
            get => this.sdDropStart1;
            set
            {
                this.sdDropStart1 = value;
                this.replaceable = false;
            }
        }

        /// <summary>TODO The sort onion.</summary>
        [NotNull]
        // ReSharper disable once ReturnTypeCanBeEnumerable.Global
        public IOrderedEnumerable<double> sortOnion => this.onion.OrderByDescending(d => d);

        // offset plane(normal to vector and amount on the right when turning clockwise.
        /// <summary>Gets or sets a value indicating whether step down.</summary>
        public bool stepDown
        {
            get => this.stepDown1;
            set
            {
                this.stepDown1 = value;
                this.replaceable = false;
            }
        }

        /// <summary>Gets or sets a value indicating whether tabbing.</summary>
        public bool tabbing
        {
            get => this.tabbing1;
            set
            {
                this.tabbing1 = value;
                this.replaceable = false;
            }
        }

        // How stepdown will deal with
        // points that have reached
        // the required depth (Middle is dropped if length greater than value);
        // thicknesses to leave before final cut.
        /// <summary>Gets or sets a value indicating whether three axis height offset.</summary>
        public bool threeAxisHeightOffset
        {
            get => this.threeAxisHeightOffset1;
            set
            {
                this.threeAxisHeightOffset1 = value;
                this.replaceable = false;
            }
        }

        /// <inheritdoc />
        public string TypeDescription => "Features that can be added to a basic ToolPath cut.";

        /// <inheritdoc />
        public string TypeName => "ToolPathAdditions";

        /// <summary>TODO The deep clone.</summary>
        /// <returns>The <see cref="ToolPathAdditions"/>.</returns>
        [NotNull]
        public ToolPathAdditions deepClone() => new ToolPathAdditions(this);

        /// <summary>TODO The replace.</summary>
        /// <param name="tPa">TODO The t pa.</param>
        public void replace([NotNull] ToolPathAdditions tPa)
        {
            if (!this.replaceable) { return; }
            this.replaceable = tPa.replaceable;
            this.insert = tPa.insert;
            this.retract = tPa.retract;
            this.offset = tPa.offset;
            this.activate = tPa.activate;
            this.stepDown = tPa.stepDown;
            this.sdDropStart = tPa.sdDropStart;
            this.sdDropMiddle = tPa.sdDropMiddle;
            this.sdDropEnd = tPa.sdDropEnd;
            this.onion = new List<double>();
            this.onion.AddRange(tPa.onion);
            this.threeAxisHeightOffset = tPa.threeAxisHeightOffset;
            this.tabbing = tPa.tabbing;
            this.leadComm = tPa.leadComm;
            this.machineOptions = tPa.machineOptions;
        }

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        public override string ToString() => "Toolpath Additions";
    }
}