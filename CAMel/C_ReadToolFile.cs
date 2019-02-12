﻿using System;
using System.Collections.Generic;
using System.IO;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;

using CAMel.Types;

using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using CsvHelper;

namespace CAMel
{
    // Mappings for csv helper

    public class ShapeConverter : DefaultTypeConverter
    {
        public override object ConvertFromString(TypeConverterOptions tCO, string text)
        {
            EndShape ES;
            switch (text)
            {
                case "Ball": ES = EndShape.Ball; break;
                case "Square": ES = EndShape.Square; break;
                case "V": ES = EndShape.V; break;
                case "Other": ES = EndShape.Other; break;
                default: ES = EndShape.Error; break;
            }
            return ES;
        }

        public override string ConvertToString(TypeConverterOptions tCO, object value)
        {
            EndShape ES = (EndShape) value;
            string text = "Error";
            switch (ES)
            {
                case EndShape.Ball : text = "Ball"; break;
                case EndShape.Square : text = "Square"; break;
                case EndShape.V : text = "V"; break;
                case EndShape.Other: text = "Other"; break;
                case EndShape.Error: text = "Error"; break;
            }
            return text;
        }
    }

   
    public class MaterialToolMap : CsvClassMap<MaterialToolBuilder>
    {
        public MaterialToolMap()
        {
            this.Map(m => m.matName).Name("Material");
            Map(m => m.toolName).Name("Tool");
            Map(m => m.toolNumber).Name("Tool Number");
            Map(m => m.toolWidth).Name("Tool Width");
            Map(m => m.insertWidth).Name("Insert Width");
            Map(m => m.toolLength).Name("Length");
            Map(m => m.speed).Name("Speed");
            Map(m => m.feedCut).Name("Feed Rate");
            Map(m => m.feedPlunge).Name("Plunge Rate");
            Map(m => m.cutDepth).Name("Cut Depth");
            Map(m => m.finishDepth).Name("Finish Depth");
            Map(m => m.tolerance).Name("Tolerance");
            Map(m => m.minStep).Name("Min Step");
            Map(m => m.shape).Name("Shape");
            Map(m => m.sideLoad).Name("Side Load");
        }
    }

    public class C_ReadToolFile : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_ReadToolFile()
            : base("Read Tool File", "ReadMT",
                "Read in a .csv file with material and tool details",
                "CAMel", " Hardware")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddParameter(new Param_FilePath(),"File", "F", "File containing Material Tool Details", GH_ParamAccess.item);
        }
                

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_MaterialToolPar(), "MaterialTools", "MTs", "All Material Tools from the .csv file", GH_ParamAccess.list);
        }

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();  
            
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string file = string.Empty;

            if (!DA.GetData(0, ref file)) { return; }

            this.Message = Path.GetFileNameWithoutExtension(file);

            var MTBs = new HashSet<MaterialToolBuilder>();

            using (StreamReader fileReader = new StreamReader(file))
            {
                CsvReader csv = new CsvReader(fileReader);
                csv.Configuration.RegisterClassMap<MaterialToolMap>();
                MTBs.UnionWith(csv.GetRecords<MaterialToolBuilder>());
            }

            List<MaterialTool> MTs = new List<MaterialTool>();
            foreach(MaterialToolBuilder MTB in MTBs) { MTs.Add(new MaterialTool(MTB)); }

            DA.SetDataList(0, MTs);
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
                return Properties.Resources.readmaterialtool;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{9496CF36-6030-4680-9DF1-0552C38FAB12}"); }
        }
    }
}