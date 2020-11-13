using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Storage;
using System.IO;
using Phantasma.Storage.Utils;
using Phantasma.Domain;
using Phantasma.Core;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Tokens
{
    public struct TokenInfo : IToken, ISerializable
    {
        public string Symbol { get; private set; }
        public string Name { get; private set; }
        public Address Owner { get; private set; }
        public TokenFlags Flags { get; private set; }
        public BigInteger MaxSupply { get; private set; }    
        public int Decimals { get; private set; }
        public byte[] Script { get; private set; }
        public ContractInterface ABI { get; private set; }

        public TokenInfo(string symbol, string name, Address owner, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script, ContractInterface ABI)
        {
            Throw.IfNullOrEmpty(symbol, nameof(symbol));
            Throw.IfNullOrEmpty(name, nameof(name));
            Throw.If(owner.IsNull, "token owner can't be null address");
            Throw.If(decimals < 0, "decimals can't be negative");
            Throw.If(flags == TokenFlags.None, "token must have flags set");
            Throw.If(script == null || script.Length == 0, "token script can't be empty");

            this.Symbol = symbol;
            this.Name = name;
            this.Owner = owner;
            this.Flags = flags;
            this.Decimals = decimals;
            this.MaxSupply = maxSupply;
            this.Script = script;
            this.ABI = ABI;            
        }

        public override string ToString()
        {
            return $"{Name} ({Symbol})";
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Symbol);
            writer.WriteVarString(Name);
            writer.WriteAddress(Owner);
            writer.Write((uint)Flags);
            writer.Write(Decimals);
            writer.WriteBigInteger(MaxSupply);
            writer.WriteByteArray(Script);

            var abiBytes = ABI.ToByteArray();
            writer.WriteByteArray(abiBytes);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Symbol = reader.ReadVarString();
            Name = reader.ReadVarString();
            Owner = reader.ReadAddress();
            Flags = (TokenFlags)reader.ReadUInt32();
            Decimals = reader.ReadInt32();
            MaxSupply = reader.ReadBigInteger();
            Script = reader.ReadByteArray();

            var abiBytes = reader.ReadByteArray();
            this.ABI = ContractInterface.FromBytes(abiBytes);
        }
    }
}