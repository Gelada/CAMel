using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using JetBrains.Annotations;

namespace CAMel.Types
{
    // Features we might add to the path
    public class ToolPathAdditions : ICAMelBase
    {
        private bool _replaceable;
        public bool insert { get; set; }
        public bool retract { get; set; }
        public int activate { get; set; }
        public bool stepDown { get; set; }
        public bool sdDropStart { get; set; }    // How stepdown will deal with
        public double sdDropMiddle { get; set; } // points that have reached
        public bool sdDropEnd { get; set; }      // the required depth (Middle is dropped if length greater than value);
        [NotNull]
        public List<double> onion { get; set; }  // thicknesses to leave before final cut.
        public bool threeAxisHeightOffset { get; set; }
        public bool tabbing { get; set; }        // add tabs if machine wants to.
        public double leadCurvature { get; set; }   // if leading in or out what factor of standard value to use

        // Adding anything here needs significant support:
        //  Add checker to .any
        //  Add serialization and deserialization
        //  Add to the proxy editor
        //  Add to Constructors
        //  Add to replace
        //  Add to default.

        public ToolPathAdditions() // create the empty addition
        {
            this._replaceable = false;
            this.insert = false;
            this.retract = false;
            this.activate = 0;
            this.stepDown = false;
            this.sdDropStart = false;
            this.sdDropMiddle = 0;
            this.sdDropEnd = false;
            this.threeAxisHeightOffset = false;
            this.tabbing = false;
            this.leadCurvature = 0;
            this.onion = new List<double> { 0 };
        }

        private ToolPathAdditions([NotNull] ToolPathAdditions tPa)
        {
            this._replaceable = tPa._replaceable;
            this.insert = tPa.insert;
            this.retract = tPa.retract;
            this.activate = tPa.activate;
            this.stepDown = tPa.stepDown;
            this.sdDropStart = tPa.sdDropStart;
            this.sdDropMiddle = tPa.sdDropMiddle;
            this.sdDropEnd = tPa.sdDropEnd;
            this.onion = new List<double>();
            this.onion.AddRange(tPa.onion);
            this.threeAxisHeightOffset = tPa.threeAxisHeightOffset;
            this.tabbing = tPa.tabbing;
            this.leadCurvature = tPa.leadCurvature;
        }

        [NotNull]
        public ToolPathAdditions deepClone() => new ToolPathAdditions(this);

        [NotNull]
        public static ToolPathAdditions basicDefault => new ToolPathAdditions
        {
            _replaceable = false,
            insert = true,
            retract = true,
            activate = 0,
            stepDown = true,
            sdDropStart = true,
            sdDropMiddle = -1,
            sdDropEnd = true,
            onion = new List<double> { 0 },
            threeAxisHeightOffset = false,
            tabbing = false,
            leadCurvature = 0
        };

        [NotNull]
        public static ToolPathAdditions twoAxisDefault => new ToolPathAdditions
        {
            _replaceable = false,
            insert = true,
            retract = true,
            activate = 1,
            stepDown = false,
            sdDropStart = true,
            sdDropMiddle = -1,
            sdDropEnd = true,
            onion = new List<double> { 0 },
            threeAxisHeightOffset = false,
            tabbing = false,
            leadCurvature = 1
        };

        [NotNull]
        public static ToolPathAdditions replaceable => new ToolPathAdditions
        {
            _replaceable = true,
            insert = false,
            retract = false,
            activate = 0,
            stepDown = false,
            sdDropStart = true,
            sdDropMiddle = -1,
            sdDropEnd = true,
            onion = new List<double> { 0 },
            threeAxisHeightOffset = false,
            tabbing = false,
            leadCurvature = 0
        };

        public bool any =>
            this.insert ||
            this.retract ||
            this.activate != 0 ||
            this.stepDown ||
            this.threeAxisHeightOffset ||
            this.tabbing ||
            this.onion.Count == 1 && Math.Abs(this.onion[0]) > CAMel_Goo.Tolerance ||
            this.onion.Count > 1;

        public string TypeDescription => "Features that can be added to a basic ToolPath cut.";
        public string TypeName => "ToolPathAdditions";

        public override string ToString() => "Toolpath Additions";

        [NotNull]
        // ReSharper disable once ReturnTypeCanBeEnumerable.Global
        public IOrderedEnumerable<double> sortOnion => this.onion.OrderByDescending(d => d);

