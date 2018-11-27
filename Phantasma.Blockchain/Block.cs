using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using Phantasma.Blockchain.Contracts;
using Phantasma.IO;

namespace Phantasma.Blockchain
{
    public sealed class Block
    {
        public static readonly BigInteger InitialDifficulty = 127;
        public static readonly float IdealBlockTime = 5;
        public static readonly float BlockTimeFlutuation = 0.2f;

        public Address ChainAddress { get; private set; }
        public Address MinerAddress { get; private set; }

        public uint Height { get; private set; }
        public Timestamp Timestamp { get; private set; }
        public uint Nonce { get; private set; }
        public Hash Hash { get; private set; }
        public Hash PreviousHash { get; private set; }

        public byte[] Payload { get; private set; }

        private List<Hash> _transactionHashes;
        public IEnumerable<Hash> TransactionHashes => _transactionHashes;

        // stores the events for each included transaction
        private Dictionary<Hash, List<Event>> _eventMap = new Dictionary<Hash, List<Event>>();
        
        /// <summary>
        /// Note: When creating the genesis block of a new side chain, the previous block would be the block that contained the CreateChain call
        /// </summary>
        public Block(uint height, Address chainAddress, Address minerAddress, Timestamp timestamp, IEnumerable<Hash> hashes, Hash previousHash, byte[] data = null)
        {
            this.ChainAddress = chainAddress;
            this.MinerAddress = minerAddress;
            this.Timestamp = timestamp;
            this.Payload = data;

            //this.Height = previous != null && previous.Chain == chain ? previous.Height + 1 : 0;
            //this.PreviousHash = previous != null ? previous.Hash : null;

            this.Height = height;
            this.PreviousHash = previousHash;

            _transactionHashes = new List<Hash>();
            foreach (var hash in hashes)
            {
                _transactionHashes.Add(hash);
            }

            /*if (previous != null)
            {
                var delta = this.Timestamp - previous.Timestamp;

                if (delta < IdealBlockTime * (1.0f - BlockTimeFlutuation))
                {
                    this.difficulty = previous.difficulty - 1;
                }
                else
                if (delta > IdealBlockTime * (1.0f + BlockTimeFlutuation))
                {
                    this.difficulty = previous.difficulty - 1;
                }
                else {
                    this.difficulty = previous.difficulty;
                }
            }
            else
            {
                this.difficulty = InitialDifficulty;
            }*/

            this.UpdateHash(0);
        }

        private byte[] ToArray()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    Serialize(writer);
                }

                return stream.ToArray();
            }
        }

        internal void Notify(Hash hash, Event evt)
        {
            List<Event> list;

            if (_eventMap.ContainsKey(hash))
            {
                list = _eventMap[hash];
            }
            else
            {
                list = new List<Event>();
                _eventMap[hash] = list;
            }

            list.Add(evt);
        }

        // TODO - Optimize this to avoid recalculating the arrays if only the nonce changed
        internal void UpdateHash(uint nonce)
        {
            this.Nonce = nonce;
            var data = ToArray();
            var hashBytes = CryptoExtensions.Sha256(data);
            this.Hash = new Hash(hashBytes);
        }

        public IEnumerable<Event> GetEventsForTransaction(Hash hash)
        {
            if (_eventMap.ContainsKey(hash))
            {
                return _eventMap[hash];
            }

            return Enumerable.Empty<Event>();
        }

        #region SERIALIZATION

        internal void Serialize(BinaryWriter writer) {
            writer.Write((uint)Height);
            writer.Write(Timestamp.Value);
            writer.WriteHash(PreviousHash);
            writer.WriteAddress(MinerAddress);
            writer.WriteAddress(ChainAddress);
            writer.WriteByteArray(Payload);

            writer.Write((ushort)_transactionHashes.Count);
            foreach (var hash in _transactionHashes)
            {
                writer.WriteHash(hash);
                var evts = GetEventsForTransaction(hash).ToArray();
                writer.Write((ushort)evts.Length);
                foreach (var evt in evts)
                {
                    evt.Serialize(writer);
                }
            }
            writer.Write(Nonce);
        }

        public static Block Unserialize(BinaryReader reader) {
            var height = reader.ReadUInt32();
            var timestamp = new Timestamp(reader.ReadUInt32());
            var prevHash = reader.ReadHash();
            var minerAddress =  reader.ReadAddress();
            var chainAddress = reader.ReadAddress();
            var extraContent = reader.ReadByteArray();

            var hashCount = reader.ReadUInt16();
            var hashes = new List<Hash>();

            var eventMap = new Dictionary<Hash, Event[]>();
            for (int j=0; j<hashCount; j++)
            {
                var hash = reader.ReadHash();
                hashes.Add(hash);

                var evtCount = reader.ReadUInt16();
                var evts = new Event[evtCount];
                for (int i = 0; i < evtCount; i++)
                {
                    evts[i] = Event.Unserialize(reader);
                }

                eventMap[hash] = evts;
            }

            var nonce = reader.ReadUInt32();

            var block = new Block(height, chainAddress, minerAddress, timestamp, hashes, prevHash, extraContent); 
            return block;
        }
        #endregion
    }
}
