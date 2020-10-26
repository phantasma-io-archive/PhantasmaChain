using Phantasma.Cryptography;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Phantasma.VM.Utils
{
    public struct DisasmMethodCall
    {
        public string ContractName;
        public string MethodName;

        public VMObject[] Arguments;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"{ContractName}.{MethodName}(");
            for (int i=0; i<Arguments.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                var arg = Arguments[i];
                sb.Append(arg.ToString());
            }
            sb.Append(")");
            return sb.ToString();
        }
    }

    public static class DisasmUtils
    {
        private static VMObject[] PopArgs(string contract, string method, Stack<VMObject> stack, Dictionary<string, int> methodArgumentCountTable)
        {

            var key = method;
            if (contract != null)
            {
                key = $"{contract}.{method}";
            }

            if (methodArgumentCountTable.ContainsKey(key))
            {
                var argCount = methodArgumentCountTable[key];
                var result = new VMObject[argCount];
                for (int i = 0; i < argCount; i++)
                {
                    result[i] = stack.Pop();
                }
                return result;
            }
            else
            {
                throw new System.Exception("Cannot disassemble method arguments => " + key);
            }
        }

        public static Dictionary<string, int> GetDefaultDisasmTable()
        {
            var table = new Dictionary<string, int>();
            table["Runtime.Log"] = 1;
            table["Runtime.Event"] = 3;
            table["Runtime.IsWitness"] = 1;
            table["Runtime.IsTrigger"] = 0;
            table["Runtime.TransferBalance"] = 3;
            table["Runtime.MintTokens"] = 4;
            table["Runtime.BurnTokens"] = 3;
            table["Runtime.SwapTokens"] = 5;
            table["Runtime.TransferTokens"] = 4;
            table["Runtime.TransferToken"] = 4;
            table["Runtime.MintToken"] = 4;
            table["Runtime.BurnToken"] = 3;

            table["Nexus.CreateToken"] = 7;

            table["gas.AllowGas"] = 4;
            table["gas.SpendGas"] = 1;

            table["market.SellToken"] = 6;
            table["market.BuyToken"] = 3;

            table["swap.GetRate"] = 3;
            table["swap.DepositTokens"] = 3;
            table["swap.SwapFee"] = 3;
            table["swap.SwapReverse"] = 4;
            table["swap.SwapFiat"] = 4;
            table["swap.SwapTokens"] = 4;
            table["stake.Migrate"] = 2;
            table["stake.MasterClaim"] = 1;
            table["stake.Stake"] = 2;
            table["stake.Unstake"] = 2;
            table["stake.Claim"] = 2;
            table["stake.AddProxy"] = 3;
            table["stake.RemoveProxy"] = 2;
            
            table["account.RegisterName"] = 2;
            table["account.UnregisterName"] = 1;
            table["account.RegisterScript"] = 2;
            
            table["storage.UploadData"] = 6;
            table["storage.UploadFile"] = 7;
            table["storage.DeleteFile"] = 2;
            table["storage.SetForeignSpace"] = 2;

            // TODO add more here
            return table;
        }

        public static IEnumerable<DisasmMethodCall> ExtractMethodCalls(Disassembler disassembler, Dictionary<string, int> methodArgumentCountTable)
        {
            var instructions = disassembler.Instructions.ToArray();
            var result = new List<DisasmMethodCall>();

            int index = 0;
            var regs = new VMObject[16];
            var stack = new Stack<VMObject>();
            while (index < instructions.Length)
            {
                var instruction = instructions[index];

                switch (instruction.Opcode)
                {
                    case Opcode.LOAD:
                        {
                            var dst = (byte)instruction.Args[0];
                            var type = (VMType)instruction.Args[1];
                            var bytes = (byte[])instruction.Args[2];

                            regs[dst] = new VMObject();
                            regs[dst].SetValue(bytes, type);

                            break;
                        }

                    case Opcode.PUSH:
                        {
                            var src = (byte)instruction.Args[0];
                            var val = regs[src];

                            var temp = new VMObject();
                            temp.Copy(val);
                            stack.Push(temp);
                            break;
                        }

                    case Opcode.CTX:
                        {
                            var src = (byte)instruction.Args[0];
                            var dst = (byte)instruction.Args[1];

                            regs[dst] = new VMObject();
                            regs[dst].Copy(regs[src]);
                            break;
                        }

                    case Opcode.SWITCH:
                        {
                            var src = (byte)instruction.Args[0];
                            var val = regs[src];

                            var contractName = regs[src].AsString();
                            var methodName = stack.Pop().AsString();
                            var args = PopArgs(contractName, methodName, stack, methodArgumentCountTable);
                            result.Add(new DisasmMethodCall() { MethodName = methodName, ContractName = contractName, Arguments = args });
                            break;
                        }

                    case Opcode.EXTCALL:
                        {
                            var src = (byte)instruction.Args[0];
                            var methodName = regs[src].AsString();
                            var args = PopArgs(null, methodName, stack, methodArgumentCountTable);
                            result.Add(new DisasmMethodCall() { MethodName = methodName, ContractName = "", Arguments = args });
                            break;
                        }
                }

                index++;
            }

            return result;
        }

        public static IEnumerable<DisasmMethodCall> ExtractMethodCalls(byte[] script, Dictionary<string, int> methodArgumentCountTable)
        {
            var disassembler = new Disassembler(script);
            return ExtractMethodCalls(disassembler, methodArgumentCountTable);
        }
    }
}
