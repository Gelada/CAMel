using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using Rhino.Geometry;
using CAMel.Types.MaterialForm;
using CAMel.Types.Machine;

namespace CAMel.Types
{
    // Attach a collection of information
    // to a Code string, including 
    // errors, warnings and ranges.

    // TODO update error handling to include error line numbers

    public class CodeInfo
    {
        private StringBuilder Code;
        public Dictionary<string, double> machineState { get; set; } // Data version of last written instruction to use between write calls
        private Dictionary<string, Interval> ranges; // Highest and lowest value for each coordinate
        private Dictionary<string, List<int>> warnings; // Warning text and number of occurences
        private Dictionary<string, List<int>> errors; // Error text and number of occurences
        private readonly IMachine Mach; // Machine for language handling.

        public MaterialTool currentMT { get; set; }
        public IMaterialForm currentMF { get; set; }
        private int lines;

        public CodeInfo()
        {
            this.Code = new StringBuilder();
            this.ranges = new Dictionary<string,Interval>();
            this.errors = new Dictionary<string,List<int>>();
            this.warnings = new Dictionary<string, List<int>>();
            this.machineState = new Dictionary<string, double>();
            this.lines = 0;
        }

        public CodeInfo(IMachine M)
        {
            this.Code = new StringBuilder();
            this.Mach = M;
            this.ranges = new Dictionary<string, Interval>();
            this.errors = new Dictionary<string, List<int>>();
            this.warnings = new Dictionary<string, List<int>>();
            this.machineState = new Dictionary<string, double>();
            this.lines = 0;
        }

        public void growRange(string key, double V)
        {
            Interval temp;
            if (!this.ranges.ContainsKey(key))
            { this.ranges.Add(key, new Interval(V, V)); }
            else
            {
                temp = this.ranges[key];
                temp.Grow(V);
                this.ranges[key] = temp;
            }
        }

        public Dictionary<string,Interval> getRanges() => this.ranges;

        public bool addWarning(string warn)
        {
            bool newWarning = false;
            if(this.warnings.ContainsKey(warn))
            { this.warnings[warn].Add(this.lines); }
            else
            {
                this.warnings.Add(warn, new List<int>() { this.lines });
                newWarning = true;
            }
            this.appendComment(warn);
            return newWarning;
        }
        public bool addError(string err)
        {
            bool newError = false;
            if (this.errors.ContainsKey(err))
            { this.errors[err].Add(this.lines); }
            else
            {
                this.errors.Add(err, new List<int>() { this.lines});
                newError = true;
            }
            this.appendComment(err);
            return newError;
        }

        // Checks to see if warnings were reported
        public bool hasWarnings() => this.warnings.Count > 0;

        // Checks to see if warnings were reported, or errors on the ignore list
        public bool hasWarnings(List<string> ignore)
        {
            foreach (string k in this.errors.Keys)
            { if (ignore.Contains(k)) { return true; } }

            return this.warnings.Count > 0;
        }

        // Return string with all warnings
        public string getWarnings() => getWarnings(new List<string>());

        private string lineNumbers(List<int> data)
        {
            string lN = string.Empty;
            bool first = true;
            foreach(int i in data)
            {
                if (!first) { lN = lN + ", "; }
                first = false;
                lN = lN + i.ToString();
            }
            return lN;
        }

        // Return string with warnings and ignored errors
        public string getWarnings(List<string> ignore)
        {
            StringBuilder outP = new StringBuilder();
            if (this.warnings.Count > 0)
            {
                outP.AppendLine("Warnings: (on lines)");
                foreach (string k in this.warnings.Keys)
                { outP.AppendLine(k + ": " + this.warnings[k].Count + " (" + lineNumbers(this.warnings[k])+")"); }
            }

            // Add ignored errors
            bool first = true;
            foreach (string k in this.errors.Keys)
            {
                if (ignore.Contains(k))
                {
                    if (first)
                    {
                        first = false;
                        outP.AppendLine("Ignored Errors: (on lines)");
                    }
                    outP.AppendLine(k + ": " + this.errors[k].Count + " (" + lineNumbers(this.errors[k])+")");
                }
            }
            return outP.ToString();
        }

        // Checks to see if errors were reported
        public bool hasErrors()
        {
            return this.errors.Count > 0;
        }

        // Checks to see if there are errors, other than those in the ignore list were reported
        public bool hasErrors(List<string> ignore)
        {
            foreach (string k in this.errors.Keys)
            { if (!ignore.Contains(k)) { return true; } }

            return false;
        }


        // return string with all errors
        public string getErrors() => getErrors(new List<string>());

        // return string listing errors that are not ignored
        public string getErrors(List<string> ignore)
        {
            StringBuilder outP = new StringBuilder();
            bool first = true;

            foreach (string k in this.errors.Keys)
            {
                if (!ignore.Contains(k))
                {
                    if (first)
                    {
                        first = false;
                        outP.AppendLine("Errors: (on lines)");
                    }
                    outP.AppendLine(k + ": " + this.errors[k].Count + " (" + lineNumbers(this.errors[k]) + ")");
                }
            }

            return outP.ToString();
        }

        internal object getRangesString()
        {
            string rOut = string.Empty;
            foreach (string k in this.ranges.Keys)
            {
                rOut = rOut + "\n" + k + ": " + ranges[k].T0.ToString("0.00") +
                    " to " + ranges[k].T1.ToString("0.00");
            }
            return rOut;
        }

        public override string ToString()
        {
            return this.Code.ToString();
        }

        public string ToString(int start, int length)
        {
            int ulength;
            if (start+length > this.Code.Length)
            {
                ulength = this.Code.Length - start;
            } else
            {
                ulength = length;
            }

            if( ulength > 0)
            {
              return this.Code.ToString(start, ulength);
            }
            else{
                return string.Empty;
            }
        }

        public int Length
        {
            get { return this.Code.Length; }
        }
        
        public void appendLineNoNum(string L)
        {
            // Add \r\n manually to ensure consistency of 
            // files between OSX and Windows. 
            if (L.Length > 0) { this.Code.Append(L+ "\r\n"); }  
        }
        public void appendLine(string L)
        {
            string line = L;

            if (L.Length > 0)
            {
                    line = "N" + this.lines.ToString("0000") + "0 " + L;
                    this.lines++;
                this.appendLineNoNum(line);
            }
        }
        public void appendComment(string L)
        {
            this.appendLineNoNum(this.Mach.comment(L));
        }

        public void append(string L)
        {
            if (L != string.Empty)
            {
                char[] seps = { '\n', '\r' };
                String[] Lines = L.Split(seps, StringSplitOptions.RemoveEmptyEntries);

                foreach (String line in Lines)
                {
                    this.appendLine(line);
                }
            }
        }

    }
}
