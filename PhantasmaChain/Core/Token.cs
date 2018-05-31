using System;
using System.Numerics;

namespace Phantasma.Core
{
    public class Token
    {
        [Flags]
        public enum Attribute
        {
            None = 0x0,
            Burnable = 0x1,
            Mintable = 0x2,
            Tradable = 0x4,
            Infinite = 0x8,
        }

        public readonly byte[] ID;
        public readonly string Name;
        public readonly byte[] OwnerPublicKey;
        public readonly Attribute Flags;
        public BigInteger TotalSupply { get; private set; }
        public BigInteger CurrentSupply { get; private set; }

        public Token(byte[] ID, string name, BigInteger initialSupply, BigInteger totalSupply, Attribute flags, byte[] ownerPublicKey)
        {
            this.ID = ID;
            this.Name = name;
            this.CurrentSupply = initialSupply;
            this.TotalSupply = totalSupply;
            this.Flags = flags;
            this.OwnerPublicKey = ownerPublicKey;
        }

        public bool HasAttribute(Attribute attr)
        {
            return ((this.Flags & attr) == 0);
        }

        public bool Burn(BigInteger amount)
        {
            if (this.CurrentSupply < amount || this.TotalSupply < amount)
            {
                return false;
            }

            if (!HasAttribute(Attribute.Burnable))
            {
                return false;
            }

            this.CurrentSupply -= amount;
            this.TotalSupply -= amount;
            return true;
        }

        public bool Mint(BigInteger amount)
        {
            if (!HasAttribute(Attribute.Infinite))
            {
                if (this.CurrentSupply + amount < this.TotalSupply)
                {
                    return false;
                }
            }

            if (!HasAttribute(Attribute.Mintable))
            {
                return false;
            }

            this.CurrentSupply += amount;
            return true;
        }

    }
}
