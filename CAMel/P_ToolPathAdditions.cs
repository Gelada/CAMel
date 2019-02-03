using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using System.Linq;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;



namespace CAMel.Types
{
    // Features we might add to the path
    public class ToolPathAdditions : ICAMel_Base
    {
        public bool insert { get; set; }
        public bool retract { get; set; }
        public bool stepDown { get; set; }
        public bool sdDropStart { get; set; }    // How stepdown will deal with 
        public double sdDropMiddle { get; set; } // points that have reached  
        public bool sdDropEnd { get; set; }      // the required depth (Middle is dropped if length greater than value);
        public List<double> onion { get; set; }  // thicknesses to leave before final cut.
        public bool threeAxisHeightOffset { get; set; }
        public bool tabbing { get; set; }        // add tabs if machine wants to.
        public double leadLength { get; set; }   // if leading in or out what factor of standard value to use

        // Adding anything here needs significant support:
        //  Add checker to .any
        //  Add serialization and deserialization
        //  Add to the proxy editor
        //  Add to Constructors
        //  Add to default.

        public ToolPathAdditions() // create the empty addition
        {
            this.insert = false;
            this.retract = false;
            this.stepDown = false;
            this.sdDropStart = false;
            this.sdDropMiddle = 0;
            this.sdDropEnd = false;
            this.threeAxisHeightOffset = false;
            this.tabbing = false;
            this.leadLength = 0;
            this.onion = new List<double>() { 0 };
        }

        private ToolPathAdditions(ToolPathAdditions TPA)
        {
            this.insert = TPA.insert;
            this.retract = TPA.retract;
            this.stepDown = TPA.stepDown;
            this.sdDropStart = TPA.sdDropStart;
            this.sdDropMiddle = TPA.sdDropMiddle;
            this.sdDropEnd = TPA.sdDropEnd;
            this.onion = new List<double>();
            this.onion.AddRange(TPA.onion);
            this.threeAxisHeightOffset = TPA.threeAxisHeightOffset;
            this.tabbing = TPA.tabbing;
            this.leadLength = TPA.leadLength;
        }

        public ToolPathAdditions deepClone() => new ToolPathAdditions(this);

        public static ToolPathAdditions basicDefault => new ToolPathAdditions()
        {
            insert = true,
            retract = true,
            stepDown = true,
            sdDropStart = true,
            sdDropMiddle = -1,
            sdDropEnd = true,
            onion = new List<double>() { 0 },
            threeAxisHeightOffset = false,
            tabbing = false,
            leadLength = 0
        };

        public bool any
        {
            get
            {
                return this.insert ||
                    this.retract ||
                    this.stepDown ||
                    this.threeAxisHeightOffset ||
                    this.tabbing ||
                    this.leadLength != 0 ||
                    (this.onion.Count == 1 && this.onion[0] != 0) ||
                    this.onion.Count > 1;
            }
        }

        public string TypeDescription => "Features that can be added to a basic ToolPath cut.";
        public string TypeName => "ToolPathAdditions";

        public IOrderedEnumerable<double> sortOnion
        {
            get
            {
                return this.onion.OrderByDescending(d => d);
            }
        }

        public override string ToString() => "Toolpath Additons";
    }

    // Grasshopper Type Wrapper
    public class GH_ToolPathAdditions : CAMel_Goo<ToolPathAdditions>
    {
        // Default Constructor
        public GH_ToolPathAdditions() { this.Value = new ToolPathAdditions(); }
        // Create from unwrapped version
        public GH_ToolPathAdditions(ToolPathAdditions TP) { this.Value = TP; }
        // Copy Constructor
        public GH_ToolPathAdditions(GH_ToolPathAdditions TP) { this.Value = TP.Value; }
        // Duplicate
        public override IGH_Goo Duplicate() { return new GH_ToolPathAdditions(this); }

        public override IGH_GooProxy EmitProxy()
        {
            return new GH_ToolPathAdditionsProxy(this);
        }

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            writer.SetBoolean("insert", this.Value.insert);
            writer.SetBoolean("retract", this.Value.retract);
            writer.SetBoolean("stepDown", this.Value.stepDown);
            writer.SetBoolean("sdDropStart", this.Value.sdDropStart);
            writer.SetDouble("sdDropMiddle", this.Value.sdDropMiddle);
            writer.SetBoolean("sdDropEnd", this.Value.sdDropEnd);
            for(int i=0; i< this.Value.onion.Count;i++)
            {writer.SetDouble("onion", i, this.Value.onion[i]);}
            writer.SetInt32("onionCount", this.Value.onion.Count);
            writer.SetBoolean("threeAxisHeightOffset", this.Value.threeAxisHeightOffset);
            writer.SetBoolean("tabbing", this.Value.tabbing);
            writer.SetDouble("leadLength", this.Value.leadLength);
            return base.Write(writer);
        }

