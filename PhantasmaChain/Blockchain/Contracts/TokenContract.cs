using Phantasma.Mathematics;
using Phantasma.Cryptography;
using System;

namespace Phantasma.Blockchain.Contracts
{
    public class Token : NativeContract
    {
        internal override NativeContractKind Kind => NativeContractKind.Token;

        public string Symbol => "SOUL";
        public string Name => "Phantasma";

        public BigInteger MaxSupply => 93000000;
        public BigInteger CirculatingSupply => 50000000;
        public BigInteger Decimals => 8;

        public Token() : base()
        {
        }

/*        public bool HasAttribute(TokenAttribute attr)
        {
            return ((this.Attributes & attr) == 0);
        }

        public bool Burn(Address address, BigInteger amount)
        {
            if (this.CirculatingSupply < amount || this.MaxSupply < amount)
            {
                return false;
            }

            if (!HasAttribute(TokenAttribute.Burnable))
            {
                return false;
            }

            this.CirculatingSupply -= amount;
            this.MaxSupply -= amount;

            return true;
        }

        public bool Mint(Address address, BigInteger amount)
        {
            if (!HasAttribute(TokenAttribute.Infinite))
            {
                if (this.CirculatingSupply + amount < this.MaxSupply)
                {
                    return false;
                }
            }

            if (!HasAttribute(TokenAttribute.Mintable))
            {
                return false;
            }

            this.CirculatingSupply += amount;
            return true;
        }
        */

        public bool Send(Address destination, BigInteger amount)
        {
            throw new NotImplementedException();
        }

        public BigInteger BalanceOf(Address address)
        {
            return 0;
        }
    }
}