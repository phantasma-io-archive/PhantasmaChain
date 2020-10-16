using Phantasma.VM.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Phantasma.CodeGen.Assembler
{
    public static class AssemblerUtils
    {

        public static byte[] BuildScript(IEnumerable<string> lines)
        {
            Dictionary<uint, int> offsets;
            return BuildScriptWithOffsets(lines, out offsets);
        }

        public static byte[] BuildScriptWithOffsets(IEnumerable<string> lines, out Dictionary<uint, int> offsets)
        {
            Semanteme[] semantemes = null;
            try
            {
                semantemes = Semanteme.ProcessLines(lines).ToArray();
            }
            catch (Exception e)
            {
                throw new Exception("Error parsing the script" + e.ToString());
            }

            var sb = new ScriptBuilder();
            Semanteme tmp;
            byte[] script;

            offsets = new Dictionary<uint, int>();
            try
            {
                foreach (var entry in semantemes)
                {
                    offsets[entry.LineNumber] = sb.CurrentSize;
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

        public static IEnumerable<string> CommentOffsets(IEnumerable<string> lines, Dictionary<uint, int> offsets)
        {
            uint lineNumber = 0;
            var output = new List<string>();
            foreach (var line in lines)
            {
                lineNumber++;

                var temp = line.Replace("\r\n", "").Replace("\n", "").Replace("\r", "");

                if (offsets.ContainsKey(lineNumber))
                {
                    var ofs = offsets[lineNumber];
                    output.Add($"{temp} // {ofs}");
                }
                else
                {
                    output.Add(temp);
                }
            }

            return output;
        }
    }
}
