using System.Collections.Generic;

namespace Phantasma.CodeGen.Core
{
    public enum LiteralKind
    {
        Unknown,
        String,
        Integer,
        Float,
        Boolean,
        Null,
        Binary
    }

    public class Token
    {
        public enum Kind
        {
            Invalid,
            Delimiter,
            Identifier,
            Keyword,
            Operator,
            String,
            Integer,
            Float,
            Boolean,
            Binary,
            Null
        }

        public readonly Kind kind;
        public readonly string text;
        public readonly int index;

        public Token(Kind kind, string text, int index)
        {
            this.kind = kind;
            this.text = text;
            this.index = index;
        }

        public override string ToString()
        {
            return text + "=>" + kind;
        }
    }

    public abstract class Lexer
    {
        public abstract bool IsOperator(string s);

        public abstract bool IsDelimiter(char c);

        public static bool IsLiteral(Token.Kind kind)
        {
            switch (kind)
            {
                case Token.Kind.Integer:
                case Token.Kind.Float:
                case Token.Kind.Boolean:
                case Token.Kind.String:
                case Token.Kind.Null:
                case Token.Kind.Binary:
                    return true;
                default: return false;
            }
        }


        private enum State
        {
            Normal,
            String,
            Comment
        }

        private void BreakIntoTokens(List<Token> tokens, ref string s, ref int baseIndex, ref int index, Token.Kind kind = Token.Kind.Invalid)
        {
            if (s.Length > 0)
            {
                var token = kind != Token.Kind.Invalid ? new Token(kind, s, baseIndex) : Tokenize(s, baseIndex);
                tokens.Add(token);
                s = "";
            }

            baseIndex = index;
        }

        protected abstract Token Tokenize(string s, int index);

        public List<Token> Execute(string src)
        {
            string s = "";
            char last = '\0';
            int index = 0;
            bool wasWhitespace = false;

            var tokens = new List<Token>();
            var state = State.Normal;

            int baseIndex = 0;

            while (index < src.Length)
            {
                var c = src[index];
                index++;

                bool isWhitespace = char.IsWhiteSpace(c);
                bool isDelimiter = IsDelimiter(c) || (s.Length == 1 && IsDelimiter(s[0]));

                switch (state)
                {
                    case State.String:
                        {
                            if (c == '\"')
                            {
                                state = State.Normal;
                                BreakIntoTokens(tokens, ref s, ref baseIndex, ref index, Token.Kind.String);
                            }
                            else
                            {
                                s += c;
                            }
                            break;
                        }

                    case State.Normal:
                        {
                            if (IsOperator(s) && !IsOperator(s + c))
                            {
                                BreakIntoTokens(tokens, ref s, ref baseIndex, ref index);
                            }
                            else
                            if ((isWhitespace && !wasWhitespace) || isDelimiter)
                            {
                                BreakIntoTokens(tokens, ref s, ref baseIndex, ref index);
                            }

                            if (c == '\"')
                            {
                                state = State.String;
                                BreakIntoTokens(tokens, ref s, ref baseIndex, ref index);
                            }
                            else
                            if (!isWhitespace)
                            {
                                s += c;
                            }

                            break;
                        }
                }

                wasWhitespace = isWhitespace;
                last = c;
            }

            if (s.Length > 0)
            {
                var token = Tokenize(s.ToString(), baseIndex);
                tokens.Add(token);
            }

            return tokens;
        }
    }
}
