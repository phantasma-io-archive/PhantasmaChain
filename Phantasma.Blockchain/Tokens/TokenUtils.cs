using Phantasma.Domain;
using Phantasma.VM;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using Phantasma.Cryptography;
using Phantasma.Core.Types;

namespace Phantasma.Blockchain.Tokens
{
    public static class TokenUtils
    {
        public static Address GetContractAddress(this IToken token)
        {
            return GetContractAddress(token.Symbol);
        }

        public static Address GetContractAddress(string symbol)
        {
            return SmartContract.GetAddressForName(symbol);
        }

        public static IEnumerable<ContractMethod> GetTriggersForABI(Dictionary<string, int> labels)
        {
            var triggers = new Dictionary<TokenTrigger, int>();
            foreach (var entry in labels)
            {
                TokenTrigger kind;

                if (Enum.TryParse<TokenTrigger>(entry.Key, true, out kind))
                {
                    triggers[kind] = entry.Value;
                }
            }

            return GetTriggersForABI(triggers);
        }

        public static IEnumerable<ContractMethod> GetTriggersForABI(IEnumerable<TokenTrigger> triggers)
        {
            var entries = new Dictionary<TokenTrigger, int>();
            foreach (var trigger in triggers)
            {
                entries[trigger] = 0;
            }

            return GetTriggersForABI(entries);
        }

        public static IEnumerable<ContractMethod> GetTriggersForABI(Dictionary<TokenTrigger, int> triggers)
        {
            var result = new List<ContractMethod>();

            foreach (var entry in triggers)
            {
                var trigger = entry.Key;
                var offset = entry.Value;

                switch (trigger)
                {
                    case TokenTrigger.OnBurn:
                    case TokenTrigger.OnMint:
                    case TokenTrigger.OnReceive:
                    case TokenTrigger.OnSend:
                        result.Add(new ContractMethod(trigger.ToString(), VM.VMType.None, offset, new[] {
                            new ContractParameter("from", VM.VMType.Object),
                            new ContractParameter("to", VM.VMType.Object),
                            new ContractParameter("symbol", VM.VMType.String),
                            new ContractParameter("amount", VM.VMType.Number)
                        }));
                        break;

                    default:
                        throw new System.Exception("AddTriggerToABI: Unsupported trigger: " + trigger);
                }
            }

            return result;
        }

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

                    if (data.Contains("*"))
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

        private static VMObject ExecuteScript(StorageContext storage, Chain chain, byte[] script, ContractInterface abi, string methodName, params object[] args)
        {
            var method = abi.FindMethod(methodName);

            if (method == null)
            {
                throw new Exception("ABI is missing: " + method.name);
            }

            var changeSet = storage as StorageChangeSetContext;

            if (changeSet == null)
            {
                changeSet = new StorageChangeSetContext(storage);
            }

            var oracle = chain.Nexus.GetOracleReader();
            var vm = new RuntimeVM(-1, script, (uint)method.offset, chain, Address.Null, Timestamp.Now, null, changeSet, oracle, ChainTask.Null, true);

            //var vm = new GasMachine(script, (uint)method.offset);

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

        public static void FetchProperty(StorageContext storage, Chain chain, string methodName, ITokenSeries series, BigInteger tokenID, Action<string, VMObject> callback)
        {
            if (series.ABI.HasMethod(methodName))
            {
                var result = ExecuteScript(storage, chain, series.Script, series.ABI, methodName, tokenID);

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

        public static void FetchProperty(StorageContext storage, Chain chain, string methodName, IToken token, Action<string, VMObject> callback)
        {
            FetchProperty(storage, chain, methodName, token.Script, token.ABI, callback);
        }

        public static void FetchProperty(StorageContext storage, Chain chain, string methodName, byte[] script, ContractInterface abi, Action<string, VMObject> callback)
        {
            if (abi.HasMethod(methodName))
            {
                var result = ExecuteScript(storage, chain, script, abi, methodName);

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
