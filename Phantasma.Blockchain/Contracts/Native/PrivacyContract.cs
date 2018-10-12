using Phantasma.Cryptography;
using Phantasma.Cryptography.Ring;
using Phantasma.Numerics;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Contracts.Native
{
    internal struct PrivacyQueue
    {
        public uint ID;
        public string symbol;
        public List<Address> addresses;
        public List<RingSignature> signatures;
    }

    public sealed class PrivacyContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Privacy;

        public static readonly BigInteger TransferAmount = 10;

        private PrivacyQueue _queue;

        public PrivacyContract() : base()
        {
            _queue = new PrivacyQueue() { addresses = new List<Address>(), signatures = new List<RingSignature>(),  symbol = "SOUL", ID = 333 };
        }

        public uint PutPrivate(Address from, string symbol)
        {
            Expect(IsWitness(from));

            var token = this.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Chain.GetTokenBalances(token);
            var balance = balances.Get(from);
            Expect(balance >= TransferAmount);

            var queue = _queue;
            foreach (var address in queue.addresses)
            {
                Expect(address != from);
            }

            balances.Subtract(from, TransferAmount);
            queue.addresses.Add(from);

            return queue.ID; // TODO should be quueue ID
        }

        public void TakePrivate(Address to, string symbol, uint queueID, RingSignature signature)
        {
            var queue = _queue;

            Expect(queue.symbol == symbol);
            Expect(queue.ID == queueID);

            var msg = this.Transaction.ToByteArray(false);
            Expect(signature.Verify(msg, queue.addresses));

            var token = this.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            foreach (var otherSignature in queue.signatures)
            {
                Expect(!signature.IsLinked(otherSignature));
            }

            queue.signatures.Add(signature);

            var balances = this.Chain.GetTokenBalances(token);
            balances.Add(to, TransferAmount);
        }
    }
}
