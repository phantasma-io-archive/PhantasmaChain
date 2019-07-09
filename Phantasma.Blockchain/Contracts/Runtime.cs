using System;
using System.Collections.Generic;
using Phantasma.VM.Contracts;
using Phantasma.VM;
using Phantasma.Cryptography;
using Phantasma.Core;
using Phantasma.Numerics;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using Phantasma.Storage.Context;
using Phantasma.Storage;

namespace Phantasma.Blockchain.Contracts
{
    public class RuntimeVM : VirtualMachine
    {
        public Transaction Transaction { get; private set; }
        public Chain Chain { get; private set; }
        public Chain ParentChain { get; private set; }
        public Block Block { get; private set; }
        public Nexus Nexus => Chain.Nexus;
        public Timestamp Time => Block != null ? Block.Timestamp : Timestamp.Now;

        private List<Event> _events = new List<Event>();
        public IEnumerable<Event> Events => _events;

        public Address FeeTargetAddress { get; private set; }

        public StorageChangeSetContext ChangeSet { get; private set; }

        public BigInteger UsedGas { get; private set; }
        public BigInteger PaidGas { get; private set; }
        public BigInteger MaxGas { get; private set; }
        public BigInteger GasPrice { get; private set; }
        public readonly bool readOnlyMode;

        public OracleReaderDelegate OracleReader = null;

        private BigInteger seed;

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
            this.MaxGas = 10000;  // a minimum amount required for allowing calls to Gas contract etc

            this.Chain = chain;
            this.Block = block;
            this.Transaction = transaction;
            this.ChangeSet = changeSet;
            this.readOnlyMode = readOnlyMode;

            this.FeeTargetAddress = Address.Null;

            if (this.Chain != null && !Chain.IsRoot)
            {
                var parentName = chain.Nexus.GetParentChainByName(chain.Name);
                this.ParentChain = chain.Nexus.FindChainByName(parentName);
            }
            else
            {
                this.ParentChain = null;
            }

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
                if (PaidGas < UsedGas && Nexus.Ready)
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
            var contract = this.Nexus.FindContract(contextName);
            if (contract != null)
            {
                contract.SetRuntimeData(this);
                return Chain.GetContractContext(contract);
            }

            return null;
        }

        public object CallContext(string contextName, string methodName, params object[] args)
        {
            var context = LoadContext(contextName);
            Expect(context != null, "could not call context: " + contextName);

            for (int i= args.Length - 1; i>=0; i--)
            {
                var obj = VMObject.FromObject(args[i]);
                this.Stack.Push(obj);
            }

            this.Stack.Push(VMObject.FromObject(methodName));

            var temp = context.Execute(this.CurrentFrame, this.Stack);
            Expect(temp == ExecutionState.Halt, "expected call success");

            Expect(this.Stack.Count > 0, "expected result on stack");
            return this.Stack.Pop().ToObject();
        }

        public void Notify<T>(Enum kind, Address address, T content)
        {
            var intVal = (int)(object)kind;
            Notify<T>((EventKind)(EventKind.Custom + intVal), address, content);
        }

        public void Notify<T>(EventKind kind, Address address, T content)
        {
            var bytes = content == null ? new byte[0] : Serialization.Serialize(content);

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

                        if (address != this.Chain.Address)
                        {
                            this.FeeTargetAddress = address;
                        }

                        break;
                    }
            }

            Notify(kind, address, bytes);
        }

        public void Notify(EventKind kind, Address address, byte[] bytes)
        {
            var evt = new Event(kind, address, bytes);
            _events.Add(evt);
        }

        public static readonly uint RND_A = 16807;
        public static readonly uint RND_M = 2147483647;

        // returns a first initial pseudo random number
        private BigInteger Randomize(byte[] init)
        {
            byte[] temp;

            if (Block != null)
            {
                temp = Block.Hash.ToByteArray();
            }
            else
            {
                var time = System.BitConverter.GetBytes(Time.Value);
                temp = ByteArrayUtils.ConcatBytes(time, init);
            }

            var seed = new BigInteger(temp);
            return seed;
        }

        // returns a next random number
        public BigInteger NextRandom()
        {
            seed = ((RND_A * seed) % RND_M);
            return seed;
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
            if (readOnlyMode || !Nexus.Ready)
            {
                return ExecutionState.Running;
            }

            var gasCost = GetGasCostForOpcode(opcode);
            return ConsumeGas(gasCost);
        }

        public ExecutionState ConsumeGas(BigInteger gasCost)
        {
            if (gasCost == 0)
            {
                return ExecutionState.Running;
            }

            if (gasCost < 0)
            {
                Throw.If(gasCost < 0, "invalid gas amount");
            }

            // required for allowing transactions to occur pre-minting of native token
            if (readOnlyMode || !Nexus.Ready)
            {
                return ExecutionState.Running;
            }

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
