namespace roundhouse.parser.rules
{
    /// <summary>
    /// Provides a data object that encapsulates a collection of "rules" or
    /// options used in the scanning and parsing process. Manipulating the
    /// settings of this object allows the scanning and parsing of SQL that
    /// is specialized for each database vendor.
    /// </summary>
    public class ParserRules
    {
        /// <summary>
        /// Flag indicating if the back-tick character (`) should be
        /// interpreted as a quote mark
        /// </summary>
        private bool backtickAsQuote;

        /// <summary>
        /// Flag indicating that a hash mark (#) should be treated as a single
        /// line comment indicator
        /// <summary>
        private bool hashmarkAsComment;

        public ParserRules()
        {
            BacktickAsQuote = false;
            HashmarkAsComment = false;
        }

        public bool BacktickAsQuote
        {
            get
            { 
                return this.backtickAsQuote;
            }
            set
            {
                this.backtickAsQuote = value;
            }
        }

        public bool HashmarkAsComment
        {
            get 
            { 
                return this.hashmarkAsComment;
            }
            set
            {
                this.hashmarkAsComment = value;
            }
        }
    }
}