        // Deserialize this instance from a Grasshopper reader object.
        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            try
            {
                ToolPathAdditions TPA = new ToolPathAdditions();
                if (reader.ItemExists("insert")) { TPA.insert = reader.GetBoolean("insert"); }
                if (reader.ItemExists("retract")) { TPA.retract = reader.GetBoolean("retract"); }
                if (reader.ItemExists("stepDown")) { TPA.stepDown = reader.GetBoolean("stepDown"); }
                if (reader.ItemExists("sdDropStart")) { TPA.sdDropStart = reader.GetBoolean("sdDropStart"); }
                if (reader.ItemExists("sdDropMiddle")) { TPA.sdDropMiddle = reader.GetDouble("sdDropMiddle"); }
                if (reader.ItemExists("sdDropEnd")) { TPA.sdDropEnd = reader.GetBoolean("sdDropEnd"); }
                if (reader.ItemExists("onionCount"))
                {
                    int Count = reader.GetInt32("onionCount");
                    TPA.onion = new List<double>();
                    for (int i = 0; i < Count; i++)
                    { TPA.onion.Add(reader.GetDouble("onion", i)); }
                }
                if (reader.ItemExists("threeAxisHeightOffset")) { TPA.threeAxisHeightOffset = reader.GetBoolean("threeAxisHeightOffset"); }
                if (reader.ItemExists("tabbing")) { TPA.tabbing = reader.GetBoolean("tabbing"); }
                if (reader.ItemExists("leadLength")) { TPA.leadLength = reader.GetDouble("leadLength"); }
                this.Value = TPA;
                return base.Read(reader);
            }
            catch(Exception ex) when (ex is OverflowException || ex is InvalidCastException || ex is NullReferenceException)
            {
                throw new FormatException("ToolPathAdditions deserialization failed due to badly formatted data.", ex);
            }
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(ToolPathAdditions)))
            {
                object ptr = this.Value;
                target = (Q)ptr;
                return true;
            }
            return false;
        }

        public override bool CastFrom(object source)
        {
            if (source == null) { return false; }
            //Cast from unwrapped TPA
            if (typeof(ToolPathAdditions).IsAssignableFrom(source.GetType()))
            {
                this.Value = (ToolPathAdditions)source;
                return true;
            }

            return false;
        }
        
    }

    // Grasshopper Parameter Wrapper
    public class GH_ToolPathAdditionsPar : GH_PersistentParam<GH_ToolPathAdditions>
    {
        public GH_ToolPathAdditionsPar() :
            base("Tool Path Additions", "ToolPathAdditions", "Extra work that a ToolPath can do as it is proccessed for cutting.", "CAMel", "  Params") { }
        public override Guid ComponentGuid
        {
            get { return new Guid("421A7CE5-4206-4628-964F-1A3810899556"); }
        }
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Properties.Resources.toolpathadditions;
            }
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
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
            GH_DocumentObject.Menu_AppendSeparator(menu);
            this.Menu_AppendManageCollection(menu);
            GH_DocumentObject.Menu_AppendSeparator(menu);
            this.Menu_AppendDestroyPersistent(menu);
            this.Menu_AppendInternaliseData(menu);
            this.Menu_AppendExtractParameter(menu);
        }

        protected override GH_GetterResult Prompt_Plural(ref List<GH_ToolPathAdditions> values)
        {
            values = new List<GH_ToolPathAdditions>();
            return GH_GetterResult.success;
        }

        protected override GH_GetterResult Prompt_Singular(ref GH_ToolPathAdditions value)
        {
            // Give a reasonable generic
            value = new GH_ToolPathAdditions(ToolPathAdditions.basicDefault);
            return GH_GetterResult.success;
        }
    }

    public class GH_ToolPathAdditionsProxy : GH_GooProxy<GH_ToolPathAdditions>
    {
        public GH_ToolPathAdditionsProxy(GH_ToolPathAdditions obj) : base(obj)
        { }

        [Category(" General"), Description("Add an insert to the start of the toolpath to beging cutting. "),DisplayName(" Insert"), RefreshProperties(RefreshProperties.All)]
        public bool insert
        {
            get { return this.Owner.Value.insert; }
            set
            {
                ToolPathAdditions TPA = this.Owner.Value;
                TPA.insert = value;
                this.Owner.Value = TPA;
            }
        }
        [Category(" General"), Description("Add a retract to the end of the toolpath to finish cutting. "), DisplayName(" Retract"), RefreshProperties(RefreshProperties.All)]
        public bool retract
        {
            get { return this.Owner.Value.retract; }
            set
            {
                ToolPathAdditions TPA = this.Owner.Value;
                TPA.retract = value;
                this.Owner.Value = TPA;
            }
        }
        [Category(" Step Down"), Description("Create a sequence of paths stepping down through the material."), DisplayName(" Step down"), RefreshProperties(RefreshProperties.All)]
        public bool stepdown
        {
            get { return this.Owner.Value.stepDown; }
            set
            {
                ToolPathAdditions TPA = this.Owner.Value;
                TPA.stepDown = value;
                this.Owner.Value = TPA;
            }
        }
        [Category(" Step Down"), Description("When stepping down drop the start of paths where roughing is complete."), DisplayName("Drop Start"), RefreshProperties(RefreshProperties.All)]
        public bool sdDropStart
        {
            get { return this.Owner.Value.sdDropStart; }
            set
            {
                ToolPathAdditions TPA = this.Owner.Value;
                TPA.sdDropStart = value;
                this.Owner.Value = TPA;
            }
        }
        [Category(" Step Down"), Description("When stepping down drop the middle of paths where roughing is complete, if longer than this. Set as negative for automatic value."), DisplayName("Drop Middle"), RefreshProperties(RefreshProperties.All)]
        public double sdDropMiddle
        {
            get { return this.Owner.Value.sdDropMiddle; }
            set
            {
                ToolPathAdditions TPA = this.Owner.Value;
                TPA.sdDropMiddle = value;
                this.Owner.Value = TPA;
            }
        }
        [Category(" Step Down"), Description("When stepping down drop the end of paths where roughing is complete"), DisplayName("Drop End"), RefreshProperties(RefreshProperties.All)]
        public bool sdDropEnd
        {
            get { return this.Owner.Value.sdDropEnd; }
            set
            {
                ToolPathAdditions TPA = this.Owner.Value;
                TPA.sdDropEnd = value;
                this.Owner.Value = TPA;
            }
        }

        [Category(" Step Down"), Description("Height above toolpath to cut the finish path, for onion skinning. Can be a comma separated list. "), DisplayName("Onion Skin"), RefreshProperties(RefreshProperties.All)]
        public string onion
        {
            get { return CAMel_Goo.doubleToCSV( this.Owner.Value.onion, "0.####"); }
            set
            {
                ToolPathAdditions TPA = this.Owner.Value;
                TPA.onion = CAMel_Goo.cSVToDouble(value);
                this.Owner.Value = TPA;
            }
        }
        [Category(" General"), Description("Take account of tool width for 3axis cutting, ensuring the path is followed by the active cutting surface of the tool, not just the tip."), DisplayName("3Axis Height Offset"), RefreshProperties(RefreshProperties.All)]
        public bool threeAxisHeightOffset
        {
            get { return this.Owner.Value.threeAxisHeightOffset; }
            set
            {
                ToolPathAdditions TPA = this.Owner.Value;
                TPA.threeAxisHeightOffset = value;
                this.Owner.Value = TPA;
            }
        }
        [Category(" Tabbing"), Description("Add bumps to the cut (mainly useful for cutting 2d parts) NOT IMPLEMENTED"), DisplayName("Tabbing"), RefreshProperties(RefreshProperties.All)]
        public bool tabbing
        {
            get { return this.Owner.Value.tabbing; }
            set
            {
                ToolPathAdditions TPA = this.Owner.Value;
                TPA.tabbing = value;
                this.Owner.Value = TPA;
            }
        }
        [Category(" General"), Description("2d Lead path length for start and end, for systems like Plasma cutting."), DisplayName("Lead Length"), RefreshProperties(RefreshProperties.All)]
        public double leadFactor
        {
            get { return this.Owner.Value.leadLength; }
            set
            {
                ToolPathAdditions TPA = this.Owner.Value;
                TPA.leadLength = value;
                this.Owner.Value = TPA;
            }
        }
    }
}