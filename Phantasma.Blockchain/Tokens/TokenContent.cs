using System.IO;
using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.Blockchain.Tokens
{
    public struct TokenContent: ISerializable
    {
        // sizes in bytes
        // TODO find optimal values for this
        public static readonly int MaxROMSize = 256;
        public static readonly int MaxRAMSize = 256;

        public TokenContent(Address currentChain, Address currentOwner, byte[] ROM, byte[] RAM, BigInteger value) : this()
        {
            CurrentChain = currentChain;
            CurrentOwner = currentOwner;
            this.ROM = ROM;
            this.RAM = RAM;
            this.Value = value;
        }

        public Address CurrentChain { get; private set; }
        public Address CurrentOwner { get; private set; }
        public byte[] ROM { get; private set; }
        public byte[] RAM { get; private set; }
        public BigInteger Value { get; private set; } // in KCAL

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteAddress(CurrentChain);
            writer.WriteAddress(CurrentOwner);
            writer.WriteBigInteger(Value);
            writer.WriteByteArray(ROM);
            writer.WriteByteArray(RAM);
        }

        public void UnserializeData(BinaryReader reader)
        {
            CurrentChain = reader.ReadAddress();
            CurrentOwner = reader.ReadAddress();
            Value = reader.ReadBigInteger();

            ROM = reader.ReadByteArray();
            ROM = ROM ?? new byte[0];

            RAM = reader.ReadByteArray();
            RAM = RAM ?? new byte[0];
        }
    }
}
