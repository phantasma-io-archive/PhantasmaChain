using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using Phantasma.Blockchain.Contracts;
using Phantasma.Storage;
using Phantasma.Core;
using Phantasma.Storage.Utils;

namespace Phantasma.Blockchain
{
    public sealed class Block : ISerializable
    {
        public static readonly BigInteger InitialDifficulty = 127;
        public static readonly float IdealBlockTime = 5;
        public static readonly float BlockTimeFlutuation = 0.2f;

        public Address ChainAddress { get; private set; }

        public uint Height { get; private set; }
        public Timestamp Timestamp { get; private set; }
        public Hash PreviousHash { get; private set; }

        private bool _dirty;
        private Hash _hash;
        public Hash Hash
        {
            get
            {
                if (_dirty)
                {
                    UpdateHash();
                }

                return _hash;
            }
        }

        public byte[] Payload { get; private set; }

        private List<Hash> _transactionHashes;
        public IEnumerable<Hash> TransactionHashes => _transactionHashes;
        public int TransactionCount => _transactionHashes.Count;

        // stores the events for each included transaction
        private Dictionary<Hash, List<Event>> _eventMap = new Dictionary<Hash, List<Event>>();

        // stores the results of invocations
        private Dictionary<Hash, byte[]> _resultMap = new Dictionary<Hash, byte[]>();

        // stores the results of oracles
        private List<OracleEntry> _oracleData = new List<OracleEntry>();

        // required for unserialization
        public Block()
        {

        }

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

            this.Payload = new byte[0];
            this._dirty = true;
        }

        public void Notify(Hash hash, Event evt)
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
            _dirty = true;
        }

        // TODO - Optimize this to avoid recalculating the arrays if only the nonce changed
        internal void UpdateHash(byte[] payload)
        {
            this.Payload = payload;
            UpdateHash();
        }

        internal void UpdateHash()
        {
            var data = ToByteArray();
            var hashBytes = CryptoExtensions.SHA256(data);
            _hash = new Hash(hashBytes);
            _dirty = false;
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

        internal void Serialize(BinaryWriter writer)
        {
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
                int resultLen = _resultMap.ContainsKey(hash) ? _resultMap[hash].Length : -1;
                writer.Write((short)resultLen);
                if (resultLen > 0)
                {
                    var result = _resultMap[hash];
                    writer.WriteByteArray(result);
                }
            }

            writer.Write((ushort)_oracleData.Count);
            foreach (var entry in _oracleData)
            {
                writer.WriteVarString(entry.URL);
                writer.WriteByteArray(entry.Content);
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

        public static Block Unserialize(BinaryReader reader)
        {
            var block = new Block();
            block.UnserializeData(reader);
            return block;
        }

        internal void SetResultForHash(Hash hash, byte[] result)
        {
            Throw.IfNull(result, nameof(result));
            Throw.If(result.Length > 32 * 1024, "transaction result is too large");
            _resultMap[hash] = result;
        }

        public void SerializeData(BinaryWriter writer)
        {
            Serialize(writer);
        }

        public void UnserializeData(BinaryReader reader)
        {
            this.Height = reader.ReadUInt32();
            this.Timestamp = new Timestamp(reader.ReadUInt32());
            this.PreviousHash = reader.ReadHash();
            this.ChainAddress = reader.ReadAddress();

            var hashCount = reader.ReadUInt16();
            var hashes = new List<Hash>();

            _eventMap.Clear();
            _resultMap.Clear();
            for (int j = 0; j < hashCount; j++)
            {
                var hash = reader.ReadHash();
                hashes.Add(hash);

                var evtCount = reader.ReadUInt16();
                var evts = new List<Event>(evtCount);
                for (int i = 0; i < evtCount; i++)
                {
                    evts.Add(Event.Unserialize(reader));
                }

                _eventMap[hash] = evts;

                var resultLen = reader.ReadInt16();
                if (resultLen >= 0)
                {
                    if (resultLen == 0)
                    {
                        _resultMap[hash] = new byte[0];
                    }
                    else
                    {
                        _resultMap[hash] = reader.ReadByteArray();
                    }
                }
            }

            var oracleCount = reader.ReadUInt16();
            _oracleData.Clear();
            while (oracleCount > 0)
            {
                var key = reader.ReadString();
                var val = reader.ReadByteArray();
                _oracleData.Add(new OracleEntry( key, val));
            }

            this.Payload = reader.ReadByteArray();

            _transactionHashes = new List<Hash>();
            foreach (var hash in hashes)
            {
                _transactionHashes.Add(hash);
            }

            _dirty = true;
        }
        #endregion
    }
}
