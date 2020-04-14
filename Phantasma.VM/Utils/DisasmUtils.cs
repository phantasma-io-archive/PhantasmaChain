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
                throw new System.Exception("Cannot disassemble method arguments => " + method);
            }
        }

        public static Dictionary<string, int> GetDefaultDisasmTable()
        {
            var table = new Dictionary<string, int>();
            table["gas.AllowGas"] = 4;
            table["gas.SpendGas"] = 1;
            table["Runtime.TransferTokens"] = 4;
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
