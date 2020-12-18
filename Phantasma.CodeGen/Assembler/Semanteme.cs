using Phantasma.VM;
using Phantasma.VM.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Phantasma.CodeGen.Assembler
{
    public abstract class Semanteme
    {
        public readonly uint LineNumber;
        public uint BaseAddress;

        public abstract void Process(ScriptBuilder sb);

        public Semanteme(uint lineNumber)
        {
            this.LineNumber = lineNumber;
        }

        public static Semanteme[] ProcessLines(IEnumerable<string> lines)
        {
            ArgumentUtils.ClearAlias();


            var semantemes = new List<Semanteme>();

            bool insideComment = false;

            uint lineNumber = 0;
            foreach (string entry in lines)
            {
                string line = entry.Trim();
                
                lineNumber++;

                int index;

                index = SearchStringIndex(line, "//");
                if (index >= 0)
                {
                    // strip single line comments
                    line = line.Substring(0, index);
                }

                if (insideComment)
                {
                    index = SearchStringIndex(line, "*/");
                    if (index == -1)
                    {
                        continue;
                    }

                    line = line.Substring(index + 2);
                }

                index = 0;
                while (true)
                {
                    index = SearchStringIndex(line, "/*", index);
                    if (index == -1)
                    {
                        break;
                    }

                    int index2 = SearchStringIndex(line, "*/", index + 2);
                    if (index2 >= 0)
                    {
                        line = line.Substring(0, index) + line.Substring(index2 + 2);
                    }
                    else
                    {
                        line = line.Substring(0, index);
                        insideComment = true;
                        break;
                    }
                }

                index = SearchStringIndex(line, ":");
                if (index >= 0)
                {
                    semantemes.Add(new Label(lineNumber, line.Substring(0, index).AsLabel()));
                    line = line.Substring(index + 1).Trim();
                }

                if (!string.IsNullOrEmpty(line))
                {
                    string[] words = SplitWords(line);
                    var name = words[0];
                    var args = words.Skip(1).ToArray();
                    semantemes.Add(new Instruction(lineNumber, name, args));
                }
            }

            return semantemes.ToArray();
        }

        private static int SearchStringIndex(string line, string target, int start = 0)
        {
            bool insideString = false;

            int targetIndex = 0;
            char targetChar = target[0];

            char prev = '\0';

            for (int i=start; i<line.Length; i++)
            {
                var ch = line[i];

                if (insideString)
                {
                    if (ch == '"')
                    {
                        insideString = false;
                    }
                }
                else
                {
                    if (ch == '\\' && prev == ch)
                    {
                        break;
                    }

                    if (ch == targetChar)
                    {
                        targetIndex++;

                        if (targetIndex >= target.Length)
                        {
                            return (i + 1) - target.Length;
                        }

                        targetChar = target[targetIndex];
                    }

                    if (ch == '"')
                    {
                        insideString = true;
                    }
                }

                prev = ch;
            }

            return -1;
        }

        private static string[] SplitWords(string line)
        {
            bool insideQuotes = false;
            bool escaped = false;
            List<string> words = new List<string>();
            var currentWord = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                switch (c)
                {
                    case ',':
                    case '[':
                    case ']':
                    case ' ':
                        if (insideQuotes)
                            goto default;

                        if (currentWord.Length > 0)
                        {
                            words.Add(currentWord.ToString());
                            currentWord.Clear();
                        }
                            
                        break;

                    case '\\':
                        if (i + 1 >= line.Length)
                            throw new Exception("Escaping character not followed by an escapee");

                        escaped = true;                        
                        break;

                    case '\"':
                        if (!escaped)
                        {
                            insideQuotes = !insideQuotes;
                        }
                        else
                        {
                            escaped = true;
                        }
                        goto default;

                    default:
                        currentWord.Append(c);
                        break;
                }
            }

            if(currentWord.Length > 0)
                words.Add(currentWord.ToString());

            return words.ToArray();
        }
    }
}
