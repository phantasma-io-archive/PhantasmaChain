using Phantasma.CodeGen.Languages;
using Phantasma.Core;
using System;

namespace Phantasma.CodeGen.Core
{
    public enum Language
    {
        Unknown,
        CSharp,
        Solidity
    }

    public abstract class LanguageProcessor
    {
        public abstract Lexer Lexer { get; }
        public abstract Parser Parser { get; }
        public abstract string Description { get; }

        public static Language GetLanguage(string extension)
        {
            switch (extension)
            {
                case ".cs": return Language.CSharp;
                case ".sol": return Language.Solidity;
                default: return Language.Unknown;
            }
        }

        public static LanguageProcessor GetProcessor(Language language)
        {
            Throw.If(language == Language.Unknown, "unknown language");

            switch (language)
            {
                case Language.CSharp: return new CSharpProcessor();
                case Language.Solidity: return new SolidityProcessor();
                default: throw new NotImplementedException(language.ToString());
            }
        }
    }
}
