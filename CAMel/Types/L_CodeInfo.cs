// --------------------------------------------------------------------------------------------------------------------
// <copyright file="L_CodeInfo.cs" company="">
//   
// </copyright>
// <summary>
//   Defines the CodeInfo type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace CAMel.Types
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using CAMel.Types.Machine;
    using CAMel.Types.MaterialForm;

    using JetBrains.Annotations;

    using Rhino.Geometry;

    // Attach a collection of information
    // to a Code string, including
    // errors, warnings and ranges.

    /// <summary>TODO The code info.</summary>
    public class CodeInfo
    {
        /// <summary>TODO The code.</summary>
        [NotNull] private readonly StringBuilder code;
        /// <summary>Stores the machine state. A data version of last written instruction to use between write calls</summary>
        [NotNull]
        public Dictionary<string, double> machineState { get; }
        /// <summary>Highest and lowest value for each coordinate.</summary>
        [NotNull]
        private readonly Dictionary<string, Interval> ranges;
        /// <summary> Warning text and number of occurrences. </summary>
        [NotNull]
        private readonly Dictionary<string, List<int>> warnings;
        /// <summary>Error text and number of occurrences. </summary>
        [NotNull]
        private readonly Dictionary<string, List<int>> errors;
        [NotNull]
        private readonly IMachine m; // Machine for language handling.

        [NotNull] public MaterialTool currentMT { get; set; }
        [NotNull] public IMaterialForm currentMF { get; set; }

        private int lines;

        public CodeInfo([NotNull] IMachine m, [NotNull] IMaterialForm mF, [NotNull] MaterialTool mT)
        {
            this.code = new StringBuilder();
            this.m = m;
            this.currentMF = mF;
            this.currentMT = mT;
            this.ranges = new Dictionary<string, Interval>();
            this.errors = new Dictionary<string, List<int>>();
            this.warnings = new Dictionary<string, List<int>>();
            this.machineState = new Dictionary<string, double>();
            this.lines = 0;
        }

        public void growRange([NotNull] string key, double v)
        {
            if (!this.ranges.ContainsKey(key)) { this.ranges.Add(key, new Interval(v, v)); }
            else
            {
                Interval temp = this.ranges[key];
                temp.Grow(v);
                this.ranges[key] = temp;
            }
        }

        [NotNull, PublicAPI] public Dictionary<string, Interval> getRanges() => this.ranges;

        public void addWarning([NotNull] string warn)
        {
            if (this.warnings.ContainsKey(warn) && this.warnings[warn] != null) { this.warnings[warn].Add(this.lines); }
            else { this.warnings.Add(warn, new List<int> {this.lines}); }
            appendComment(warn);
        }
        public void addError([NotNull] string err)
        {
            if (this.errors.ContainsKey(err) && this.errors[err] != null) { this.errors[err].Add(this.lines); }
            else { this.errors.Add(err, new List<int> {this.lines}); }
            appendComment(err);
        }

        // Checks to see if warnings were reported
        [PublicAPI] public bool hasWarnings() => this.warnings.Count > 0;

        // Checks to see if warnings were reported, or errors on the ignore list
        public bool hasWarnings([CanBeNull] List<string> ignore)
        {
            if (this.errors.Keys.Any(k => ignore?.Contains(k) == true)) { return true; }

            return this.warnings.Count > 0;
        }

        // Return string with all warnings
        [NotNull, PublicAPI] public string getWarnings() => getWarnings(new List<string>());

        [NotNull]
        private static string lineNumbers([CanBeNull] List<int> data)
        {
            if (data == null) { return string.Empty; }
            string lN = string.Empty;
            bool first = true;
            foreach (int i in data)
            {
                if (!first) { lN += ", "; }
                first = false;
                lN += i;
            }
            return lN;
        }

        // Return string with warnings and ignored errors
        [NotNull]
        public string getWarnings([CanBeNull] List<string> ignore)
        {
            StringBuilder outP = new StringBuilder();
            if (this.warnings.Count > 0)
            {
                outP.AppendLine("Warnings: (on lines)");
                foreach (string k in this.warnings.Keys)
                { outP.AppendLine(k + ": " + this.warnings[k]?.Count + " (" + lineNumbers(this.warnings[k]) + ")"); }
            }

            // Add ignored errors
            bool first = true;
            foreach (string k in this.errors.Keys.Where(k => ignore?.Contains(k) == true))
            {
                if (first)
                {
                    first = false;
                    outP.AppendLine("Ignored Errors: (on lines)");
                }
                outP.AppendLine(k + ": " + this.errors[k]?.Count + " (" + lineNumbers(this.errors[k]) + ")");
            }
            return outP.ToString();
        }

        // Checks to see if errors were reported
        [PublicAPI] public bool hasErrors() => this.errors.Count > 0;

        // Checks to see if there are errors, other than those in the ignore list were reported
        public bool hasErrors([CanBeNull] List<string> ignore)
        {
            return this.errors.Keys.Any(k => ignore?.Contains(k) == false);
        }

        // return string with all errors
        [NotNull, PublicAPI] public string getErrors() => getErrors(new List<string>());

        // return string listing errors that are not ignored
        [NotNull]
        public string getErrors([CanBeNull] List<string> ignore)
        {
            StringBuilder outP = new StringBuilder();
            bool first = true;

            foreach (string k in this.errors.Keys.Where(k => ignore?.Contains(k) == false))
            {
                if (first)
                {
                    first = false;
                    outP.AppendLine("Errors: (on lines)");
                }
                outP.AppendLine(k + ": " + this.errors[k]?.Count + " (" + lineNumbers(this.errors[k]) + ")");
            }

            return outP.ToString();
        }

        [NotNull]
        internal string getRangesString()
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            return this.ranges.Keys.Aggregate(string.Empty, (current, k) => current + "\n" + k + ": " + this.ranges[k].T0.ToString("0.00") + " to " + this.ranges[k].T1.ToString("0.00"));
        }

        public override string ToString() => this.code.ToString();

        [NotNull]
        public string ToString(int start, int length)
        {
            int uLength;
            if (start + length > this.code.Length) { uLength = this.code.Length - start; }
            else { uLength = length; }

            return uLength > 0 ? this.code.ToString(start, uLength) : string.Empty;
        }

        // ReSharper disable once InconsistentNaming
        public int Length => this.code.Length;

        public void appendLineNoNum([NotNull] string l)
        {
            // Add \r\n manually to ensure consistency of
            // files between OSX and Windows.
            if (l.Length > 0) { this.code.Append(l + "\r\n"); }
        }
        private void appendLine([NotNull] string l)
        {
            if (l.Length <= 0) { return; }

            string line = this.m.lineNumber(l, this.lines);
            this.lines++;
            appendLineNoNum(line);
        }
        public void appendComment([NotNull] string l) => appendLineNoNum(this.m.comment(l));
        public void append([NotNull] string l)
        {
            if (l == string.Empty) { return; }

            char[] seps = {'\n', '\r'};
            string[] newLines = l.Split(seps, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in newLines) { appendLine(line); }
        }
    }
}
