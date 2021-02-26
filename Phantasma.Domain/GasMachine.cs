using System;
using System.Numerics;
using System.Collections.Generic;
using Phantasma.VM;

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
        public GasMachine(byte[] script, uint offset, string contextName = null) : base(script, offset, contextName)
        {
            UsedGas = 0;
        }

        public BigInteger UsedGas { get; protected set; }

        public override void DumpData(List<string> lines)
        {
            throw new NotImplementedException();
        }

        public override ExecutionContext LoadContext(string contextName)
        {
            return new DummyExecutionContext(contextName);
        }

        public override ExecutionState ExecuteInterop(string method)
        {
            BigInteger gasCost;

            // construtor
            if (method.EndsWith("()"))
            {
                gasCost = 10;
            }
            else
            {
                int dotPos = method.IndexOf('.');
                Expect(dotPos > 0, "extcall is missing namespace");

                var methodNamespace = method.Substring(0, dotPos);
                switch (methodNamespace)
                {
                    case "Runtime":
                    case "Data":
                    case "Map":
                    case "List":
                    case "Set":
                        gasCost = 50;
                        break;

                    case "Nexus":
                        gasCost = 1000;
                        break;

                    case "Organization":
                    case "Oracle":
                        gasCost = 200;
                        break;

                    case "Account":
                    case "Leaderboard":
                        gasCost = 100;
                        break;

                    default:
                        Expect(false, "invalid extcall namespace: " + methodNamespace);
                        gasCost = 0;
                        break;
                }
            }

            if (gasCost > 0)
            {
                ConsumeGas(gasCost);
            }

            return ExecutionState.Running;
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
