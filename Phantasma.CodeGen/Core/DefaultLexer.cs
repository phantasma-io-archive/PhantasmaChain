using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Phantasma.CodeGen.Core
{
    public class DefaultLexer: Lexer
    {
        private HashSet<string> _keywords;

        public DefaultLexer(IEnumerable<string> keywords)
        {
            _keywords = new HashSet<string>(keywords);
        }

        public override bool IsOperator(string s)
        {
            switch (s)
            {
                case "+":
                case "-":
                case "++":
                case "--":
                case "*":
                case "/":
                case "%":
                case "!":
                case "=":
                case "==":
                case ">>":
                case "<<":
                case "<":
                case ">":
                case "<=":
                case ">=":
                case "|":
                case "&":
                case "||":
                case "&&":
                case "^":

                    return true;
                default: return false;
            }
        }

        public override bool IsDelimiter(char c)
        {
            switch (c)
            {
                case ',':
                case ';':
                case ':':
                case '{':
                case '}':
                case '[':
                case ']':
                case '(':
                case ')':
                    return true;
                default: return false;
            }
        }

        protected override Token Tokenize(string s, int index)
        {
            if (s.Length == 1 && IsDelimiter(s[0]))
            {
                return new Token(Token.Kind.Delimiter, s, index);
            }

            if (s.Equals("true") || s.Equals("false"))
            {
                return new Token(Token.Kind.Boolean, s, index);
            }

            if (s.Equals("null"))
            {
                return new Token(Token.Kind.Null, s, index);
            }

            if (IsOperator(s))
            {
                return new Token(Token.Kind.Operator, s, index);
            }

            if (_keywords.Contains(s))
            {
                return new Token(Token.Kind.Keyword, s, index);
            }

            if (Regex.Match(s, "^[0-9]*$").Success)
            {
                return new Token(Token.Kind.Integer, s, index);
            }

            if (Regex.Match(s, @"^[0-9]*(?:\.[0-9]*)?$").Success)
            {
                return new Token(Token.Kind.Float, s, index);
            }

            if (Regex.Match(s, @"^(?:((?!\d)\w+(?:\.(?!\d)\w+)*)\.)?((?!\d)\w+)$").Success)
            {
                return new Token(Token.Kind.Identifier, s, index);
            }

            return new Token(Token.Kind.Invalid, s, index);
        }

    }
}
