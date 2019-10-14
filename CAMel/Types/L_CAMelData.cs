﻿namespace CAMel.Types
{
    using System;

    using JetBrains.Annotations;

    using Rhino.DocObjects;
    using Rhino.Runtime;

    public static class RhinoObjectExtension
    {
        private const string _KeyN = "95351F55-3489-40D1-BDBA-F49C0B84BDEA";
        private const string _GuidN = "95351F55-3489-40D1-BDBA-F49C0B84BDEF";
        private const string _SideN = "95351F55-3489-40D1-BDBA-F49C0B84BDEG";
        private const string _SeamN = "95351F55-3489-40D1-BDBA-F49C0B84BDEH";

        public static double getKey([CanBeNull] this RhinoObject ro)
        {
            if (ro?.Attributes?.UserDictionary != null &&
                ro.Attributes.UserDictionary.TryGetGuid(_GuidN, out Guid id) &&
                id == ro.Id &&
                ro.Attributes.UserDictionary.TryGetDouble(_KeyN, out double key))
            { return key; }
            return double.NaN;
        }

        public static void setKey([CanBeNull] this RhinoObject ro, double key)
        {
            if (ro?.Attributes?.UserDictionary == null) { return; }

            ro.Attributes.UserDictionary.Set(_KeyN, key);
            ro.Attributes.UserDictionary.Set(_GuidN, ro.Id);
            ro.CommitChanges();
        }
        public static double getSide([CanBeNull] this CommonObject ro)
        {
            if (ro?.UserDictionary != null &&
                ro.UserDictionary.TryGetDouble(_SideN, out double side))
            { return side; }
            return 0;
        }

        public static void setSide([CanBeNull] this CommonObject ro, double value) =>
            ro?.UserDictionary?.Set(_SideN, value);

        public static double getNewSeam([CanBeNull] this CommonObject ro)
        {
            if (ro?.UserDictionary != null &&
                ro.UserDictionary.TryGetDouble(_SeamN, out double seam))
            { return seam; }
            return double.NaN;
        }

        public static void setNewSeam([CanBeNull] this CommonObject ro, double value) =>
            ro?.UserDictionary?.Set(_SeamN, value);
    }
}
