using System;
using System.Collections.Generic;
using System.Text;
using CAMel.Types.Machine;
using CAMel.Types.MaterialForm;
using JetBrains.Annotations;
using Rhino.Geometry;

namespace CAMel.Types
{
    // Attach a collection of information
    // to a Code string, including
    // errors, warnings and ranges.

    // TODO update error handling to include error line numbers

    public class CodeInfo
    {
        [NotNull] private readonly StringBuilder _code;
        [NotNull] public Dictionary<string, double> machineState { get; } // Data version of last written instruction to use between write calls
        [NotNull] private readonly Dictionary<string, Interval> _ranges; // Highest and lowest value for each coordinate
        [NotNull] private readonly Dictionary<string, List<int>> _warnings; // Warning text and number of occurrences
        [NotNull] private readonly Dictionary<string, List<int>> _errors; // Error text and number of occurrences
        [NotNull] private readonly IMachine _m; // Machine for language handling.

        [NotNull] public MaterialTool currentMT { get; set; }
        [NotNull] public IMaterialForm currentMF { get; set; }

        private int _lines;

        public CodeInfo([NotNull] IMachine m, [NotNull] IMaterialForm mF, [NotNull] MaterialTool mT)
        {
            this._code = new StringBuilder();
            this._m = m;
            this.currentMF = mF;
            this.currentMT = mT;
            this._ranges = new Dictionary<string, Interval>();
            this._errors = new Dictionary<string, List<int>>();
            this._warnings = new Dictionary<string, List<int>>();
            this.machineState = new Dictionary<string, double>();
            this._lines = 0;
        }

        public void growRange([NotNull] string key, double v)
        {
            if (!this._ranges.ContainsKey(key)) { this._ranges.Add(key, new Interval(v, v)); }
            else
            {
                Interval temp = this._ranges[key];
                temp.Grow(v);
                this._ranges[key] = temp;
            }
        }

        [NotNull, PublicAPI] public Dictionary<string, Interval> getRanges() => this._ranges;

        public void addWarning([NotNull] string warn)
        {
            if (this._warnings.ContainsKey(warn) && this._warnings[warn] != null) { this._warnings[warn].Add(this._lines); }
            else { this._warnings.Add(warn, new List<int> {this._lines}); }
            appendComment(warn);
        }
        public void addError([NotNull] string err)
        {
            if (this._errors.ContainsKey(err) && this._errors[err] != null) { this._errors[err].Add(this._lines); }
            else { this._errors.Add(err, new List<int> {this._lines}); }
            appendComment(err);
        }

        // Checks to see if warnings were reported
        [PublicAPI] public bool hasWarnings() => this._warnings.Count > 0;

        // Checks to see if warnings were reported, or errors on the ignore list
        public bool hasWarnings([CanBeNull] List<string> ignore)
        {
            foreach (string k in this._errors.Keys)
            { if (ignore?.Contains(k) == true) { return true; } }

            return this._warnings.Count > 0;
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
                if (!first) { lN = lN + ", "; }
                first = false;
                lN = lN + i;
            }
            return lN;
        }

        // Return string with warnings and ignored errors
        [NotNull]
        public string getWarnings([CanBeNull] List<string> ignore)
        {
            StringBuilder outP = new StringBuilder();
            if (this._warnings.Count > 0)
            {
                outP.AppendLine("Warnings: (on lines)");
                foreach (string k in this._warnings.Keys)
                { outP.AppendLine(k + ": " + this._warnings[k]?.Count + " (" + lineNumbers(this._warnings[k]) + ")"); }
            }

            // Add ignored errors
            bool first = true;
            foreach (string k in this._errors.Keys)
            {
                if (ignore?.Contains(k) != true) { continue; }
                if (first)
                {
                    first = false;
                    outP.AppendLine("Ignored Errors: (on lines)");
                }
                outP.AppendLine(k + ": " + this._errors[k]?.Count + " (" + lineNumbers(this._errors[k]) + ")");
            }
            return outP.ToString();
        }

        // Checks to see if errors were reported
        [PublicAPI] public bool hasErrors() => this._errors.Count > 0;

        // Checks to see if there are errors, other than those in the ignore list were reported
        public bool hasErrors([CanBeNull] List<string> ignore)
        {
            foreach (string k in this._errors.Keys)
            { if (ignore?.Contains(k) == false) { return true; } }

            return false;
        }

        // return string with all errors
        [NotNull, PublicAPI] public string getErrors() => getErrors(new List<string>());

        // return string listing errors that are not ignored
        [NotNull]
        public string getErrors([CanBeNull] List<string> ignore)
        {
            StringBuilder outP = new StringBuilder();
            bool first = true;

            foreach (string k in this._errors.Keys)
            {
                if (ignore?.Contains(k) != false) { continue; }

                if (first)
                {
                    first = false;
                    outP.AppendLine("Errors: (on lines)");
                }
                outP.AppendLine(k + ": " + this._errors[k]?.Count + " (" + lineNumbers(this._errors[k]) + ")");
            }

            return outP.ToString();
        }

        [NotNull]
        internal string getRangesString()
        {
            string rOut = string.Empty;
            foreach (string k in this._ranges.Keys)
            {
                rOut = rOut + "\n" + k + ": " + this._ranges[k].T0.ToString("0.00") +
                       " to " + this._ranges[k].T1.ToString("0.00");
            }
            return rOut;
        }

        public override string ToString() => this._code.ToString();

        [NotNull]
        public string ToString(int start, int length)
        {
            int uLength;
            if (start + length > this._code.Length) { uLength = this._code.Length - start; }
            else { uLength = length; }

            return uLength > 0 ? this._code.ToString(start, uLength) : string.Empty;
        }

        // ReSharper disable once InconsistentNaming
        public int Length => this._code.Length;

        public void appendLineNoNum([NotNull] string l)
        {
            // Add \r\n manually to ensure consistency of
            // files between OSX and Windows.
            if (l.Length > 0) { this._code.Append(l + "\r\n"); }
        }
        private void appendLine([NotNull] string l)
        {
            if (l.Length <= 0) { return; }

            string line = this._m.lineNumber(l, this._lines);
            this._lines++;
            appendLineNoNum(line);
        }
        public void appendComment([NotNull] string l) => appendLineNoNum(this._m.comment(l));
        public void append([NotNull] string l)
        {
            if (l == string.Empty) { return; }

            char[] seps = {'\n', '\r'};
            string[] lines = l.Split(seps, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines) { appendLine(line); }
        }
    }
}
