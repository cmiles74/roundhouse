namespace roundhouse.parser.rules
{
    /// <summary>
    /// Provides a set of scanning and parsing rules that may be used with the
    /// MySQL database server.
    /// </summary>
    public class MySqlRules : ParserRules
    {
        public MySqlRules()
        {
            BacktickAsQuote = true;
            HashmarkAsComment = true;
        }
    }
}