using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace CAMel
{
    public class CAMelInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "CAMel";
            }
        }
        public override Bitmap Icon
        {
            get
            {
                //Return a 24x24 pixel bitmap to represent this GHA library.
                return null;
            }
        }
        public override string Description
        {
            get
            {
                //Return a short string describing the purpose of this GHA library.
                return "CAMel: Tools to roll your own CNC solutions";
            }
        }
        public override Guid Id
        {
            get
            {
                return new Guid("78ce1cc3-d79f-48d5-af54-9bb4f794186b");
            }
        }

        public override string AuthorName
        {
            get
            {
                //Return a string identifying you or your company.
                return "Edmund Harriss";
            }
        }
        public override string AuthorContact
        {
            get
            {
                //Return a string representing your preferred contact details.
                return "CAMel@mathematicians.org.uk";
            }
        }
    }
}
