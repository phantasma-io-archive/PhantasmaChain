using System.IO;
using System.Linq;
using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using Phantasma.Blockchain.Contracts;
using Phantasma.Storage;
using Phantasma.Core;
using Phantasma.Storage.Utils;
using Phantasma.Domain;

namespace Phantasma.Blockchain
{
    public sealed class Block : IBlock, ISerializable
    {
        public Address ChainAddress { get; private set; }

        public BigInteger Height { get; private set; }
        public Timestamp Timestamp { get; private set; }
        public Hash PreviousHash { get; private set; }
        public uint Protocol { get; private set; }

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

        private List<Hash> _transactionHashes;
        public Hash[] TransactionHashes => _transactionHashes.ToArray();
        public int TransactionCount => _transactionHashes.Count;

        // stores the events for each included transaction
        private Dictionary<Hash, List<Event>> _eventMap = new Dictionary<Hash, List<Event>>();

        // stores the results of invocations
        private Dictionary<Hash, byte[]> _resultMap = new Dictionary<Hash, byte[]>();

        // stores the results of oracles
        private List<OracleEntry> _oracleData = new List<OracleEntry>();
        public IOracleEntry[] OracleData => _oracleData.Select(x => (IOracleEntry)x).ToArray();

        // required for unserialization
        public Block()
        {

        }

        /// <summary>
        /// Note: When creating the genesis block of a new side chain, the previous block would be the block that contained the CreateChain call
        /// </summary>
        public Block(BigInteger height, Address chainAddress, Timestamp timestamp, IEnumerable<Hash> hashes, Hash previousHash, uint protocol)
        {
            this.ChainAddress = chainAddress;
            this.Timestamp = timestamp;
            this.Protocol = protocol;

            this.Height = height;
            this.PreviousHash = previousHash;

            _transactionHashes = new List<Hash>();
            foreach (var hash in hashes)
            {
                _transactionHashes.Add(hash);
            }

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

        internal void UpdateHash()
        {
            var data = ToByteArray();
            var hashBytes = CryptoExtensions.SHA256(data);
            _hash = new Hash(hashBytes);
            _dirty = false;
        }

        public Event[] GetEventsForTransaction(Hash hash)
        {
            if (_eventMap.ContainsKey(hash))
            {
                return _eventMap[hash].ToArray();
            }

            return new Event[0];
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
            using (var stream = new MemoryStream())
            {
                using (var temp = new BinaryWriter(stream))
                {
                    temp.WriteBigInteger(Height);
                    temp.Write(Timestamp.Value);
                    temp.WriteHash(PreviousHash);
                    temp.WriteAddress(ChainAddress);
                    temp.WriteVarInt(Protocol);

                    temp.Write((ushort)_transactionHashes.Count);
                    foreach (var hash in _transactionHashes)
                    {
                        temp.WriteHash(hash);
                        var evts = GetEventsForTransaction(hash).ToArray();
                        temp.Write((ushort)evts.Length);
                        foreach (var evt in evts)
                        {
                            evt.Serialize(temp);
                        }
                        int resultLen = _resultMap.ContainsKey(hash) ? _resultMap[hash].Length : -1;
                        temp.Write((short)resultLen);
                        if (resultLen > 0)
                        {
                            var result = _resultMap[hash];
                            temp.WriteByteArray(result);
                        }
                    }

                    temp.Write((ushort)_oracleData.Count);
                    foreach (var entry in _oracleData)
                    {
                        temp.WriteVarString(entry.URL);
                        temp.WriteByteArray(entry.Content);
                    }
                }

                var bytes = stream.ToArray();
                var compressed = Compression.CompressGZip(bytes);
                writer.WriteVarInt(bytes.Length);
                writer.WriteByteArray(compressed);
            }
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
            var expectedLen = (int)reader.ReadVarInt();
            var bytes = reader.ReadByteArray();
            var decompressed = Compression.DecompressGZip(bytes);

            if (decompressed.Length > expectedLen)
            {
                decompressed = decompressed.Take(expectedLen).ToArray();
            }

            using (var stream = new MemoryStream(decompressed))
            {
                using (var temp = new BinaryReader(stream))
                {
                    this.Height = temp.ReadBigInteger();
                    this.Timestamp = new Timestamp(temp.ReadUInt32());
                    this.PreviousHash = temp.ReadHash();
                    this.ChainAddress = temp.ReadAddress();
                    this.Protocol = (uint)temp.ReadVarInt();

                    var hashCount = temp.ReadUInt16();
                    var hashes = new List<Hash>();

                    _eventMap.Clear();
                    _resultMap.Clear();
                    for (int j = 0; j < hashCount; j++)
                    {
                        var hash = temp.ReadHash();
                        hashes.Add(hash);

                        var evtCount = temp.ReadUInt16();
                        var evts = new List<Event>(evtCount);
                        for (int i = 0; i < evtCount; i++)
                        {
                            evts.Add(Event.Unserialize(temp));
                        }

                        _eventMap[hash] = evts;

                        var resultLen = temp.ReadInt16();
                        if (resultLen >= 0)
                        {
                            if (resultLen == 0)
                            {
                                _resultMap[hash] = new byte[0];
                            }
                            else
                            {
                                _resultMap[hash] = temp.ReadByteArray();
                            }
                        }
                    }

                    var oracleCount = temp.ReadUInt16();
                    _oracleData.Clear();
                    while (oracleCount > 0)
                    {
                        var key = temp.ReadVarString();
                        var val = temp.ReadByteArray();
                        _oracleData.Add(new OracleEntry(key, val));
                        oracleCount--;
                    }

                    _transactionHashes = new List<Hash>();
                    foreach (var hash in hashes)
                    {
                        _transactionHashes.Add(hash);
                    }
                }
            }
            _dirty = true;
        }

        internal void MergeOracle(OracleReader oracle)
        {
            if (oracle.Entries.Any())
            {
                _oracleData = oracle.Entries.ToList();
                _dirty = true;
            }
        }
        #endregion
    }
}
