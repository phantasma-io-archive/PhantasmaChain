using System;
using System.Collections.Generic;
using Phantasma.VM.Contracts;
using Phantasma.VM;
using Phantasma.Cryptography;
using Phantasma.IO;
using Phantasma.Blockchain.Storage;
using Phantasma.Core;
using Phantasma.Numerics;
using Phantasma.Blockchain.Contracts.Native;

namespace Phantasma.Blockchain.Contracts
{
    public class RuntimeVM : VirtualMachine
    {
        public Transaction Transaction { get; private set; }
        public Chain Chain { get; private set; }
        public Block Block { get; private set; }
        public Nexus Nexus => Chain.Nexus;
        
        private List<Event> _events = new List<Event>();
        public IEnumerable<Event> Events => _events;

        public StorageChangeSetContext ChangeSet { get; private set; }

        public BigInteger UsedGas { get; private set; }
        public BigInteger PaidGas { get; private set; }
        public BigInteger MaxGas { get; private set; }
        public BigInteger GasPrice { get; private set; }
        public readonly bool readOnlyMode;

        public RuntimeVM(byte[] script, Chain chain, Block block, Transaction transaction, StorageChangeSetContext changeSet, bool readOnlyMode) : base(script)
        {
            Throw.IfNull(chain, nameof(chain));
            Throw.IfNull(changeSet, nameof(changeSet));

            // NOTE: block and transaction can be null, required for Chain.InvokeContract
            //Throw.IfNull(block, nameof(block));
            //Throw.IfNull(transaction, nameof(transaction));

            this.GasPrice = 0;
            this.UsedGas = 0;
            this.PaidGas = 0;
            this.MaxGas = 100;  // a minimum amount required for allowing calls to Gas contract etc

            this.Chain = chain;
            this.Block = block;
            this.Transaction = transaction;
            this.ChangeSet = changeSet;
            this.readOnlyMode = readOnlyMode;

            Chain.RegisterInterop(this);
        }

        internal void RegisterMethod(string name, Func<RuntimeVM, ExecutionState> handler)
        {
            handlers[name] = handler;
        }

        private Dictionary<string, Func<RuntimeVM, ExecutionState>> handlers = new Dictionary<string, Func<RuntimeVM, ExecutionState>>();

        public override ExecutionState ExecuteInterop(string method)
        {
            if (handlers.ContainsKey(method))
            {
                return handlers[method](this);
            }

            return ExecutionState.Fault;
        }

        public override ExecutionState Execute()
        {
            var result = base.Execute();

            if (result == ExecutionState.Halt)
            {
                if (readOnlyMode)
                {
                    if (ChangeSet.Any())
                    {
#if DEBUG
                        throw new VMDebugException(this, "VM changeset modified in read-only mode");
#else
                        result = ExecutionState.Fault;
#endif
                    }
                }
                else
                if (PaidGas < UsedGas && Nexus.FuelToken != null)
                {
#if DEBUG
                    throw new VMDebugException(this, "VM unpaid gas");
#else
                                        result = ExecutionState.Fault;
#endif
                }
            }

            return result;
        }

        public T GetContract<T>(Address address) where T : IContract
        {
            throw new System.NotImplementedException();
        }

        public override ExecutionContext LoadContext(string contextName)
        {
            var contract = this.Chain.FindContract<SmartContract>(contextName);
            if (contract != null)
            {
                contract.SetRuntimeData(this);
                return Chain.GetContractContext(contract);
            }

            return null;
        }

        public void Notify<T>(EventKind kind, Address address, T content)
        {
            var bytes = content == null ? new byte[0]: Serialization.Serialize(content);

            switch (kind)
            {
                case EventKind.GasEscrow:
                    {
                        var gasInfo = (GasEventData)(object)content;
                        this.MaxGas = gasInfo.amount;
                        this.GasPrice = gasInfo.price;
                        break;
                    }

                case EventKind.GasPayment:
                    {
                        var gasInfo = (GasEventData)(object)content;
                        this.PaidGas += gasInfo.amount;
                        break;
                    }
            }

            var evt = new Event(kind, address, bytes);
            _events.Add(evt);
        }

        public void Expect(bool condition, string description)
        {
#if DEBUG
            if (!condition)
            {
                throw new VMDebugException(this, description);
            }
#endif

            Throw.If(!condition, $"contract assertion failed: {description}");
        }

        #region GAS
        public override ExecutionState ValidateOpcode(Opcode opcode)
        {
            // required for allowing transactions to occur pre-minting of native token
            if (readOnlyMode || Nexus.FuelToken == null || Nexus.FuelToken.CurrentSupply == 0)
            {
                return ExecutionState.Running;
            }

            var gasCost = GetGasCostForOpcode(opcode);
            Throw.If(gasCost < 0, "invalid gas amount");

            UsedGas += gasCost;

            if (UsedGas > MaxGas)
            {
#if DEBUG
                throw new VMDebugException(this, "VM gas limit exceeded");
#else
                                return ExecutionState.Fault;
#endif
            }

            return ExecutionState.Running;
        }

        public static BigInteger GetGasCostForOpcode(Opcode opcode)
        {
            switch (opcode)
            {
                case Opcode.GET:
                case Opcode.PUT:
                case Opcode.CALL:
                case Opcode.LOAD:
                    return 2;

                case Opcode.EXTCALL:
                    return 3;

                case Opcode.CTX:
                    return 5;

                case Opcode.SWITCH:
                    return 10;

                case Opcode.NOP:
                case Opcode.RET:
                    return 0;

                default: return 1;
            }
        }
        #endregion
    }
}
