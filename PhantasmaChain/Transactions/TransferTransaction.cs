using PhantasmaChain.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace PhantasmaChain.Transactions
{
    public class TransferTransaction : Transaction
    {
        public byte[] TokenID { get; private set; }
        public byte[] DestinationPublicKey { get; private set; }
        public BigInteger Amount { get; private set; }

        public TransferTransaction(byte[] publicKey, BigInteger fee, uint txOrder, byte[] tokenID, byte[] destPublicKey, BigInteger amount) : base(TransactionKind.Transfer, publicKey, fee, txOrder)
        {
            this.TokenID = tokenID;
            this.DestinationPublicKey = destPublicKey;
            this.Amount = amount;
        }

        public override BigInteger GetCost(Chain chain)
        {
            return 0;
        }

        protected override void SerializeData(BinaryWriter writer)
        {
            writer.WriteByteArray(this.TokenID);
            writer.WriteByteArray(this.DestinationPublicKey);
            writer.WriteBigInteger(this.Amount);
        }

        protected override void UnserializeData(BinaryReader reader)
        {
            this.TokenID = reader.ReadByteArray();
            this.DestinationPublicKey = reader.ReadByteArray();
            this.Amount = reader.ReadBigInteger();
        }

        protected override bool ValidateData(Chain chain)
        {
            var token = chain.GetTokenByID(this.TokenID);
            if (token == null)
            {
                return false;
            }

            var account = chain.GetAccount(this.PublicKey);
            if (account == null)
            {
                return false;
            }

            var balance = account.GetBalance(token);

            return balance >= this.Amount;
        }

        protected override void Apply(Chain chain, Action<Event> notify)
        {
            var source = chain.GetAccount(this.PublicKey);
            if (source == null)
            {
                throw new ChainException("Source missing");
            }

            var dest = chain.GetOrCreateAccount(this.DestinationPublicKey);

            var token = chain.GetTokenByID(this.TokenID);

            source.Withdraw(token, this.Amount, notify);
            dest.Deposit(token, this.Amount, notify);
        }

    }
}
