using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.VM;
using System;
using System.Collections.Generic;

namespace Phantasma.Blockchain
{
    public abstract class DescriptionVM : VirtualMachine
    {
        public DescriptionVM(byte[] script) : base(script)
        {
        }

        public abstract IToken FetchToken(string symbol);
        public abstract string OutputAddress(Address address);
        public abstract string OutputSymbol(string symbol);

        public override void DumpData(List<string> lines)
        {
            // do nothing
        }

        private static readonly string OutputTag = "Output.";

        public override ExecutionState ExecuteInterop(string method)
        {
            if (method.StartsWith(OutputTag))
            {
                method = method.Substring(OutputTag.Length);
                switch (method)
                {
                    case "Decimals":
                        {
                            var amount = this.PopNumber("amount");
                            var symbol = this.PopString("symbol");

                            var info = FetchToken(symbol);

                            var result = UnitConversion.ToDecimal(amount, info.Decimals);

                            this.Stack.Push(VMObject.FromObject(result.ToString()));
                            return ExecutionState.Running;
                        }

                    case "Account":
                        {
                            var temp = this.Stack.Pop();
                            Address addr;
                            if (temp.Type == VMType.String)
                            {
                                var text = temp.AsString();
                                Expect(Address.IsValidAddress(text), $"expected valid address");
                                addr = Address.FromText(text);
                            }
                            else
                            if (temp.Type == VMType.Bytes)
                            {
                                var bytes = temp.AsByteArray();
                                addr = Serialization.Unserialize<Address>(bytes);
                            }
                            else
                            {
                                addr = temp.AsInterop<Address>();
                            }

                            var result = OutputAddress(addr);
                            this.Stack.Push(VMObject.FromObject(result.ToString()));
                            return ExecutionState.Running;
                        }

                    case "Symbol":
                        {
                            var symbol = this.PopString("symbol");
                            var result = OutputSymbol(symbol);
                            this.Stack.Push(VMObject.FromObject(result.ToString()));
                            return ExecutionState.Running;
                        }

                    default:
                        throw new VMException(this, $"unknown interop: {OutputTag}{method}");

                }
            }

            throw new VMException(this, "unknown interop: " + method);
        }

        public override ExecutionContext LoadContext(string contextName)
        {
            throw new NotImplementedException();
        }
    }

}
