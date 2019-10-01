using System;
using System.IO;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.Domain
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
        Fiat = 1 << 6,
        External = 1 << 7,
        Burnable = 1 << 8,
    }

    public interface IToken
    {
        string Name { get; }
        string Symbol { get; }
        string Platform { get; }
        Hash Hash { get; }
        TokenFlags Flags { get; }
        BigInteger MaxSupply { get;  }
        int Decimals { get; }
        byte[] Script { get; }
    }

    public struct PackedNFTData
    {
        public readonly string Symbol;
        public readonly byte[] ROM;
        public readonly byte[] RAM;

        public PackedNFTData(string symbol, byte[] rom, byte[] ram)
        {
            Symbol = symbol;
            ROM = rom;
            RAM = ram;
        }
    }

    public struct TokenContent : ISerializable
    {
        // sizes in bytes
        // TODO find optimal values for this
        public static readonly int MaxROMSize = 256;
        public static readonly int MaxRAMSize = 256;

        public TokenContent(string currentChain, Address currentOwner, byte[] ROM, byte[] RAM) : this()
        {
            CurrentChain = currentChain;
            CurrentOwner = currentOwner;
            this.ROM = ROM;
            this.RAM = RAM;
        }

        public string CurrentChain { get; private set; }
        public Address CurrentOwner { get; private set; }
        public byte[] ROM { get; private set; }
        public byte[] RAM { get; private set; }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(CurrentChain);
            writer.WriteAddress(CurrentOwner);
            writer.WriteByteArray(ROM);
            writer.WriteByteArray(RAM);
        }

        public void UnserializeData(BinaryReader reader)
        {
            CurrentChain = reader.ReadVarString();
            CurrentOwner = reader.ReadAddress();

            ROM = reader.ReadByteArray();
            ROM = ROM ?? new byte[0];

            RAM = reader.ReadByteArray();
            RAM = RAM ?? new byte[0];
        }
    }

}
