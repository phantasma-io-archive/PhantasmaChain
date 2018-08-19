using Phantasma.VM.Contracts;
using Phantasma.VM.Types;
using System.Numerics;

namespace Phantasma.VM
{
    public interface IRuntime
    {
        ITransaction Transaction { get; }
        IFungibleToken NativeToken { get; }
        BigInteger CurrentHeight { get; }

        IBlock GetBlock(BigInteger height);

        T GetContract<T>(Address address) where T : IContract;
    }
}
