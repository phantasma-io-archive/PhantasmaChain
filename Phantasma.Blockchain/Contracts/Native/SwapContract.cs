using Phantasma.Blockchain.Storage;
using Phantasma.Cryptography;
using Phantasma.Numerics;
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

            if (toSymbol == Runtime.Nexus.StakingToken.Symbol)
            {
                var quoteBalance = _quoteBalance.Get<string, BigInteger>(fromSymbol);
                Runtime.Expect(quoteBalance > 0, "invalid balance");
            }
            else
            if (fromSymbol == Runtime.Nexus.StakingToken.Symbol)
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
