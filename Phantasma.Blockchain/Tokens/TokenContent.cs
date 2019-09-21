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

        public TokenContent(Address currentChain, Address currentOwner, byte[] ROM, byte[] RAM) : this()
        {
            CurrentChain = currentChain;
            CurrentOwner = currentOwner;
            this.ROM = ROM;
            this.RAM = RAM;
        }

        public Address CurrentChain { get; private set; }
        public Address CurrentOwner { get; private set; }
        public byte[] ROM { get; private set; }
        public byte[] RAM { get; private set; }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteAddress(CurrentChain);
            writer.WriteAddress(CurrentOwner);
            writer.WriteByteArray(ROM);
            writer.WriteByteArray(RAM);
        }

        public void UnserializeData(BinaryReader reader)
        {
            CurrentChain = reader.ReadAddress();
            CurrentOwner = reader.ReadAddress();

            ROM = reader.ReadByteArray();
            ROM = ROM ?? new byte[0];

            RAM = reader.ReadByteArray();
            RAM = RAM ?? new byte[0];
        }
    }
}
