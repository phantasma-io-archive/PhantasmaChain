using Phantasma.Contracts.Interfaces;
using Phantasma.Contracts.Types;
using Phantasma.Core;
using Phantasma.VM;
using System.Numerics;

namespace Phantasma.Contracts
{
    public interface IRuntime
    {
        ITransaction Transaction { get; }
        IFungibleToken NativeToken { get; }
        BigInteger CurrentHeight { get; }

        Block GetBlock(BigInteger height);

        T GetContract<T>(Address address) where T : IContract;
    }

    public class RuntimeVM : VirtualMachine, IRuntime
    {
        private Transaction transaction;

        public RuntimeVM(Transaction tx) : base(tx.Script)
        {
            this.transaction = tx;
        }

        public ITransaction Transaction => throw new System.NotImplementedException();

        public IFungibleToken NativeToken => throw new System.NotImplementedException();

        public BigInteger CurrentHeight => throw new System.NotImplementedException();

        public override bool ExecuteInterop(string method)
        {
            throw new System.NotImplementedException();
        }

        public Block GetBlock(BigInteger height)
        {
            throw new System.NotImplementedException();
        }

        public T GetContract<T>(Address address) where T : IContract
        {
            throw new System.NotImplementedException();
        }
    }
}
