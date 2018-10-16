using System;
using System.Collections.Generic;
using Phantasma.VM.Contracts;
using Phantasma.VM;
using Phantasma.Cryptography;
using Phantasma.IO;

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

        public RuntimeVM(Chain chain, Block block, Transaction tx) : base(tx.Script)
        {
            this.Transaction = tx;
            this.Block = block;
            this.Chain = chain;

            chain.RegisterInterop(this);
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

        public override ExecutionContext LoadContext(Address address)
        {
            var nexus = this.Chain.Nexus;

            foreach (var entry in nexus.Chains)
            {
                if (entry.Address == address)
                {
                    var storage = this.Chain.FindStorage(address);
                    entry.Contract.SetRuntimeData(this, storage);
                    return entry.ExecutionContext;
                }
            }

            return null;
        }

        public void Notify<T>(EventKind kind, Address address, T content)
        {
            var bytes = content == null ? new byte[0]: Serialization.Serialize(content);

            var evt = new Event(kind, address, bytes);
            _events.Add(evt);
        }
    }
}
