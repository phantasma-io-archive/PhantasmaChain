using PhantasmaChain.Core;
using System;
using System.IO;
using System.Linq;
using System.Numerics;

namespace PhantasmaChain.Transactions
{
    public class MintTransaction : Transaction
    {
        public byte[] TokenID { get; private set; }
        public BigInteger Amount { get; private set; }

        public MintTransaction(byte[] publicKey, BigInteger fee, uint txOrder, byte[] tokenID, BigInteger amount) : base(TransactionKind.Mint, publicKey, fee, txOrder)
        {
            this.TokenID = tokenID;
            this.Amount = Amount;
        }

        public override BigInteger GetCost(Chain chain)
        {
            return 1;
        }

        protected override void SerializeData(BinaryWriter writer)
        {
            writer.WriteByteArray(this.TokenID);
            writer.WriteBigInteger(this.Amount);
        }

        protected override void UnserializeData(BinaryReader reader)
        {
            this.TokenID = reader.ReadByteArray();
            this.Amount = reader.ReadBigInteger();
        }

        protected override bool ValidateData(Chain chain)
        {
            var account = chain.GetAccount(this.PublicKey);
            if (account == null)
            {
                return false;
            }

            var token = chain.GetTokenByID(this.TokenID);
            if (token == null)
            {
                return false;
            }

            if (!token.OwnerPublicKey.SequenceEqual(this.PublicKey))
            {
                return false;
            }

            return true;
        }

        protected override void Apply(Chain chain, Action<Event> notify)
        {
            var token = chain.GetTokenByID(this.TokenID);
            var account = chain.GetAccount(this.PublicKey);
            account.Deposit(token, this.Amount, notify);
            token.Mint(this.Amount);
        }
    }
}
