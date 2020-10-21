using Phantasma.Blockchain.Tokens;
using Phantasma.Numerics;
using Phantasma.VM;
using System;
using System.Collections.Generic;

namespace Phantasma.Blockchain
{
    public class DescriptionVM : VirtualMachine
    {
        private Func<string, TokenInfo> tokenFetcher;

        public DescriptionVM(byte[] script, Func<string, TokenInfo> tokenFetcher) : base(script)
        {
            this.tokenFetcher = tokenFetcher;
        }

        public override void DumpData(List<string> lines)
        {
            // do nothing
        }

        public override ExecutionState ExecuteInterop(string method)
        {
            switch (method)
            {
                case "decimals":
                    {
                        var amount = this.PopNumber("amount");
                        var symbol = this.PopString("symbol");

                        var info = tokenFetcher(symbol);

                        var result = UnitConversion.ToDecimal(amount, info.Decimals);

                        this.Stack.Push(VMObject.FromObject(result.ToString()));
                        return ExecutionState.Running;
                    }

                default:
                    throw new VMException(this, "unknown interop: " + method);
            }
        }

        public override ExecutionContext LoadContext(string contextName)
        {
            throw new NotImplementedException();
        }
    }

}
