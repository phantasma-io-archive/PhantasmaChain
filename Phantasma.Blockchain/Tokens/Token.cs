using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Storage;
using System.IO;
using Phantasma.Storage.Utils;
using Phantasma.Domain;

namespace Phantasma.Blockchain.Tokens
{
    public struct TokenInfo : IToken, ISerializable
    {
        public string Symbol { get; private set; }
        public string Name { get; private set; }
        public string Platform { get; private set; }
        public Hash Hash { get; private set; }
        public TokenFlags Flags { get; private set; }
        public BigInteger MaxSupply { get; private set; }    
        public int Decimals { get; private set; }
        public byte[] Script { get; private set; }

        internal TokenInfo(string symbol, string name, string platform, Hash hash, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script)
        {
            this.Symbol = symbol;
            this.Name = name;
            this.Platform = platform;
            this.Hash = hash;
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
            writer.WriteVarString(Symbol);
            writer.WriteVarString(Name);
            writer.WriteVarString(Platform);
            writer.WriteHash(Hash);
            writer.Write((uint)Flags);
            writer.Write(Decimals);
            writer.WriteBigInteger(MaxSupply);
            writer.WriteByteArray(Script);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Symbol = reader.ReadVarString();
            Name = reader.ReadVarString();
            Platform = reader.ReadVarString();
            Hash = reader.ReadHash();
            Flags = (TokenFlags)reader.ReadUInt32();
            Decimals = reader.ReadInt32();
            MaxSupply = reader.ReadBigInteger();
            Script = reader.ReadByteArray();
        }
    }
}