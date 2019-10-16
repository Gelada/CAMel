namespace CAMel.Types
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using JetBrains.Annotations;

    /// <summary>TODO The bp command.</summary>
    public class BpCommand : IEnumerable<double>
    {
        /// <summary>Gets the command.</summary>
        [NotNull]
        public string command { get; }

        /// <summary>Gets the values.</summary>
        [NotNull]
        private List<double> values { get; }

        /// <summary>Initializes a new instance of the <see cref="BpCommand"/> class.</summary>
        /// <param name="comm">TODO The comm.</param>
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

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        public override string ToString()
        {
            string str = this.command;
            return this.values.Aggregate(str, (current, d) => current + " " + d) ?? string.Empty;
        }

        #region List Functions

        /// <summary>TODO The this.</summary>
        /// <param name="index">TODO The index.</param>
        /// <returns>The <see cref="double"/>.</returns>
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

    /// <summary>TODO The basic parser.</summary>
    public class BasicParser : IEnumerable<BpCommand>
    {
        /// <summary>TODO The commands.</summary>
        [NotNull] private readonly List<BpCommand> commands;

        /// <summary>Initializes a new instance of the <see cref="BasicParser"/> class.</summary>
        /// <param name="commandString">TODO The command string.</param>
        public BasicParser([CanBeNull] string commandString)
        {
            this.commands = new List<BpCommand>();
            if (string.IsNullOrEmpty(commandString)) { return; }
            List<string> split = new List<string>();
            split.AddRange(commandString.Split(','));
            this.commands.AddRange(split.Select(s => new BpCommand(s)).ToList());
        }

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        public override string ToString()
        {
            return this.commands.Aggregate(string.Empty, (current, comm) => current + comm) ?? string.Empty;
        }

        /// <summary>TODO The contains.</summary>
        /// <param name="command">TODO The command.</param>
        /// <param name="c">TODO The c.</param>
        /// <returns>The <see cref="bool"/>.</returns>
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
