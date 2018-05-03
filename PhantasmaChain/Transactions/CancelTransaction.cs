using PhantasmaChain.Core;
using System;
using System.IO;
using System.Numerics;

namespace PhantasmaChain.Transactions
{
    public class CancelTransaction : Transaction
    {
        public readonly byte[] orderID;

        public CancelTransaction(byte[] publicKey, BigInteger fee, uint txOrder) : base(TransactionKind.Cancel, publicKey, fee, txOrder)
        {
        }

        public override BigInteger GetCost(Chain chain)
        {
            throw new NotImplementedException();
        }

        protected override void Apply(Chain chain, Action<Event> notify)
        {
            throw new NotImplementedException();
        }

        protected override void SerializeData(BinaryWriter writer)
        {
            throw new NotImplementedException();
        }

        protected override void UnserializeData(BinaryReader reader)
        {
            throw new NotImplementedException();
        }

        protected override bool ValidateData(Chain chain)
        {
            throw new NotImplementedException();
        }
    }
}
