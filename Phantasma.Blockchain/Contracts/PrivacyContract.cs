using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts
{
    internal struct PrivacyQueue
    {
        public uint ID;
        public int size;
        public StorageList addresses; //<Address>
        public StorageList signatures; //<RingSignature>
    }

    public sealed class PrivacyContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Privacy;

        /*
        public static readonly BigInteger TransferAmount = 10;

        internal StorageMap _queues; // = new Dictionary<string, List<PrivacyQueue>>();

        public PrivacyContract() : base()
        {
        }

        private PrivacyQueue FindQueue(string tokenSymbol, uint ID)
        {
            if (_queues.ContainsKey(tokenSymbol))
            {
                var list = _queues.Get<string, StorageList>(tokenSymbol);

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

        private PrivacyQueue FetchQueue(string symbol)
        {
            StorageList list = _queues.Get<string, StorageList>(symbol);

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
            var baseKey = $"{symbol}.{id}";

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
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(this.Runtime.Nexus.TokenExists(symbol), "invalid token");

            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var balances = new BalanceSheet(symbol);
            var balance = balances.Get(this.Storage, from);
            Runtime.Expect(balance >= TransferAmount, "not enough balance");

            var queue = FetchQueue(symbol);
            Runtime.Expect(queue.addresses.Count() < queue.size, "queue full");

            var addresses = queue.addresses.All<Address>();
            foreach (var address in addresses)
            {
                Runtime.Expect(address != from, "address already in queue");
            }

            // TODO it is wrong to do this in current codebase...
            balances.Subtract(this.Storage, from, TransferAmount);
            queue.addresses.Add(from);

            return queue.ID; // TODO should be quueue ID
        }

        public void TakePrivate(Address to, string symbol, uint queueID, RingSignature signature)
        {
            Runtime.Expect(Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var queue = FindQueue(symbol, queueID);
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

            // TODO this is wrong
            var balances = new BalanceSheet(symbol);
            balances.Add(this.Storage, to, TransferAmount);
        }*/
    }
}
