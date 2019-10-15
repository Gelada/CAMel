namespace CAMel.Types
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using JetBrains.Annotations;

    public class BpCommand : IEnumerable<double>
    {
        [NotNull] public string command { get; }

        [NotNull] public List<double> values { get; }

        public BpCommand([CanBeNull] string comm)
        {
            if (string.IsNullOrEmpty(comm))
            {
                this.command = string.Empty;
                this.values = new List<double>();
            }
            else
            {
                List<string> terms = new List<string>();
                terms.AddRange(comm.Split(' '));

                this.values = new List<double>();
                if (terms.Count > 0)
                {
                    if (double.TryParse(terms[0], out double val))
                    {
                        this.values.Add(val);
                        this.command = string.Empty;
                    }
                    else { this.command = terms[0] ?? string.Empty; }
                }
                else { this.command = string.Empty; }

                for (int i = 1; i < terms.Count; i++)
                {
                    if (double.TryParse(terms[i], out double val)) { this.values.Add(val); }
                    else { this.values.Add(0); }
                }
            }
        }

        public override string ToString()
        {
            string str = this.command;
            return this.values.Aggregate(str, (current, d) => current + " " + d) ?? string.Empty;
        }

        #region List Functions

        public double this[int index]
        {
            get
            {
                if (this.values.Count > index) { return this.values[index]; }
                return 0;
            }
        }

        /// <inheritdoc />
        public IEnumerator<double> GetEnumerator() => this.values.GetEnumerator();
        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this.values).GetEnumerator();

        #endregion
    }

    public class BasicParser : IEnumerable<BpCommand>
    {
        [NotNull] private readonly List<BpCommand> commands;

        public BasicParser([CanBeNull] string commandString)
        {
            this.commands = new List<BpCommand>();
            if (string.IsNullOrEmpty(commandString)) { return; }
            List<string> split = new List<string>();
            split.AddRange(commandString.Split(','));
            this.commands.AddRange(split.Select(s => new BpCommand(s)).ToList());
        }

        public override string ToString()
        {
            return this.commands.Aggregate(string.Empty, (current, comm) => current + comm) ?? string.Empty;
        }

        [UsedImplicitly]
        public bool contains([NotNull] string command, out BpCommand c)
        {
            foreach (BpCommand bpC in this.commands.Where(bpC => bpC?.command == command))
            {
                c = bpC;
                return true;
            }

            c = null;
            return false;
        }

        /// <inheritdoc />
        public IEnumerator<BpCommand> GetEnumerator() => this.commands.GetEnumerator();
        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this.commands).GetEnumerator();
    }
}
