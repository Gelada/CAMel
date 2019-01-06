using System;
using System.Collections;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace CAMel
{
   static class Exceptions
    {
        public static void matToolException() => throw new InvalidOperationException("Attempting to use ToolPath with no Material Tool information.");
        public static void matFormException() => throw new InvalidOperationException("Attempting to use ToolPath with no MaterialForm information.");
        public static void additionsException() => throw new InvalidOperationException("Cannot write Code for toolpaths with unprocessed additions (such as step down or insert and retract moves.\n");
        public static void noToolException() => throw new InvalidOperationException("Tool not found, the list of Material Tools in your machine is either missing the named tool or simply empty.");
    }
}
