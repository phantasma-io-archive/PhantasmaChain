using System;

namespace Phantasma.CodeGen
{
    public class CompilerException : Exception
    {
        public CompilerException(uint lineNumber, string message)
            : base($"ERROR: {message} in line {lineNumber}.")
        {
        }
    }
}
