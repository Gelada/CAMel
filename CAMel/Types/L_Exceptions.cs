namespace CAMel.Types
{
    using System;

    using JetBrains.Annotations;

    /// <summary>TODO The exceptions.</summary>
    internal static class Exceptions
    {
        /// <summary>TODO The mat tool exception.</summary>
        [ContractAnnotation("=> halt")]
        public static void matToolException() => throw new InvalidOperationException("Attempting to use ToolPath with no Material Tool information.");
        /// <summary>TODO The mat form exception.</summary>
        [ContractAnnotation("=> halt")]
        public static void matFormException() => throw new InvalidOperationException("Attempting to use ToolPath with no MaterialForm information.");
        /// <summary>TODO The additions exception.</summary>
        [ContractAnnotation("=> halt")]
        public static void additionsException() => throw new InvalidOperationException("Cannot write Code for toolpaths with unprocessed additions (such as step down or insert and retract moves.)");
        /// <summary>TODO The transition exception.</summary>
        [ContractAnnotation("=> halt")]
        public static void transitionException() => throw new InvalidOperationException("Transition called between points in material. ");
        /// <summary>TODO The empty path exception.</summary>
        [ContractAnnotation("=> halt")]
        public static void emptyPathException() => throw new InvalidOperationException("Attempting to use a ToolPath with no points.");
        /// <summary>TODO The no tool exception.</summary>
        [ContractAnnotation("=> halt")]
        public static void noToolException() => throw new InvalidOperationException("Tool not found, the list of Material Tools in your machine is either missing the named tool or simply empty.");
        /// <summary>TODO The no tool path exception.</summary>
        [ContractAnnotation("=> halt")]
        public static void noToolPathException() => throw new InvalidOperationException("No ToolPaths found in Machine Operation.");
        /// <summary>TODO The null panic.</summary>
        [ContractAnnotation("=> halt")]
        public static void nullPanic() => throw new NullReferenceException("Something horrible went wrong, creating a null in CAMel. ");
        /// <summary>TODO The bad surface path.</summary>
        [ContractAnnotation("=> halt")]
        public static void badSurfacePath() => throw new ArgumentException("Bad SurfacePath.");
        /// <summary>TODO The bad surface path.</summary>
        [ContractAnnotation("=> halt")]
        public static void badSurfacePathMesh() => throw new InvalidOperationException("Errors encountered in the surfacePath mesh");
        /// <summary>TODO The bad surface path.</summary>
        [ContractAnnotation("=> halt")]
        public static void materialDirectionPreciseException() => throw new NotSupportedException("Precise edging cannot be used with materialForm direction methods.");
        [ContractAnnotation("=> halt")]
        internal static void noBoundaryPreciseException() => throw new NotSupportedException("Surface or mesh must have boundary.");
        [ContractAnnotation("=> halt")]
        internal static void multipleBoundaryPreciseException() => throw new NotImplementedException("Cannot currently process surfaces with holes.");
    }
}
