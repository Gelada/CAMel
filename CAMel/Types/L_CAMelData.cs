namespace CAMel.Types
{
    using System;

    using JetBrains.Annotations;

    using Rhino.DocObjects;
    using Rhino.Runtime;

    /// <summary>TODO The rhino object extension.</summary>
    public static class RhinoObjectExtension
    {
        /// <summary>TODO The key n.</summary>
        private const string KeyN = "95351F55-3489-40D1-BDBA-F49C0B84BDEA";
        /// <summary>TODO The guid n.</summary>
        private const string GuidN = "95351F55-3489-40D1-BDBA-F49C0B84BDEF";
        /// <summary>TODO The side n.</summary>
        private const string SideN = "95351F55-3489-40D1-BDBA-F49C0B84BDEG";
        /// <summary>TODO The seam n.</summary>
        private const string SeamN = "95351F55-3489-40D1-BDBA-F49C0B84BDEH";
        /// <summary>TODO The depth n.</summary>
        private const string DepthN = "95351F55-3489-40D1-BDBA-F49C0B84BDEI";

        /// <summary>TODO The get key.</summary>
        /// <param name="ro">TODO The ro.</param>
        /// <returns>The <see cref="double"/>.</returns>
        public static double getKey([CanBeNull] this RhinoObject ro)
        {
            if (ro?.Attributes?.UserDictionary != null &&
                ro.Attributes.UserDictionary.TryGetGuid(GuidN, out Guid id) &&
                id == ro.Id &&
                ro.Attributes.UserDictionary.TryGetDouble(KeyN, out double key))
            { return key; }
            return double.NaN;
        }

        /// <summary>TODO The set key.</summary>
        /// <param name="ro">TODO The ro.</param>
        /// <param name="key">TODO The key.</param>
        public static void setKey([CanBeNull] this RhinoObject ro, double key)
        {
            if (ro?.Attributes?.UserDictionary == null) { return; }

            ro.Attributes.UserDictionary.Set(KeyN, key);
            ro.Attributes.UserDictionary.Set(GuidN, ro.Id);
            ro.CommitChanges();
        }

        /// <summary>TODO The get side.</summary>
        /// <param name="ro">TODO The ro.</param>
        /// <returns>The <see cref="double"/>.</returns>
        public static double getSide([CanBeNull] this CommonObject ro)
        {
            if (ro?.UserDictionary != null &&
                ro.UserDictionary.TryGetDouble(SideN, out double side))
            { return side; }
            return 0;
        }

        /// <summary>TODO The set side.</summary>
        /// <param name="ro">TODO The ro.</param>
        /// <param name="value">TODO The value.</param>
        public static void setSide([CanBeNull] this CommonObject ro, double value) =>
            ro?.UserDictionary?.Set(SideN, value);

        /// <summary>TODO The get depth.</summary>
        /// <param name="ro">TODO The ro.</param>
        /// <returns>The <see cref="double"/>.</returns>
        public static double getDepth([CanBeNull] this CommonObject ro)
        {
            if (ro?.UserDictionary != null &&
                ro.UserDictionary.TryGetDouble(DepthN, out double depth))
            { return depth; }
            return double.NaN;
        }

        /// <summary>TODO The set depth.</summary>
        /// <param name="ro">TODO The ro.</param>
        /// <param name="value">TODO The value.</param>
        public static void setDepth([CanBeNull] this CommonObject ro, double value) =>
            ro?.UserDictionary?.Set(DepthN, value);

        /// <summary>TODO The get new seam.</summary>
        /// <param name="ro">TODO The ro.</param>
        /// <returns>The <see cref="double"/>.</returns>
        public static double getNewSeam([CanBeNull] this CommonObject ro)
        {
            if (ro?.UserDictionary != null &&
                ro.UserDictionary.TryGetDouble(SeamN, out double seam))
            { return seam; }
            return double.NaN;
        }

        /// <summary>TODO The set new seam.</summary>
        /// <param name="ro">TODO The ro.</param>
        /// <param name="value">TODO The value.</param>
        public static void setNewSeam([CanBeNull] this CommonObject ro, double value) =>
            ro?.UserDictionary?.Set(SeamN, value);
    }
}