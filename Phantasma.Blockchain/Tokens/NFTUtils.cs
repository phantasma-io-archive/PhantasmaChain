using Phantasma.Domain;
using Phantasma.VM;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Tokens
{
    public static class NFTUtils
    {
        public static ContractInterface GetNFTStandard()
        {
            var parameters = new ContractParameter[]
            {
                new ContractParameter("tokenID", VM.VMType.Number)
            };

            var methods = new ContractMethod[]
            {
                new ContractMethod("getName", VM.VMType.String, -1, parameters),
                new ContractMethod("getDescription", VM.VMType.String, -1, parameters),
                new ContractMethod("getImageURL", VM.VMType.String, -1, parameters),
                new ContractMethod("getInfoURL", VM.VMType.String, -1, parameters),
            };
            return new ContractInterface(methods, Enumerable.Empty<ContractEvent>());
        }

        private static void GenerateStringScript(StringBuilder sb, string propName, string data)
        {
            var split = data.Split(new char[] { '*' }, StringSplitOptions.None);

            var left = split[0];
            var right = split.Length > 1 ? split[1] : "";

            sb.AppendLine($"@{propName}: NOP");
            sb.AppendLine("POP r0 // tokenID");
            sb.AppendLine("CAST r0 r0 #String");            

            if (!string.IsNullOrEmpty(left))
            {
                if (!string.IsNullOrEmpty(right))
                {
                    sb.AppendLine("LOAD r1 \"" + left + "\"");
                    sb.AppendLine("LOAD r2 \"" + right + "\"");
                    sb.AppendLine("CAT r1 r0 r0");
                    sb.AppendLine("CAT r0 r2 r0");
                }
                else
                {
                    sb.AppendLine("LOAD r1 \"" + left + "\"");

                    if (propName.Contains("*"))
                    {
                        sb.AppendLine("CAT r1 r0 r0");
                    }
                    else
                    {
                        sb.AppendLine("PUSH r1");
                        sb.AppendLine("RET");
                        return;
                    }
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(right))
                {
                    sb.AppendLine("LOAD r1 \"" + right + "\"");
                    sb.AppendLine("CAT r0 r1 r0");
                }
                else
                {
                    // do nothing
                }
            }
            sb.AppendLine("PUSH r0");
            sb.AppendLine("RET");
        }

        public static void GenerateNFTDummyScript(string symbol, string name, string description, string jsonURL, string imgURL, out byte[] script, out ContractInterface abi)
        {
            if (!jsonURL.EndsWith("\\"))
            {
                jsonURL += '\\';
            }

            if (!imgURL.EndsWith("\\"))
            {
                imgURL += '\\';
            }

            var sb = new StringBuilder();

            GenerateStringScript(sb, "getName", name);
            GenerateStringScript(sb, "getDescription", description);
            GenerateStringScript(sb, "getInfoURL", jsonURL);
            GenerateStringScript(sb, "getImageURL", imgURL);

            var asm = sb.ToString().Split('\n');

            DebugInfo debugInfo;
            Dictionary<string, int> labels;
            script = CodeGen.Assembler.AssemblerUtils.BuildScript(asm, "dummy", out debugInfo, out labels);

            var standardABI = GetNFTStandard();

            var methods = standardABI.Methods.Select(x => new ContractMethod(x.name, x.returnType, labels[x.name], x.parameters));

            abi = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());
        }

        private static VMObject ExecuteScript(byte[] script, ContractInterface abi, string methodName, params object[] args)
        {
            var method = abi.FindMethod(methodName);

            if (method == null)
            {
                throw new Exception("ABI is missing: " + method.name);
            }

            var vm = new GasMachine(script, (uint)method.offset);

            // TODO maybe this needs to be in inverted order?
            foreach (var arg in args)
            {
                vm.Stack.Push(VMObject.FromObject(arg));
            }

            var result = vm.Execute();
            if (result == ExecutionState.Halt)
            {
                return vm.Stack.Pop();
            }

            throw new Exception("Script execution failed for: " + method.name);
        }

        public static void FetchProperty(string methodName, ITokenSeries series, BigInteger tokenID, Action<string, VMObject> callback)
        {
            if (series.ABI.HasMethod(methodName))
            {
                var result = ExecuteScript(series.Script, series.ABI, methodName, tokenID);

                string propName = methodName;

                if (propName.StartsWith("is"))
                {
                    propName = propName.Substring(2);
                }
                else
                if (propName.StartsWith("get"))
                {
                    propName = propName.Substring(3);
                }

                callback(propName, result);
            }
        }

    }
}
