﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;

namespace CAMel.Types
{
    // Attach a collection of information
    // to a Code string, including 
    // errors, warnings and ranges.

    // TODO update error handling to include error line numbers

    public class CodeInfo
    {
        private StringBuilder Code;
        public Dictionary<string, double> MachineState; // Data version of last written instruction to use between write calls
        private Dictionary<string, Interval> Ranges; // Highest and lowest value for each coordinate
        private Dictionary<string, int> Warnings; // Warning text and number of occurences
        private Dictionary<string, int> Errors; // Error text and number of occurences
        private Machine Mach; // Machine for language handling.
        private int lines;

        public CodeInfo()
        {
            this.Code = new StringBuilder();
            this.Mach = new Machine();
            this.Ranges = new Dictionary<string,Interval>();
            this.Errors = new Dictionary<string,int>();
            this.Warnings = new Dictionary<string, int>();
            this.MachineState = new Dictionary<string, double>();
            this.lines = 0;
        }

        public CodeInfo(Machine M)
        {
            this.Code = new StringBuilder();
            this.Mach = M;
            this.Ranges = new Dictionary<string, Interval>();
            this.Errors = new Dictionary<string, int>();
            this.Warnings = new Dictionary<string, int>();
            this.MachineState = new Dictionary<string, double>();
            this.lines = 0;
        }

        public void GrowRange(string key, double V)
        {
            Interval temp;
            if (!this.Ranges.ContainsKey(key))
            {
                this.Ranges.Add(key, new Interval(V, V));
            }
            else
            {
                temp = this.Ranges[key];
                temp.Grow(V);
                this.Ranges[key] = temp;
            }
        }

        public Dictionary<string,Interval> GetRanges()
        {
            return this.Ranges;
        }

        // TODO Print Ranges 

        public bool AddWarning(string warn)
        {
            bool newWarning = false;
            if(this.Warnings.ContainsKey(warn))
            {
                this.Warnings[warn]++;
            }
            else
            {
                this.Warnings.Add(warn, 1);
                newWarning = true;
            }
            return newWarning;
        }
        public bool AddError(string err)
        {
            bool newError = false;
            if (this.Errors.ContainsKey(err))
            {
                this.Errors[err]++;
            }
            else
            {
                this.Errors.Add(err, 1);
                newError = true;
            }
            return newError;
        }

        // Checks to see if warnings were reported
        public bool HasWarnings()
        {
            return this.Warnings.Count > 0;
        }

        // Checks to see if warnings were reported, or errors on the ignore list
        public bool HasWarnings(List<string> ignore)
        {
            foreach (string k in this.Errors.Keys)
                if (ignore.Contains(k)) return true;

            return this.Warnings.Count > 0;
        }

        // Return string with all warnings
        public string GetWarnings()
        {
            System.Text.StringBuilder outP = new System.Text.StringBuilder();
            if (Warnings.Count > 0)
            {
                outP.AppendLine("Warnings: ");
                foreach (string k in Warnings.Keys)
                    outP.AppendLine(k + ": " + Warnings[k]);
            }
            return outP.ToString();
        }

        // Return string with warnings and ignored errors
        public string GetWarnings(List<string> ignore)
        {
            System.Text.StringBuilder outP = new System.Text.StringBuilder();
            if (Warnings.Count > 0)
            {
                outP.AppendLine("Warnings: ");
                foreach (string k in Warnings.Keys)
                    outP.AppendLine(k + ": " + Warnings[k]);
            }

            // Add ignored errors
            bool first = true;
            foreach (string k in this.Errors.Keys)
                if (ignore.Contains(k))
                {
                    if (first)
                    {
                        first = false;
                        outP.AppendLine("Ignored Errors: ");
                    }
                    outP.AppendLine(k + ": " + Errors[k]);
                }
            return outP.ToString();
        }

        // Checks to see if errors were reported
        public bool HasErrors()
        {
            return this.Errors.Count > 0;
        }

        // Checks to see if there are errors, other than those in the ignore list were reported
        public bool HasErrors(List<string> ignore)
        {
            foreach(string k in this.Errors.Keys)
                if (!ignore.Contains(k)) return true;

            return false;
        }


        // return string with all errors
        public string GetErrors()
        {
            System.Text.StringBuilder outP = new System.Text.StringBuilder();
            if (Errors.Count > 0)
            {
                outP.AppendLine("Errors: ");
                foreach (string k in this.Errors.Keys)
                    outP.AppendLine(k + ": " + Errors[k]);
            }
            return outP.ToString();
        }

        // return string listing errors that are not ignored
        public string GetErrors(List<string> ignore)
        {
            System.Text.StringBuilder outP = new System.Text.StringBuilder();
            bool first = true;

            foreach (string k in this.Errors.Keys)
                if (!ignore.Contains(k))
                {
                    if (first)
                    {
                        first = false;
                        outP.AppendLine("Errors: ");
                    }
                    outP.AppendLine(k + ": " + Errors[k]);
                }

            return outP.ToString();
        }

        public override string ToString()
        {
            return this.Code.ToString();
        }
        
        public void AppendLineNoNum(string L)
        {
            if (L.Length > 0) { this.Code.AppendLine(L); }  
        }
        public void AppendLine(string L)
        {
            string line = L;

            if (L.Length > 0)
            {
                if (L[0] != Mach.CommentChar)
                {
                    line = "N" + this.lines.ToString("0000") + "0 " + L;
                    this.lines++;
                }
                this.Code.AppendLine(line);
            }
        }

        public void Append(string L)
        {
            string LinePattern = @".+";
            MatchCollection Lines = Regex.Matches(L, LinePattern);
            foreach(Match m in Lines)
            {
                this.AppendLine(m.ToString());
            }
        }

    }
}
