using Phantasma.CodeGen.Core.Nodes;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.CodeGen.Core
{
    public class ParserException: Exception
    {
        public enum Kind
        {
            InternalError,
            EndOfStream,
            UnexpectedToken,
            ExpectedToken,
            ExpectedIdentifier,
            ExpectedLiteral,
            ExpectedOperator,
            ExpectedKeyword,
            ExpectedType,
            DuplicatedLabel,
        }

        public readonly Token token;
        public readonly Kind kind;

        public ParserException(Token token, Kind kind)
        {
            this.token = token;
            this.kind = kind;
        }
    }

    public abstract class Parser
    {
        public abstract ModuleNode Execute(List<Token> tokens);

        protected HashSet<string> ParseOptionals(List<Token> tokens, ref int index, HashSet<string> keywords)
        {
            var result = new HashSet<string>();
            do
            {
                if (index >= tokens.Count) throw new ParserException(tokens.Last(), ParserException.Kind.EndOfStream);

                var token = tokens[index];

                if (keywords.Contains(token.text))
                {
                    result.Add(token.text);
                    index++;
                }
                else
                {
                    return result;
                }
            } while (true);
        }


        protected bool ExpectOptional(List<Token> tokens, ref int index, string value)
        {
            if (index >= tokens.Count)
            {
                throw new ParserException(tokens.Last(), ParserException.Kind.EndOfStream);
            }

            var token = tokens[index];
            if (token.text == value)
            {
                index++;
                return true;
            }

            return false;
        }

        protected bool ExpectOptional(List<Token> tokens, ref int index, Token.Kind kind)
        {
            if (index >= tokens.Count) throw new ParserException(tokens.Last(), ParserException.Kind.EndOfStream);

            var token = tokens[index];
            if (token.kind == kind)
            {
                index++;
                return true;
            }

            return false;
        }

        protected void ExpectDelimiter(List<Token> tokens, ref int index, string value)
        {
            if (index >= tokens.Count) throw new ParserException(tokens.Last(), ParserException.Kind.EndOfStream);

            var token = tokens[index];
            if (token.kind != Token.Kind.Delimiter || token.text != value)
            {
                throw new ParserException(token, ParserException.Kind.ExpectedToken);
            }

            index++;
        }

        protected void ExpectKeyword(List<Token> tokens, ref int index, string value)
        {
            if (index >= tokens.Count) throw new ParserException(tokens.Last(), ParserException.Kind.EndOfStream);

            var token = tokens[index];
            if (token.kind != Token.Kind.Keyword || token.text != value)
            {
                throw new ParserException(token, ParserException.Kind.ExpectedKeyword);
            }

            index++;
        }

        protected string ExpectValue(List<Token> tokens, ref int index, Token.Kind kind, ParserException.Kind exception)
        {
            if (index >= tokens.Count) throw new ParserException(tokens.Last(), ParserException.Kind.EndOfStream);

            var token = tokens[index];
            if (token.kind != kind)
            {
                throw new ParserException(token, exception);
            }

            index++;
            return token.text;
        }

        protected string ExpectIdentifier(List<Token> tokens, ref int index, bool allowPath)
        {
            var result = ExpectValue(tokens, ref index, Token.Kind.Identifier, ParserException.Kind.ExpectedIdentifier);

            if (!allowPath && result.Contains("."))
            {
                throw new ParserException(tokens[index - 1], ParserException.Kind.ExpectedIdentifier);
            }

            return result;
        }

        protected object ExpectLiteral(List<Token> tokens, ref int index, out LiteralKind kind)
        {
            if (index >= tokens.Count) throw new ParserException(tokens.Last(), ParserException.Kind.EndOfStream);

            var token = tokens[index];
            index++;

            switch (token.kind)
            {
                case Token.Kind.Integer:
                    {
                        kind = LiteralKind.Integer;
                        var val = BigInteger.Parse(token.text);
                        return val;
                    }

                case Token.Kind.Float:
                    {
                        kind = LiteralKind.Float;
                        var val = decimal.Parse(token.text);
                        return val;
                    }

                case Token.Kind.Boolean:
                    {
                        kind = LiteralKind.Integer;
                        var val = token.text.ToLower() == "true";
                        return val;
                    }

                case Token.Kind.String:
                    {
                        kind = LiteralKind.String;
                        return token.text;
                    }

                default:
                    {
                        throw new ParserException(token, ParserException.Kind.ExpectedLiteral);
                    }
            }

        }

        protected string ExpectOperator(List<Token> tokens, ref int index)
        {
            return ExpectValue(tokens, ref index, Token.Kind.Operator, ParserException.Kind.ExpectedOperator);
        }

    }

}
