using System;
using System.Collections.Generic;
using System.Numerics;
using Phantasma.VM.Contracts;
using Phantasma.VM;
using Phantasma.VM.Types;

namespace Phantasma.Blockchain
{
    public class RuntimeVM : VirtualMachine, IRuntime
    {
        public ITransaction Transaction { get; private set; }
        public Chain Chain { get; private set; }

        public RuntimeVM(Chain chain, Transaction tx) : base(tx.Script)
        {
            this.Transaction = tx;
            this.Chain = chain;

            chain.RegisterInterop(this);
        }

        internal void RegisterMethod(string name, Func<VirtualMachine, ExecutionState> handler)
        {
            handlers[name] = handler;
        }

        public IFungibleToken NativeToken => throw new NotImplementedException();

        public BigInteger CurrentHeight => Chain.Height;

        private Dictionary<string, Func<VirtualMachine, ExecutionState>> handlers = new Dictionary<string, Func<VirtualMachine, ExecutionState>>();

        public override ExecutionState ExecuteInterop(string method)
        {
            if (handlers.ContainsKey(method))
            {
                return handlers[method](this);
            }

            return ExecutionState.Fault;
        }

        public IBlock GetBlock(BigInteger height)
        {
            throw new System.NotImplementedException();
        }

        public T GetContract<T>(Address address) where T : IContract
        {
            throw new System.NotImplementedException();
        }

        public override ExecutionContext LoadContext(byte[] key)
        {
            throw new NotImplementedException();
        }
    }
}
