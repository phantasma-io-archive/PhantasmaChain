using Phantasma.VM.Utils;
using System;
using System.Collections.Generic;

namespace Phantasma.CodeGen.Assembler
{
    public static class AssemblerUtils
    {
        public static byte[] BuildScript(IEnumerable<string> lines)
        {
            IEnumerable<Semanteme> semantemes = null;
            try
            {
                semantemes = Semanteme.ProcessLines(lines);
            }
            catch (Exception e)
            {
                throw new Exception("Error parsing the script" + e.ToString());
            }

            var sb = new ScriptBuilder();
            Semanteme tmp;
            byte[] script = null;

            try
            {
                foreach (var entry in semantemes)
                {
                    tmp = entry;
                    entry.Process(sb);
                }
                script = sb.ToScript();
            }
            catch (Exception e)
            {
                throw new Exception("Error assembling the script: " + e.ToString());
            }

            return script;
        }
    }
}
