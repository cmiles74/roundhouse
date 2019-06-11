using System;
using System.Collections.Generic;
using roundhouse.parser.rules;

namespace roundhouse.parser
{
    /// <summary>
    /// Provides a scanner that will break a string of SQL into tokens.
    /// </summary>
    class Scanner
    {
        /// <summary>
        /// Keyword indicating the delimiter is being re-defined
        /// </summary>
        private const string DELIMITER_DECLARE = "delimiter";

        /// <summary>
        /// Default statement delimiter
        /// </summary>
        private const string DEFAULT_DELIMETER = ";";

        /// <summary>
        /// The MySQL script to scan
        /// </summary>
        private readonly string script;

        /// <summary>
        /// The current statement delimiter
        /// </summary>
        private string delimiter = DEFAULT_DELIMETER;

        /// <summary>
        /// Rules to use when scaning and parsing SQL for statements
        /// </summary>
        private readonly ParserRules parserRules;

        /// <summary>
        /// Start position in the script for the current token
        /// </summary>
        private int start;

        /// <summary>
        /// Current position in the script
        /// </summary>
        private int current;

        /// <summary>
        /// Current line in the script
        /// </summary>
        private int line;

        /// <summary>
        /// List of tokens in the script
        /// </summary>
        private readonly List<Token> tokens = new List<Token>();

        /// <summary>
        /// Creates a new scanner that will use the "generic" set of scanning 
        // and parsing and the SQL script
        /// </summary>
        /// <param name="script">the MySQL script to parse</script>
        public Scanner(string script) : this(new GenericRules(), script) {

        }

        /// <summary>
        /// Creates a new scanner and sets its scnnaing rules and SQL script
        /// </summary>
        /// <param name="parserRules">Parser rule object to use when scanning and parsing</param>
        /// <param name="script">the SQL script to parse</script>
        public Scanner(ParserRules parserRules, string script)
        {
            this.parserRules = parserRules;
            this.script = script;
        }

        public string Delimiter
        {
            get 
            {
                return this.delimiter;
            }
            set 
            {
                this.delimiter = value;
            }
        }

        /// <summary>
        /// Scans the SQL script and returns a List of Token instances
        /// </summary>
        /// <returns>List of Token</return>
        public List<Token> Scan() 
        {
            // initialize our starting location
            start = 0;
            current = 0;
            line = 1;

            while (!IsAtEnd()) {
                
                start = current;
                ScanToken();
            }

            EndOfFile();
            return tokens;
        }

        private void ScanToken() 
        {

            char c = Advance();

            // we're giving delimiters precedence over other tokens
            if (delimiter.Length == 1 && c == delimiter[0]) {
                SingleCharacterDelimiter();
                return;
            } else if (delimiter.Length == 2 && c == delimiter[0] && Peek() == delimiter[1]) {
                TwoCharacterDelimiter();
                return;
            } else if (delimiter.Length > 2 && c == delimiter[0] && Peek() == delimiter[1]) {
                if(MultiCharacterDelimiter()) {
                    return;
                }
            }

            switch (c)
            {
                // whitespace we can ignore, but delimits tokens
                case '\t':
                case ' ':
                    Whitespace();
                    break;

                case '\r':
                    if (Peek() == '\n') {
                        Advance();  // we are discarding /r
                        EndOfLine();
                        break;
                    } else {
                        goto case '\n';
                    }

                case '\n':
                    EndOfLine();
                    break;

                case '"':
                    DoubleQuoted();
                    break;

                case '\'':
                    SingleQuoted();
                    break;

                case '`':
                    if (parserRules.BacktickAsQuote) {
                        BacktickQuoted();
                        break;
                    } else {
                        goto case '#';
                    }

                case '#':
                    if (parserRules.HashmarkAsComment) {
                        //Advance();
                        Comment();
                        break;
                    } else {
                        goto case '-';
                    }
                
                case '-':
                    if (Match('-')) {
                        Advance();
                        Comment();
                        break;
                    } else {
                        goto case '/';
                    }
                
                case '/':
                    if (Match('*')) {
                        Advance();
                        MultiLineComment();
                        break;
                    } else {
                        General();
                        break;
                    }

                default:
                    General();
                    break;
            }
        }

        private void MultiLineComment()
        {
            while (!(Match('*') && Peek() == '/')) {
                
                if (PeekEndOfLine()) {
                    line++;
                }

                Advance();

                if (IsAtEnd()) {
                    // unterminated comment
                    throw new ParserException("Unterminated comment on line " + line);
                }
            }

            Advance(); // consume *
            Advance(); // consume /

            AddToken(Token.Type.Comment);
        }

        private void Comment()
        {
            while (!PeekEndOfLine() && !IsAtEnd()) {
                Advance();
            }

            AddToken(Token.Type.Comment);
        }

        private void SingleCharacterDelimiter()
        {
            AddToken(Token.Type.Delimiter);
        }

        private void TwoCharacterDelimiter()
        {
            Advance();  // consume second character
            AddToken(Token.Type.Delimiter);
        }

        private bool MultiCharacterDelimiter() 
        {
            if (start + delimiter.Length <= script.Length) {

                string possibleDelimiter = script.Substring(start, delimiter.Length);
                if (possibleDelimiter.Equals(delimiter)) {
                    current = start + delimiter.Length;  // consume the rest of the delimiter
                    AddToken(Token.Type.Delimiter);
                    return true;
                }
            }

            return false;
        }

