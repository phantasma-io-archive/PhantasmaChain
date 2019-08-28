using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Storage.Context;
using System;
using Phantasma.Storage;
using System.IO;
using Phantasma.Storage.Utils;

namespace Phantasma.Blockchain.Tokens
{
    [Flags]
    public enum TokenFlags
    {
        None = 0,
        Transferable = 1 << 0,
        Fungible = 1 << 1,
        Finite = 1 << 2,
        Divisible = 1 << 3,
        Fuel = 1 << 4,
        Stakable = 1 << 5,
        Stable = 1 << 6,
        External = 1 << 7,
        Burnable = 1 << 8,
    }

    public struct TokenInfo : ISerializable
    {
        public string Symbol { get; private set; }
        public string Name { get; private set; }

        public TokenFlags Flags { get; private set; }

        public BigInteger MaxSupply { get; private set; }

        public bool IsFungible => Flags.HasFlag(TokenFlags.Fungible);
        public bool IsBurnable => Flags.HasFlag(TokenFlags.Burnable);
        public bool IsCapped => MaxSupply > 0; // equivalent to Flags.HasFlag(TokenFlags.Infinite)

        public Address Owner { get; private set; }

        public int Decimals { get; private set; }

        public byte[] Script { get; private set; }

        internal TokenInfo(Address owner, string symbol, string name, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script)
        {
            this.Owner = owner;
            this.Symbol = symbol;
            this.Name = name;
            this.Flags = flags;
            this.Decimals = decimals;
            this.MaxSupply = maxSupply;
            this.Script = script;
        }

        public override string ToString()
        {
            return $"{Name} ({Symbol})";
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteAddress(Owner);
            writer.WriteVarString(Symbol);
            writer.WriteVarString(Name);
            writer.Write((uint)Flags);
            writer.Write(Decimals);
            writer.WriteBigInteger(MaxSupply);
            writer.WriteByteArray(Script);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Owner = reader.ReadAddress();
            Symbol = reader.ReadVarString();
            Name = reader.ReadVarString();
            Flags = (TokenFlags)reader.ReadUInt32();
            Decimals = reader.ReadInt32();
            MaxSupply = reader.ReadBigInteger();

            // TODO this try catch will be unecessary for mainnet
            try
            {
                Script = reader.ReadByteArray();
            }
            catch
            {
                Script = new byte[0];
            }
        }
    }
}