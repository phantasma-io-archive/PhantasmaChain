using Phantasma.Blockchain.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Cryptography.Ring;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Contracts.Native
{
    internal struct PrivacyQueue
    {
        public uint ID;
        public int size;
        public StorageList addresses; //<Address>
        public StorageList signatures; //<RingSignature>
    }

    public sealed class PrivacyContract : SmartContract
    {
        public override string Name => "privacy";

        public static readonly BigInteger TransferAmount = 10;

        internal StorageMap _queues; // = new Dictionary<Token, List<PrivacyQueue>>();

        public PrivacyContract() : base()
        {
        }

        private PrivacyQueue FindQueue(Token token, uint ID)
        {
            if (_queues.ContainsKey(token))
            {
                var list = _queues.Get<string, StorageList>(token.Symbol);
                
                var queues = list.All<PrivacyQueue>();
                foreach (var entry in queues)
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
            StorageList list = _queues.Get<string, StorageList>(token.Symbol);

            PrivacyQueue queue;

            var count = list.Count();
            if (count > 0)
            {
                var last = list.Get<PrivacyQueue>(count - 1);
                if (last.addresses.Count() < last.size)
                {
                    return last;
                }
            }

            var id = (uint)(count + 1);
            var baseKey = $"{token.Symbol}.{id}";

            var addrKey = $"{baseKey}.addr".AsByteArray();
            var addressList = new StorageList(addrKey, Runtime.ChangeSet);
            addressList.Clear();

            var ringKey = $"{baseKey}.ring".AsByteArray();
            var ringList = new StorageList(addrKey, Runtime.ChangeSet);
            ringList.Clear();

            queue = new PrivacyQueue() { addresses = addressList, signatures = ringList, ID = id, size = 3 };
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
            Runtime.Expect(queue.addresses.Count() < queue.size, "queue full");

            var addresses = queue.addresses.All<Address>();
            foreach (var address in addresses)
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
            Runtime.Expect(queue.addresses.Count() == queue.size, "queue not full yet");

            var addresses = queue.addresses.All<Address>();
            foreach (var address in addresses)
            {
                Runtime.Expect(address != to, "cant send to anyone already in the queue");
            }

            var msg = this.Runtime.Transaction.ToByteArray(false);
            Runtime.Expect(signature.Verify(msg, addresses), "ring signature failed");

            var signatures = queue.signatures.All<Signature>();
            foreach (RingSignature otherSignature in signatures)
            {
                Runtime.Expect(!signature.IsLinked(otherSignature), "ring signature already linked");
            }

            queue.signatures.Add(signature);

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            balances.Add(to, TransferAmount);
        }
    }
}