        private void Whitespace()
        {
            while (!IsAtEnd() && !PeekEndOfLine() && Char.IsWhiteSpace(Peek())) {
                Advance();
            }

            AddToken(Token.Type.Whitespace);
        }

        private void BacktickQuoted()
        {
            while (!IsAtEnd() && !IsBacktickQuote(Peek())) {
                
                if (MatchEndOfLine()) {
                    line++;
                }

                Advance();

                if (IsAtEnd()) {
                    // unterminated string
                    throw new ParserException("Unterminated quoted value on line " + line);
                }
            }

            Advance(); // closing quote
            AddToken(Token.Type.Quote);
        }

        private void DoubleQuoted()
        {
            while (!IsAtEnd() && !IsDoubleQuote(Peek())) {
                
                if (MatchEndOfLine()) {
                    line++;
                }

                Advance();

                if (IsAtEnd()) {
                    // unterminated string
                    throw new ParserException("Unterminated double quoted value on line " + line);
                }
            }

            Advance(); // closing quote
            AddToken(Token.Type.AnsiQuote);
        }

        private void SingleQuoted()
        {
            while (!IsAtEnd() && !IsSingleQuote(Peek())) {
                
                if (MatchEndOfLine()) {
                    line++;
                }

                Advance();

                if (IsAtEnd()) {
                    // unterminated string
                    throw new ParserException("Unterminated single quoted value on line " + line);
                }
            }

            Advance(); // closing quote
            AddToken(Token.Type.SingleQuote);
        }

        private void General()
        {
            while (!IsAtEnd() && Char.IsLetterOrDigit(Peek())) {
                
                // we need to check for delimiters come immediately after an identifiers
                if (Char.IsLetterOrDigit(delimiter[0])) {
                    if (delimiter.Length == 1 && Peek() == delimiter[0]) {
                        break;
                    } else if (delimiter.Length == 2 && Peek() == delimiter[0] && PeekPeek() == delimiter[1]) {
                        break;
                    } else if (delimiter.Length > 2 && PeekMultiCharacterDelimiter()) {
                        break;
                    }
                }

                Advance();
            }

            AddToken(Token.Type.Text);
        }

        private void EndOfFile()
        {
            AddEmptyToken(Token.Type.EndOfFile);
        }

        private void EndOfLine()
        {
            line++;
            AddEmptyToken(Token.Type.EndOfLine);
        }

        private bool Match(char expected)
        {

            if (IsAtEnd()) {
                return false;
            }

            if(script[current] != expected) {
                return false;
            }

            current++;
            return true;
        }

        private bool MatchEndOfLine() {
            if (script[current] == '\n') {
                current++;
                return true;
            }
            
            if (script[current] == '\r' && script[current + 1] == '\n') {
                current += 2;
                return true;
            }

            return false;
        }

        private char Peek() 
        {
            if (IsAtEnd()) {
                return '\0';
            }

            return script[current];
        }

        private char PeekPeek() 
        {
            if (IsAtEnd() || (current + 1 >= script.Length)) {
                return '\0';
            }

            return script[current + 1];
        }

        private bool PeekEndOfLine() {
            if (current + 1 <= script.Length) {
                if (script.Substring(current, 1) == "\n") {
                    return true;
                }
            }

            if (current + 2 <= script.Length) {
                if (script.Substring(current, 2) == "\r\n") {
                    return true;
                }
            }

            return false;
        }

        private bool PeekMultiCharacterDelimiter() 
        {
            if (current + delimiter.Length <= script.Length) {

                string possibleDelimiter = script.Substring(current, delimiter.Length);
                if (possibleDelimiter.Equals(delimiter)) {
                    return true;
                }
            }

            return false;
        }

        private char Advance() 
        {

            current++;
            return script[current - 1];
        }

        private bool IsAtEnd()
        {
            return current >= script.Length;
        }

        private bool IsAtEndOfLine() {
            return (Peek() == '\r' && PeekPeek() == '\n') || Peek() == '\n';
        }

        private static bool IsBacktickQuote(char c) 
        {
            return c == '`';
        }

        private static bool IsDoubleQuote(char c) 
        {
            return c == '"';
        }

        private static bool IsSingleQuote(char c)
        {
            return c == '\'';
        }

        private void DelimiterDeclaration() {

            // add our declaration keyword
            int end = current - start;
            string value = script.Substring(start, end);
            tokens.Add(new Token(Token.Type.DelimiterDeclare, value, line, start));

            // move our start forward for next scan
            start = current;

            // consume any whitespace
            Whitespace();

            if (IsAtEnd()) {
                // no delimiter provided
                throw new ParserException("Delimiter keyword used but no delimiter was provided");
            }

            // consume the new delimiter
            while (Peek() != '\n' && !IsAtEnd()) {
                Advance();
            }

            // add the new delimiter
            end = current - start;
            value = script.Substring(start, end).Trim();
            tokens.Add(new Token(Token.Type.Delimiter, value, line, start));

            // set our new delimiter
            delimiter = value;
        }

        private void AddToken(Token.Type type)
        {
            int end = current - start;
            if (end < 0) {
                end = 0;
            }

            if(current + end < 1) {
                return;
            }

            // delimiter redeclaration
            string value = script.Substring(start, end);
            if (value.ToLowerInvariant().Equals(DELIMITER_DECLARE)) {
                DelimiterDeclaration();
                return;
            }

            // typical token addition
            tokens.Add(new Token(type, value, line, start));
        }

        private void AddEmptyToken(Token.Type type)
        {
            tokens.Add(new Token(type, null, line, start));
        }
    }
}