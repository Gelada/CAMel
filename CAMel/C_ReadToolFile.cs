using System;
using System.Collections.Generic;
using System.IO;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
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
            Map(m => m.toolLength).Name("Length");
            Map(m => m.speed).Name("Speed");
            Map(m => m.feedCut).Name("Feed Rate");
            Map(m => m.feedPlunge).Name("Plunge Rate");
            Map(m => m.cutDepth).Name("Cut Depth");
            Map(m => m.finishDepth).Name("Finish Depth");
            Map(m => m.tolerance).Name("Tolerance");
            Map(m => m.minStep).Name("Min Step");
            Map(m => m.shape).TypeConverter<ShapeConverter>();
        }
    }

    public class C_ReadToolFile : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Create3AxisMachine class.
        /// </summary>
        public C_ReadToolFile()
            : base("Read Tool File", "FindMT",
                "Read in a .csv file with tool details",
                "CAMel", " Utilities")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File", "F", "File containing Material Tool Details", GH_ParamAccess.item);
            pManager.AddTextParameter("Material", "M", "Material to cut", GH_ParamAccess.item);
            pManager.AddTextParameter("Tool", "T", "Tool to use", GH_ParamAccess.item);
        }
                

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("MaterialTools", "MT", "List of Material Tool information read from the .csv file", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string matName = string.Empty;
            string toolName = string.Empty;
            string file = "";

            if (!DA.GetData(0, ref file)) { return; }
            if (!DA.GetData(1, ref matName)) { return; }
            if (!DA.GetData(2, ref toolName)) { return; }

            this.Message = Path.GetFileNameWithoutExtension(file);

            List<MaterialToolBuilder> MTBs = new List<MaterialToolBuilder>();

            using (StreamReader fileReader = new StreamReader(file))
            {
                CsvReader csv = new CsvReader(fileReader);
                MTBs = new List<MaterialToolBuilder>();
                csv.Configuration.RegisterClassMap<MaterialToolMap>();
                MTBs.AddRange(csv.GetRecords<MaterialToolBuilder>());
            }

            List<MaterialTool> MTs = new List<MaterialTool>();
            foreach(MaterialToolBuilder MTB in MTBs) { MTs.Add(new MaterialTool(MTB)); }

            bool found = false;
            MaterialTool MT = null;
            for(int i=0; i < MTs.Count; i++)
            {
                if(MTs[i].toolName == toolName && MTs[i].matName == matName)
                {
                    if(found)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "More than one material tool combination found, using first.");
                        break;
                    }
                    else
                    {
                        found = true;
                        MT = MTs[i];
                    }
                }
            }
            if (!found) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No material tool combination found."); }
            else { DA.SetData(0, new GH_MaterialTool(MT)); }
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
                return Properties.Resources.creatematerialtool;
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