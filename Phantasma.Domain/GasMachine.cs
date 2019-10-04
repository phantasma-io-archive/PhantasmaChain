using Phantasma.Numerics;
using Phantasma.VM;
using System;
using System.Collections.Generic;

namespace Phantasma.Domain
{
    internal class DummyExecutionContext : ExecutionContext
    {
        public override string Name => _name;

        private string _name;

        public DummyExecutionContext(string name)
        {
            this._name = name;
        }

        public override ExecutionState Execute(ExecutionFrame frame, Stack<VMObject> stack)
        {
            return ExecutionState.Halt;
        }
    }

    public class GasMachine : VirtualMachine
    {
        public GasMachine(byte[] script): base(script)
        {
            UsedGas = 0;
        }

        public BigInteger UsedGas { get; protected set; }

#if DEBUG
        public override void DumpData(List<string> lines)
        {
            throw new NotImplementedException();
        }
#endif

        public override ExecutionState ExecuteInterop(string method)
        {
            return ExecutionState.Running;
       }

        public override ExecutionContext LoadContext(string contextName)
        {
            return new DummyExecutionContext(contextName);
        }

        public virtual ExecutionState ConsumeGas(BigInteger gasCost)
        {
            UsedGas += gasCost;
            return ExecutionState.Running;
        }

        public override ExecutionState ValidateOpcode(Opcode opcode)
        {
            var gasCost = GetGasCostForOpcode(opcode);
            return ConsumeGas(gasCost);
        }

        public static BigInteger GetGasCostForOpcode(Opcode opcode)
        {
            switch (opcode)
            {
                case Opcode.GET:
                case Opcode.PUT:
                case Opcode.CALL:
                case Opcode.LOAD:
                    return 5;

                case Opcode.EXTCALL:
                case Opcode.CTX:
                    return 10;

                case Opcode.SWITCH:
                    return 100;

                case Opcode.NOP:
                case Opcode.RET:
                    return 0;

                default: return 1;
            }
        }
    }
}
