using System;
using JetBrains.Annotations;

namespace CAMel.Types
{
    internal static class Exceptions
    {
        [ContractAnnotation("=> halt")]
        public static void matToolException() => throw new InvalidOperationException("Attempting to use ToolPath with no Material Tool information.");
        [ContractAnnotation("=> halt")]
        public static void matFormException() => throw new InvalidOperationException("Attempting to use ToolPath with no MaterialForm information.");
        [ContractAnnotation("=> halt")]
        public static void additionsException() => throw new InvalidOperationException("Cannot write Code for toolpaths with unprocessed additions (such as step down or insert and retract moves.)");
        [ContractAnnotation("=> halt")]
        public static void transitionException() => throw new InvalidOperationException("Transition called between points in material. ");
        [ContractAnnotation("=> halt")]
        public static void emptyPathException() => throw new InvalidOperationException("Attempting to use a ToolPath with no points.");
        [ContractAnnotation("=> halt")]
        public static void noToolException() => throw new InvalidOperationException("Tool not found, the list of Material Tools in your machine is either missing the named tool or simply empty.");
        [ContractAnnotation("=> halt")]
        public static void noToolPathException() => throw new InvalidOperationException("No ToolPaths found in Machine Operation.");
        [ContractAnnotation("=> halt")]
        public static void nullPanic() => throw new NullReferenceException("Something horrible went wrong, creating a null in CAMel. ");
    }
}
