using Phantasma.Contracts.Interfaces;
using Phantasma.Contracts.Types;
using System.Numerics;

namespace Phantasma.Contracts
{
    public static class Runtime
    {
        public static ITransaction Transaction { get; }
        public static IFungibleToken NativeToken;
        public static BigInteger CurrentHeight;

        public static extern Block GetBlock(BigInteger height);

        public static Block LastBlock
        {
            get
            {
                return GetBlock(CurrentHeight);
            }
        }

        public static extern T GetContract<T>(Address address) where T : IContract;
    }
}
