using Phantasma.Numerics;
using Phantasma.VM;
using System;
using System.Collections.Generic;

namespace Phantasma.Domain
{
    public class GasMachine : VirtualMachine
    {
        public GasMachine(byte[] script): base(script)
        {
            UsedGas = 0;
        }

        public BigInteger UsedGas { get; protected set; }

        public override void DumpData(List<string> lines)
        {
            throw new NotImplementedException();
        }

        public override ExecutionState ExecuteInterop(string method)
        {
            throw new NotImplementedException();
        }

        public override ExecutionContext LoadContext(string contextName)
        {
            throw new NotImplementedException();
        }

        public virtual ExecutionState ConsumeGas(BigInteger gasCost)
        {
            UsedGas += gasCost;
            return ExecutionState.Running;
        }
    }
}
