using System;
using System.Collections.Generic;
using Phantasma.VM.Contracts;
using Phantasma.VM;
using Phantasma.Cryptography;
using Phantasma.IO;
using Phantasma.Blockchain.Storage;
using Phantasma.Core;

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

        public RuntimeVM(byte[] script, Chain chain, Block block, Transaction transaction, StorageChangeSetContext changeSet) : base(script)
        {
            Throw.IfNull(chain, nameof(chain));
            Throw.IfNull(changeSet, nameof(changeSet));

            // NOTE: block and transaction can be null, required for Chain.InvokeContract
            //Throw.IfNull(block, nameof(block));
            //Throw.IfNull(transaction, nameof(transaction));

            this.Chain = chain;
            this.Block = block;
            this.Transaction = transaction;
            this.ChangeSet = changeSet;
            Chain.RegisterInterop(this);
        }

        internal void RegisterMethod(string name, Func<VirtualMachine, ExecutionState> handler)
        {
            handlers[name] = handler;
        }

        private Dictionary<string, Func<VirtualMachine, ExecutionState>> handlers = new Dictionary<string, Func<VirtualMachine, ExecutionState>>();

        public override ExecutionState ExecuteInterop(string method)
        {
            if (handlers.ContainsKey(method))
            {
                return handlers[method](this);
            }

            return ExecutionState.Fault;
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

            var evt = new Event(kind, address, bytes);
            _events.Add(evt);
        }

        public void Expect(bool condition, string description)
        {
            Throw.If(!condition, $"contract assertion failed: {description}");
        }

    }
}
