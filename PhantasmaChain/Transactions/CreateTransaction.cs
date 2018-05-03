using PhantasmaChain.Core;
using PhantasmaChain.Cryptography;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace PhantasmaChain.Transactions
{
    public class CreateTransaction : Transaction
    {
        public byte[] TokenID { get; private set; }
        public string Name { get; private set; }
        public Token.Attribute Attributes { get; private set; }
        public BigInteger InitialSupply { get; private set; }
        public BigInteger TotalSupply { get; private set; }

        public CreateTransaction(byte[] publicKey, BigInteger fee, uint txOrder, byte[] tokenID, string name, BigInteger initialSupply, BigInteger totalSupply, Token.Attribute attributes) : base(TransactionKind.Create, publicKey, fee, txOrder)
        {
            this.TokenID = tokenID;
            this.Name = name;
            this.InitialSupply = initialSupply;
            this.TotalSupply = totalSupply;
            this.Attributes = attributes;
        }

        protected override void SerializeData(BinaryWriter writer)
        {
            writer.WriteByteArray(this.TokenID);
            writer.WriteShortString(this.Name);
            writer.Write((byte)this.Attributes);
            writer.WriteBigInteger(this.InitialSupply);
            writer.WriteBigInteger(this.TotalSupply);
        }

        protected override void UnserializeData(BinaryReader reader)
        {
            this.TokenID = reader.ReadByteArray();
            this.Name = reader.ReadShortString();
            this.Attributes = (Token.Attribute) reader.ReadByte();
            this.InitialSupply = reader.ReadBigInteger();
            this.TotalSupply = reader.ReadBigInteger();
        }

        protected override bool ValidateData(Chain chain)
        {
            if (this.TotalSupply <= 0)
            {
                return false;
            }

            if (this.InitialSupply > this.TotalSupply)
            {
                return false;
            }

            if (this.InitialSupply != this.TotalSupply && ((this.Attributes & Token.Attribute.Mintable) == 0))
            {
                return false;
            }

            if (this.TokenID.Length < 1 || this.TokenID.Length > 8)
            {
                return false;
            }

            if (this.Name.Length < 1 || this.Name.Length > 64)
            {
                return false;
            }

            // check if token already exists
            if (chain.GetTokenByID(this.TokenID) != null)
            {
                return false;
            }

            if (chain.GetTokenByName(this.Name) != null)
            {
                return false;
            }

            return true;
        }

        protected override void Apply(Chain chain, Action<Event> notify)
        {
            var token = chain.CreateToken(this.PublicKey, this.TokenID, this.Name, this.InitialSupply, this.TotalSupply, this.Attributes, notify);
            notify(new Event(EventKind.Token, this.PublicKey));
        }

        public override BigInteger GetCost(Chain chain)
        {
            return 0;
        }
    }
}
