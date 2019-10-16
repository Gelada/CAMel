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
        /// <summary>Gets the machine state. A data version of last written instruction to use between write calls</summary>
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
        /// <summary>TODO The m.</summary>
        [NotNull]
        private readonly IMachine m; // Machine for language handling.

        /// <summary>Gets or sets the current mt.</summary>
        [NotNull]
        public MaterialTool currentMT { get; set; }
        /// <summary>Gets or sets the current mf.</summary>
        [NotNull]
        public IMaterialForm currentMF { get; set; }

        /// <summary>TODO The lines.</summary>
        private int lines;

        /// <summary>Initializes a new instance of the <see cref="CodeInfo"/> class.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="mF">TODO The m f.</param>
        /// <param name="mT">TODO The m t.</param>
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

        /// <summary>TODO The grow range.</summary>
        /// <param name="key">TODO The key.</param>
        /// <param name="v">TODO The v.</param>
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

        /// <summary>TODO The get ranges.</summary>
        /// <returns>The <see>
        ///         <cref>Dictionary</cref>
        ///     </see>
        /// .</returns>
        [NotNull, PublicAPI]
        public Dictionary<string, Interval> getRanges() => this.ranges;

        /// <summary>TODO The add warning.</summary>
        /// <param name="warn">TODO The warn.</param>
        public void addWarning([NotNull] string warn)
        {
            if (this.warnings.ContainsKey(warn) && this.warnings[warn] != null) { this.warnings[warn].Add(this.lines); }
            else { this.warnings.Add(warn, new List<int> { this.lines }); }
            this.appendComment(warn);
        }

        /// <summary>TODO The add error.</summary>
        /// <param name="err">TODO The err.</param>
        public void addError([NotNull] string err)
        {
            if (this.errors.ContainsKey(err) && this.errors[err] != null) { this.errors[err].Add(this.lines); }
            else { this.errors.Add(err, new List<int> { this.lines }); }
            this.appendComment(err);
        }

        // Checks to see if warnings were reported
        /// <summary>TODO The has warnings.</summary>
        /// <returns>The <see cref="bool"/>.</returns>
        [PublicAPI]
        public bool hasWarnings() => this.warnings.Count > 0;

        // Checks to see if warnings were reported, or errors on the ignore list
        /// <summary>TODO The has warnings.</summary>
        /// <param name="ignore">TODO The ignore.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public bool hasWarnings([CanBeNull] List<string> ignore)
        {
            if (this.errors.Keys.Any(k => ignore?.Contains(k) == true)) { return true; }

            return this.warnings.Count > 0;
        }

        // Return string with all warnings
        /// <summary>TODO The get warnings.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        [NotNull, PublicAPI]
        public string getWarnings() => this.getWarnings(new List<string>());

        /// <summary>TODO The line numbers.</summary>
        /// <param name="data">TODO The data.</param>
        /// <returns>The <see cref="string"/>.</returns>
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
        /// <summary>TODO The get warnings.</summary>
        /// <param name="ignore">TODO The ignore.</param>
        /// <returns>The <see cref="string"/>.</returns>
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
        /// <summary>TODO The has errors.</summary>
        /// <returns>The <see cref="bool"/>.</returns>
        [PublicAPI]
        public bool hasErrors() => this.errors.Count > 0;

        // Checks to see if there are errors, other than those in the ignore list were reported
        /// <summary>TODO The has errors.</summary>
        /// <param name="ignore">TODO The ignore.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public bool hasErrors([CanBeNull] List<string> ignore)
        {
            return this.errors.Keys.Any(k => ignore?.Contains(k) == false);
        }

        // return string with all errors
        /// <summary>TODO The get errors.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        [NotNull, PublicAPI]
        public string getErrors() => this.getErrors(new List<string>());

        // return string listing errors that are not ignored
        /// <summary>TODO The get errors.</summary>
        /// <param name="ignore">TODO The ignore.</param>
        /// <returns>The <see cref="string"/>.</returns>
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

        /// <summary>TODO The get ranges string.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        [NotNull]
        internal string getRangesString()
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            return this.ranges.Keys.Aggregate(string.Empty, (current, k) => current + "\n" + k + ": " + this.ranges[k].T0.ToString("0.00") + " to " + this.ranges[k].T1.ToString("0.00"));
        }

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        public override string ToString() => this.code.ToString();

        /// <summary>TODO The to string.</summary>
        /// <param name="start">TODO The start.</param>
        /// <param name="length">TODO The length.</param>
        /// <returns>The <see cref="string"/>.</returns>
        [NotNull]
        public string ToString(int start, int length)
        {
            int uLength;
            if (start + length > this.code.Length) { uLength = this.code.Length - start; }
            else { uLength = length; }

            return uLength > 0 ? this.code.ToString(start, uLength) : string.Empty;
        }

        // ReSharper disable once InconsistentNaming
        /// <summary>TODO The length.</summary>
        public int Length => this.code.Length;

        /// <summary>TODO The append line no num.</summary>
        /// <param name="l">TODO The l.</param>
        public void appendLineNoNum([NotNull] string l)
        {
            // Add \r\n manually to ensure consistency of
            // files between OSX and Windows.
            if (l.Length > 0) { this.code.Append(l + "\r\n"); }
        }

        /// <summary>TODO The append line.</summary>
        /// <param name="l">TODO The l.</param>
        private void appendLine([NotNull] string l)
        {
            if (l.Length <= 0) { return; }

            string line = this.m.lineNumber(l, this.lines);
            this.lines++;
            this.appendLineNoNum(line);
        }

        /// <summary>TODO The append comment.</summary>
        /// <param name="l">TODO The l.</param>
        public void appendComment([NotNull] string l) => this.appendLineNoNum(this.m.comment(l));
        /// <summary>TODO The append.</summary>
        /// <param name="l">TODO The l.</param>
        public void append([NotNull] string l)
        {
            if (l == string.Empty) { return; }

            char[] seps = { '\n', '\r' };
            string[] newLines = l.Split(seps, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in newLines) { this.appendLine(line); }
        }
    }
}
