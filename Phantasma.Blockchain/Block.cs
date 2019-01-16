using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using Phantasma.Blockchain.Contracts;
using Phantasma.IO;
using System;
using Phantasma.Core;

namespace Phantasma.Blockchain
{
    public sealed class Block
    {
        public static readonly BigInteger InitialDifficulty = 127;
        public static readonly float IdealBlockTime = 5;
        public static readonly float BlockTimeFlutuation = 0.2f;

        public Address ChainAddress { get; private set; }

        public uint Height { get; private set; }
        public Timestamp Timestamp { get; private set; }
        public Hash Hash { get; private set; }
        public Hash PreviousHash { get; private set; }

        public byte[] Payload { get; private set; }

        private List<Hash> _transactionHashes;
        public IEnumerable<Hash> TransactionHashes => _transactionHashes;
        public int TransactionCount => _transactionHashes.Count;

        // stores the events for each included transaction
        private Dictionary<Hash, List<Event>> _eventMap = new Dictionary<Hash, List<Event>>();

        // stores the results of invocations
        private Dictionary<Hash, byte[]> _resultMap = new Dictionary<Hash, byte[]>();

        /// <summary>
        /// Note: When creating the genesis block of a new side chain, the previous block would be the block that contained the CreateChain call
        /// </summary>
        public Block(uint height, Address chainAddress, Timestamp timestamp, IEnumerable<Hash> hashes, Hash previousHash, byte[] data = null)
        {
            this.ChainAddress = chainAddress;
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

            this.UpdateHash(new byte[0]);
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
        internal void UpdateHash(byte[] payload)
        {
            this.Payload = payload;
            var data = ToByteArray();
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

        public byte[] GetResultForTransaction(Hash hash)
        {
            if (_resultMap.ContainsKey(hash))
            {
                return _resultMap[hash];
            }

            return null;
        }

        #region SERIALIZATION

        public byte[] ToByteArray()
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

        internal void Serialize(BinaryWriter writer) {
            writer.Write((uint)Height);
            writer.Write(Timestamp.Value);
            writer.WriteHash(PreviousHash);
            writer.WriteAddress(ChainAddress);

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
                int resultLen = _resultMap.ContainsKey(hash) ? _resultMap[hash].Length: -1;
                writer.Write((short)resultLen);
                if (resultLen > 0)
                {
                    var result = _resultMap[hash];
                    writer.WriteByteArray(result);
                }
            }
            writer.WriteByteArray(Payload);
        }

        public static Block Unserialize(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    return Unserialize(reader);
                }
            }
        }

        public static Block Unserialize(BinaryReader reader) {
            var height = reader.ReadUInt32();
            var timestamp = new Timestamp(reader.ReadUInt32());
            var prevHash = reader.ReadHash();
            var chainAddress = reader.ReadAddress();

            var hashCount = reader.ReadUInt16();
            var hashes = new List<Hash>();

            var eventMap = new Dictionary<Hash, List<Event>>();
            var resultMap = new Dictionary<Hash, byte[]>();
            for (int j=0; j<hashCount; j++)
            {
                var hash = reader.ReadHash();
                hashes.Add(hash);

                var evtCount = reader.ReadUInt16();
                var evts = new List<Event>(evtCount);
                for (int i = 0; i < evtCount; i++)
                {
                    evts.Add(Event.Unserialize(reader));
                }

                eventMap[hash] = evts;

                var resultLen = reader.ReadInt16();
                if (resultLen >= 0)
                {
                    if (resultLen == 0)
                    {
                        resultMap[hash] = new byte[0];
                    }
                    else
                    {
                        resultMap[hash] = reader.ReadByteArray();
                    }
                }
            }

            var payLoad = reader.ReadByteArray();

            var block = new Block(height, chainAddress, timestamp, hashes, prevHash, payLoad);
            block._eventMap = eventMap;
            block._resultMap = resultMap;
            return block;
        }

        internal void SetResultForHash(Hash hash, byte[] result)
        {
            Throw.IfNull(result, nameof(result));
            Throw.If(result.Length > 32 * 1024, "transaction result is too large");
            _resultMap[hash] = result;
        }
        #endregion
    }
}