        public void replace([NotNull]  ToolPathAdditions tPa)
        {
            if (!this._replaceable) { return; }
            this._replaceable = tPa._replaceable;
            this.insert = tPa.insert;
            this.retract = tPa.retract;
            this.activate = tPa.activate;
            this.stepDown = tPa.stepDown;
            this.sdDropStart = tPa.sdDropStart;
            this.sdDropMiddle = tPa.sdDropMiddle;
            this.sdDropEnd = tPa.sdDropEnd;
            this.onion = new List<double>();
            this.onion.AddRange(tPa.onion);
            this.threeAxisHeightOffset = tPa.threeAxisHeightOffset;
            this.tabbing = tPa.tabbing;
            this.leadCurvature = tPa.leadCurvature;

        }
    }

    // Grasshopper Type Wrapper
    public sealed class GH_ToolPathAdditions : CAMel_Goo<ToolPathAdditions>
    {
        // Default Constructor
        // TODO change back to replaceable, but remove replaceable flag when any value is changed.
        [UsedImplicitly]
        public GH_ToolPathAdditions() { this.Value = new ToolPathAdditions(); }
        // Create from unwrapped version
        public GH_ToolPathAdditions([CanBeNull] ToolPathAdditions tP) { this.Value = tP; }
        // Copy Constructor
        public GH_ToolPathAdditions([CanBeNull] GH_ToolPathAdditions tP) { this.Value = tP?.Value; }
        // Duplicate
        [CanBeNull]
        public override IGH_Goo Duplicate() { return new GH_ToolPathAdditions(this); }

        [NotNull]
        public override IGH_GooProxy EmitProxy() => new GH_ToolPathAdditionsProxy(this);

        public override bool Write([CanBeNull] GH_IWriter writer)
        {
            if (this.Value == null || writer == null) { return base.Write(writer); }

            writer.SetBoolean("insert", this.Value.insert);
            writer.SetBoolean("retract", this.Value.retract);
            writer.SetInt32("activate", this.Value.activate);
            writer.SetBoolean("stepDown", this.Value.stepDown);
            writer.SetBoolean("sdDropStart", this.Value.sdDropStart);
            writer.SetDouble("sdDropMiddle", this.Value.sdDropMiddle);
            writer.SetBoolean("sdDropEnd", this.Value.sdDropEnd);
            for (int i = 0; i < this.Value.onion.Count; i++)
            { writer.SetDouble("onion", i, this.Value.onion[i]); }
            writer.SetInt32("onionCount", this.Value.onion.Count);
            writer.SetBoolean("threeAxisHeightOffset", this.Value.threeAxisHeightOffset);
            writer.SetBoolean("tabbing", this.Value.tabbing);
            writer.SetDouble("leadCurve", this.Value.leadCurvature);

            return base.Write(writer);
        }

        // Deserialize this instance from a Grasshopper reader object.
        public override bool Read([CanBeNull] GH_IReader reader)
        {
            if (reader == null) { return false; }
            try
            {
                ToolPathAdditions tPa = new ToolPathAdditions();
                if (reader.ItemExists("insert")) { tPa.insert = reader.GetBoolean("insert"); }
                if (reader.ItemExists("retract")) { tPa.retract = reader.GetBoolean("retract"); }
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
                if (reader.ItemExists("leadCurve")) { tPa.leadCurvature = reader.GetDouble("leadCurve"); }
                this.Value = tPa;
                return base.Read(reader);
            }
            catch(Exception ex) when (ex is OverflowException || ex is InvalidCastException || ex is NullReferenceException)
            {
                return false;
            }
        }

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

        public override bool CastFrom([CanBeNull] object source)
        {
            switch (source) {
                case null: return false;
                //Cast from unwrapped TPA
                case ToolPathAdditions tPa:
                    this.Value = tPa;
                    return true;
                default: return false;
            }
        }

    }

    // Grasshopper Parameter Wrapper
    public class GH_ToolPathAdditionsPar : GH_PersistentParam<GH_ToolPathAdditions>
    {
        public GH_ToolPathAdditionsPar() :
            base("Tool Path Additions",
                "ToolPathAdditions",
                "Extra work that a ToolPath can do as it is processed for cutting.",
                "CAMel",
                "  Params") { }
        public override Guid ComponentGuid => new Guid("421A7CE5-4206-4628-964F-1A3810899556");

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        // You can add image files to your project resources and access them like this:
        // return Resources.IconForThisComponent;
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.toolpathadditions;

