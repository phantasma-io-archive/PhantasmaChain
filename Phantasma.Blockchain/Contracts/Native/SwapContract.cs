using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class SwapContract : SmartContract
    {
        public override string Name => "swap";

        internal StorageMap _quoteBalance; //<string, BigInteger> 
        internal StorageMap _BaseBalance; //<string, BigInteger> 

        public SwapContract() : base()
        {
        }

        // returns how many tokens would be obtained by trading from one type of another
        public BigInteger GetRate(string fromSymbol, BigInteger fromAmount, string toSymbol)
        {
            Runtime.Expect(fromSymbol != toSymbol, "invalid pair");

            if (toSymbol == Nexus.StakingTokenSymbol)
            {
                var quoteBalance = _quoteBalance.Get<string, BigInteger>(fromSymbol);
                Runtime.Expect(quoteBalance > 0, "invalid balance");
            }
            else
            if (fromSymbol == Nexus.StakingTokenSymbol)
            {

            }
            else
            {
                throw new Exception("invalid pair");
            }

            return 0;
        }

        public void Swap(Address from, string fromSymbol, BigInteger fromAmount, string toSymbol)
        {

        }
    }
}
