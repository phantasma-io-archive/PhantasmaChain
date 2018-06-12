using Phantasma.Core;
using Phantasma.VM;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Phantasma.Contracts
{
    public interface IRuntime
    {
        ITransaction Transaction { get; }
        IFungibleToken NativeToken { get; }
        BigInteger CurrentHeight { get; }

        Block GetBlock(BigInteger height);

        T GetContract<T>(Address address) where T : IContractABI;
    }

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

        internal void RegisterMethod(string name, Action<VirtualMachine> handler)
        {
            handlers[name] = handler;
        }

        public IFungibleToken NativeToken => throw new NotImplementedException();

        public BigInteger CurrentHeight => Chain.Height;

        private Dictionary<string, Action<VirtualMachine>> handlers = new Dictionary<string, Action<VirtualMachine>>();

        public override bool ExecuteInterop(string method)
        {
            if (handlers.ContainsKey(method))
            {
                handlers[method](this);
                return true;
            }

            return false;
        }

        public Block GetBlock(BigInteger height)
        {
            throw new System.NotImplementedException();
        }

        public T GetContract<T>(Address address) where T : IContractABI
        {
            throw new System.NotImplementedException();
        }

        public override ExecutionContext LoadContext(byte[] key)
        {
            throw new NotImplementedException();
        }
    }
}
