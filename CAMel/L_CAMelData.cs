using System;
//using System.Runtime.InteropServices;

using Rhino.DocObjects;
//using Rhino.DocObjects.Custom;
using Rhino.Runtime;
//using Rhino.Collections;
//using Rhino.FileIO;

namespace CAMel.Types
{
    public static class RhinoObjectExtension
    {
        const string _keyN = "95351F55-3489-40D1-BDBA-F49C0B84BDEA";
        const string _guidN = "95351F55-3489-40D1-BDBA-F49C0B84BDEF";

        public static double getKey (this RhinoObject ro)
        {
            Guid id;
            double key;
            if (ro != null &&
                ro.Attributes.UserDictionary.TryGetGuid(_guidN, out id) &&
                id == ro.Id &&
                ro.Attributes.UserDictionary.TryGetDouble(_keyN, out key))
            { return key; }

            return double.NaN;
        }

        public static double getKey(this CommonObject ro)
        {
            double key;
            if (ro != null &&
                ro.UserDictionary.TryGetDouble(_keyN, out key))
            { return key; }

            return double.NaN;
        }

        public static bool setKey(this RhinoObject ro, double key)
        {
            if (ro != null)
            {
                ro.Attributes.UserDictionary.Set(_keyN, key);
                ro.Attributes.UserDictionary.Set(_guidN, ro.Id);
                ro.CommitChanges();
                return true;
            }
            return false;
        }
        public static bool setKey(this CommonObject ro, double key)
        {
            if (ro != null)
            {
                ro.UserDictionary.Set(_keyN, key);
                return true;
            }
            return false;
        }

    }

    // Not working outside of a Rhino plugin

    // You must define a Guid attribute for your user data derived class
    // in order to support serialization. Every custom user data class
    // needs a custom Guid
    /*[Guid("95351F55-3489-40D1-BDBA-F49C0B84BDEA")]
    public class CAMelData : UserData
    {
        public double key { get; set; }
        
        // Your UserData class must have a public parameterless constructor
        public CAMelData()
        {
            this.key = double.NaN;
        }

        public CAMelData(double key)
        {
            this.key = key;
        }

        public override string Description
        {
            get { return "CAMel Custom Data"; }
        }

        public override string ToString()
        {
            return String.Format("Ordering key={0}", key);
        }

        protected override void OnDuplicate(UserData source)
        {
            this.key = Double.NaN;
        }

        // return true if you have information to save
        public override bool ShouldWrite
        {
            get
            {
                if( double.IsNaN(this.key)) { return false; }
                return true;
            }
        }

        protected override bool Read(BinaryArchiveReader archive)
        {
            ArchivableDictionary dict = archive.ReadDictionary();
            if (dict.ContainsKey("CAMelKey"))
            {
                this.key = (int)dict["CAMelKey"];;
            }
            return true;
        }
        protected override bool Write(BinaryArchiveWriter archive)
        {
            // you can implement File IO however you want... but the dictionary class makes
            // issues like versioning in the 3dm file a bit easier.  If you didn't want to use
            // the dictionary for writing, your code would look something like.
            //
            //  archive.Write3dmChunkVersion(1, 0);
            //  archive.WriteInt(Weight);
            //  archive.WriteDouble(Density);
            var dict = new ArchivableDictionary(1, "CAMel");
            dict.Set("CAMelKey", key);
            archive.WriteDictionary(dict);
            return true;
        }
    }*/
}