        public override void AppendAdditionalMenuItems([CanBeNull] ToolStripDropDown menu)
        {
            // Do our own thing as we do not really implement
            // set 1 and set multiple.

            Menu_AppendWireDisplay(menu);
            Menu_AppendDisconnectWires(menu);
            Menu_AppendPrincipalParameter(menu);
            Menu_AppendReverseParameter(menu);
            Menu_AppendFlattenParameter(menu);
            Menu_AppendGraftParameter(menu);
            Menu_AppendSimplifyParameter(menu);
            Menu_AppendSeparator(menu);
            Menu_AppendManageCollection(menu);
            Menu_AppendSeparator(menu);
            Menu_AppendDestroyPersistent(menu);
            Menu_AppendInternaliseData(menu);
            Menu_AppendExtractParameter(menu);
        }

        protected override GH_GetterResult Prompt_Plural(ref List<GH_ToolPathAdditions> values)
        {
            return GH_GetterResult.success;
        }

        // ReSharper disable once RedundantAssignment
        protected override GH_GetterResult Prompt_Singular([CanBeNull] ref GH_ToolPathAdditions value)
        {
            // Give a reasonable generic
            value = new GH_ToolPathAdditions(ToolPathAdditions.basicDefault);
            return GH_GetterResult.success;
        }
    }

    public class GH_ToolPathAdditionsProxy : GH_GooProxy<GH_ToolPathAdditions>
    {
        public GH_ToolPathAdditionsProxy([CanBeNull] GH_ToolPathAdditions obj) : base(obj)
        { }

        [Category(" General"), Description("Add an insert to the start of the toolpath to beging cutting. "),DisplayName(" Insert"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
        public bool insert
        {
            get => this.Owner?.Value?.insert ?? ToolPathAdditions.basicDefault.insert;
            set
            {
                if(this.Owner == null) {  throw new NullReferenceException();}
                if(this.Owner.Value == null) { this.Owner.Value=new ToolPathAdditions();}
                ToolPathAdditions tPa = this.Owner.Value;
                tPa.insert = value;
                this.Owner.Value = tPa;
            }
        }
        [Category(" General"), Description("Add a retract to the end of the toolpath to finish cutting. "), DisplayName(" Retract"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
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
        [Category(" General"), Description("Activate the tool, at the start of end of the path (0 off !0 on) or specify the quality of cutting (machine dependant)."), DisplayName(" Activate/Quality"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
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
        [Category(" Step Down"), Description("Create a sequence of paths stepping down through the material."), DisplayName(" Step down"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
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
        [Category(" Step Down"), Description("When stepping down drop the start of paths where roughing is complete."), DisplayName("Drop Start"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
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
        [Category(" Step Down"), Description("When stepping down drop the middle of paths where roughing is complete, if longer than this. Set as negative for automatic value."), DisplayName("Drop Middle"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
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
        [Category(" Step Down"), Description("When stepping down drop the end of paths where roughing is complete"), DisplayName("Drop End"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
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

        [CanBeNull]
        [Category(" Step Down"), Description("Height above toolpath to cut the finish path, for onion skinning. Can be a comma separated list. "), DisplayName("Onion Skin"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
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
        [Category(" General"), Description("Take account of tool width for 3axis cutting, ensuring the path is followed by the active cutting surface of the tool, not just the tip."), DisplayName("3Axis Height Offset"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
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
        [Category(" Tabbing"), Description("Add bumps to the cut (mainly useful for cutting 2d parts) NOT IMPLEMENTED"), DisplayName("Tabbing"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
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
        [Category(" General"), Description("Curvature on lead in and out, higher values give a tighter turn, use negatives for the inside and positive for outside the curve."), DisplayName("Lead Curvature"), RefreshProperties(RefreshProperties.All)]
        [UsedImplicitly]
        public double leadCurve
        {
            get => this.Owner?.Value?.leadCurvature ?? ToolPathAdditions.basicDefault.leadCurvature;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPathAdditions(); }
                ToolPathAdditions tPa = this.Owner.Value;
                tPa.leadCurvature = value;
                this.Owner.Value = tPa;
            }
        }
    }
}