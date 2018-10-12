using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Cryptography.Ring;
using Phantasma.Numerics;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Contracts.Native
{
    internal class PrivacyQueue
    {
        public uint ID;
        public int size;
        public List<Address> addresses;
        public List<RingSignature> signatures;
    }

    public sealed class PrivacyContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Privacy;

        public static readonly BigInteger TransferAmount = 10;

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

            return null;
           
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

            if (list.Count == 0)
            {
                queue = null;
            }
            else
            {
                var last = list[list.Count - 1];
                if (last.addresses.Count >= last.size)
                {
                    queue = null;
                }
                else
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
            Expect(IsWitness(from));

            var token = this.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Chain.GetTokenBalances(token);
            var balance = balances.Get(from);
            Expect(balance >= TransferAmount);

            var queue = FetchQueue(token);
            Expect(queue.addresses.Count < queue.size);

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
            var token = this.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var queue = FindQueue(token, queueID);
            Expect(queue != null);

            Expect(queue.ID == queueID);
            Expect(queue.addresses.Count == queue.size);

            // cant send to anyone already part of this queue
            foreach (var address in queue.addresses)
            {
                Expect(address != to);
            }

            var msg = this.Transaction.ToByteArray(false);
            Expect(signature.Verify(msg, queue.addresses));

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
