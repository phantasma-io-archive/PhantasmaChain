using Phantasma.Contracts;
using System;
using System.Numerics;

namespace Phantasma.Core
{
    public class Token: IFungibleToken
    {
        public string Symbol { get; }
        public string Name { get; }
        public byte[] PublicKey { get; }
        public byte[] OwnerPublicKey;
        public TokenAttribute Attributes { get; }
        public BigInteger MaxSupply { get; private set; }
        public BigInteger CirculatingSupply { get; private set; }
        public BigInteger Decimals { get; private set; }

        public Token(string symbol, string name, BigInteger initialSupply, BigInteger totalSupply, TokenAttribute flags, byte[] ownerPublicKey)
        {
            this.Symbol = symbol;
            this.Name = name;
            this.CirculatingSupply = initialSupply;
            this.MaxSupply = totalSupply;
            this.Attributes = flags;
            this.OwnerPublicKey = ownerPublicKey;
        }

        public bool HasAttribute(TokenAttribute attr)
        {
            return ((this.Attributes & attr) == 0);
        }

        public bool Burn(BigInteger amount)
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

        public bool Mint(BigInteger amount)
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

        public bool Send(Address destination, BigInteger amount)
        {
            throw new NotImplementedException();
        }

        public BigInteger BalanceOf(Address address)
        {
            throw new NotImplementedException();
        }
    }
}
