using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Cryptography.Ring;
using Phantasma.Numerics;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Contracts.Native
{
    internal struct PrivacyQueue
    {
        public uint ID;
        public int size;
        public List<Address> addresses;
        public List<RingSignature> signatures;
    }

    public sealed class PrivacyContract : SmartContract
    {
        public override string Name => "privacy";

        public static readonly LargeInteger TransferAmount = 10;

        private Dictionary<Token, List<PrivacyQueue>> _queues = new Dictionary<Token, List<PrivacyQueue>>();

        public PrivacyContract() : base()
        {
        }

        private PrivacyQueue FindQueue(Token token, uint ID)
        {
            if (_queues.ContainsKey(token))
            {
                var list = _queues[token];
                foreach (var entry in list)
                {
                    if (entry.ID == ID)
                    {
                        return entry;
                    }
                }
            }

            return new PrivacyQueue();
        }

        private PrivacyQueue FetchQueue(Token token)
        {
            List<PrivacyQueue> list;

            if (_queues.ContainsKey(token))
            {
                list = _queues[token];
            }
            else
            {
                list = new List<PrivacyQueue>();
                _queues[token] = list;
            }

            PrivacyQueue queue;

            if (list.Count > 0)
            {
                var last = list[list.Count - 1];
                if (last.addresses.Count < last.size)
                {
                    return last;
                }
            }

            queue = new PrivacyQueue() { addresses = new List<Address>(), signatures = new List<RingSignature>(), ID = (uint)(list.Count + 1), size = 3 };
            list.Add(queue);
            return queue;
        }

        public uint PutPrivate(Address from, string symbol)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            var balance = balances.Get(from);
            Runtime.Expect(balance >= TransferAmount, "not enough balance");

            var queue = FetchQueue(token);
            Runtime.Expect(queue.addresses.Count < queue.size, "queue full");

            foreach (var address in queue.addresses)
            {
                Runtime.Expect(address != from, "address already in queue");
            }

            balances.Subtract(from, TransferAmount);
            queue.addresses.Add(from);

            return queue.ID; // TODO should be quueue ID
        }

        public void TakePrivate(Address to, string symbol, uint queueID, RingSignature signature)
        {
            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var queue = FindQueue(token, queueID);
            Runtime.Expect(queue.ID > 0, "invalid queue");

            Runtime.Expect(queue.ID == queueID, "mismatching queue");
            Runtime.Expect(queue.addresses.Count == queue.size, "queue not full yet");

            foreach (var address in queue.addresses)
            {
                Runtime.Expect(address != to, "cant send to anyone already in the queue");
            }

            var msg = this.Runtime.Transaction.ToByteArray(false);
            Runtime.Expect(signature.Verify(msg, queue.addresses), "ring signature failed");

            foreach (var otherSignature in queue.signatures)
            {
                Runtime.Expect(!signature.IsLinked(otherSignature), "ring signature already linked");
            }

            queue.signatures.Add(signature);

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            balances.Add(to, TransferAmount);
        }
    }
}
