using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.CodeGen.Core;
using Phantasma.CodeGen.Core.Nodes;

namespace Phantasma.CodeGen.Languages
{
    public class SolidityProcessor : LanguageProcessor
    {
        protected string[] _keywords = new string[]{
            "return",  "public", "private", "external", "internal", "pure", "view", "payable", "constant", "anonymous", "indexed",
            "pragma", "solidity", "contract", "function", "struct", "if", "else", "while", "do", "returns"
        };

        public override Lexer Lexer => _lexer;
        public override Parser Parser => _parser;
        public override string Description => "Solidity";

        private Lexer _lexer;
        private Parser _parser;

        public SolidityProcessor()
        {
            _lexer = new DefaultLexer(_keywords);
            _parser = new SolidityParser();
        }
    }

    public class SolidityParser : DefaultParser
    {
        private int GetNumericSize(string size)
        {
            if (string.IsNullOrEmpty(size))
            {
                return 256;
            }

            int val;

            if (int.TryParse(size, out val))
            {
                if (val>=8 && val <= 256)
                {
                    if ((val % 8) == 0)
                    {
                        return val;
                    } 
                }
            }

            return -1;
        }

        protected override TypeNode GetTypeFromToken(CompilerNode owner, string token)
        {
            if (token.StartsWith("int"))
            {
                int size = GetNumericSize(token.Substring(3));
                if (size > 0)
                {
                    return new TypeNode(owner, TypeKind.Integer);
                }
                else
                {
                    return null;
                }
            }

            if (token.StartsWith("uint"))
            {
                int size = GetNumericSize(token.Substring(4));
                if (size > 0)
                {
                    return new TypeNode(owner, TypeKind.Integer);
                }
                else
                {
                    return null;
                }
            }

            if (token.StartsWith("byte"))
            {
                var size = token.Substring(4);
                if (string.IsNullOrEmpty(size) || size=="s")
                {
                    return new TypeNode(owner, TypeKind.ByteArray);
                }

                int val;
                if (int.TryParse(size, out val))
                {
                    if (val >= 1 && val <= 32)
                    {
                        return new TypeNode(owner, TypeKind.ByteArray);
                    }
                }

                return null;
            }

            switch (token)
            {
                case "bool": return new TypeNode(owner, TypeKind.Boolean);
                case "string": return new TypeNode(owner, TypeKind.String);
                case "address": return new TypeNode(owner, TypeKind.Struct); // TODO fixme
                default: return null;
            }
        }

        public override ModuleNode Execute(List<Token> tokens)
        {
            int index = 0;

            var module = new ModuleNode();
            while (index < tokens.Count)
            {
                var token = tokens[index];
                index++;

                if (token.text == "pragma")
                {
                    ExpectKeyword(tokens, ref index, "solidity");
                    ExpectOperator(tokens, ref index);
                    ExpectValue(tokens, ref index, Token.Kind.Invalid, ParserException.Kind.UnexpectedToken); // TODO this is a hack
                    ExpectDelimiter(tokens, ref index, ";");
                }
                else
                if (token.text == "contract")
                {
                    var name = ExpectIdentifier(tokens, ref index, true);

                    ExpectDelimiter(tokens, ref index, "{");

                    var classNode = new ClassNode(module);
                    classNode.name = name;

                    ParseContractContent(tokens, ref index, classNode, module);

                    ExpectDelimiter(tokens, ref index, "}");

                    if (module.body != null)
                    {
                        throw new ParserException(tokens.Last(), ParserException.Kind.InternalError);
                    }

                    module.body = GenerateEntryPoint(module, classNode.methods);
                }
                else
                {
                    throw new ParserException(token, ParserException.Kind.UnexpectedToken);
                }

            }

            return module;
        }

        private void ParseContractContent(List<Token> tokens, ref int index, ClassNode classNode, ModuleNode module)
        {
            classNode.visibility = Visibility.Public;
            classNode.isAbstract = false;
            classNode.isStatic = false;

            do
            {
                if (index >= tokens.Count) throw new ParserException(tokens.Last(), ParserException.Kind.EndOfStream);

                var token = tokens[index];

                if (token.text == "function")
                {
                    index++;
                    ParseMethodContent(tokens, ref index, classNode, module);
                }
                else
                {
                    throw new ParserException(token, ParserException.Kind.UnexpectedToken);
                }

            } while (tokens[index].text != "}");

        }

        private void ParseMethodContent(List<Token> tokens, ref int index, ClassNode classNode, ModuleNode module)
        {
            var method = new MethodNode(classNode);

            method.name = ExpectIdentifier(tokens, ref index, false);

            ExpectDelimiter(tokens, ref index, "(");
            ParseMethodArguments(tokens, ref index, method);
            ExpectDelimiter(tokens, ref index, ")");

            var attrs = ParseOptionals(tokens, ref index, new HashSet<string>() { "public", "private", "internal", "external", "pure", "constant", "view" });

            if (tokens[index].text == "returns")
            {
                index++;

                ExpectDelimiter(tokens, ref index, "(");
                var typeText = ExpectIdentifier(tokens, ref index, true);
                method.returnType = GetTypeFromToken(method, typeText);

                if (method.returnType == null)
                {
                    throw new ParserException(tokens.Last(), ParserException.Kind.ExpectedType);
                }

                ExpectDelimiter(tokens, ref index, ")");
            }
            else
            {
                method.returnType = new TypeNode(method, TypeKind.Void);
            }


            if (attrs.Contains("private"))
            {
                method.visibility = Visibility.Private;
            }
            else
            if (attrs.Contains("internal"))
            {
                method.visibility = Visibility.Internal;
            }
            else
            {
                method.visibility = Visibility.Public;
            }

            method.body = ParseStatement(tokens, ref index, method);
        }

        private void ParseMethodArguments(List<Token> tokens, ref int index, MethodNode method)
        {
            int count = 0;
            do
            {
                if (index >= tokens.Count)
                {
                    throw new ParserException(tokens.Last(), ParserException.Kind.EndOfStream);
                }

                if (count > 0)
                {
                    ExpectDelimiter(tokens, ref index, ",");
                }

                var arg = new ParameterNode(method);

                var decl = new DeclarationNode(arg);
                var typeText = ExpectIdentifier(tokens, ref index, true);

                decl.type = GetTypeFromToken(method, typeText);
                if (decl.type == null)
                {
                    throw new ParserException(tokens.Last(), ParserException.Kind.ExpectedType);
                }

                decl.identifier = ExpectIdentifier(tokens, ref index, false);

                count++;
            } while (tokens[index].text != ")");
        }
    }
}
