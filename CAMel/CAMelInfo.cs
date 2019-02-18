using System;
using System.Drawing;

using Grasshopper.Kernel;
using JetBrains.Annotations;

namespace CAMel
{
    [UsedImplicitly]
    public class CAMelInfo : GH_AssemblyInfo
    {
        [NotNull] public override string Name => "CAMel";

        [CanBeNull] public override Bitmap Icon => Properties.Resources.CAMel;

        [NotNull] public override string Description => "CAMel: Tools to roll your own CNC solutions";

        public override Guid Id => new Guid("78ce1cc3-d79f-48d5-af54-9bb4f794186b");

        [NotNull] public override string AuthorName => "Edmund Harriss";

        [NotNull] public override string AuthorContact => "http://www.Camel3d.com";
    }



   [UsedImplicitly]
   public class CAMelCategory : GH_AssemblyPriority
   {

       public override GH_LoadingInstruction PriorityLoad()
       {
           if (Grasshopper.Instances.ComponentServer == null) { throw new ArgumentNullException(); }
           Grasshopper.Instances.ComponentServer.AddCategoryIcon("CAMel", Properties.Resources.BW_CAMel);
           Grasshopper.Instances.ComponentServer.AddCategoryShortName("CAMel", "CMl");
           Grasshopper.Instances.ComponentServer.AddCategorySymbolName("CAMel", 'C');
           return GH_LoadingInstruction.Proceed;
       }
   }
}
