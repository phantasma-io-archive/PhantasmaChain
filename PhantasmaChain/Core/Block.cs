using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Phantasma.Utils;

namespace Phantasma.Core
{
    public sealed class Block
    {
        public static readonly BigInteger InitialDifficulty = 127;
        public static readonly float IdealBlockTime = 5;
        public static readonly float BlockTimeFlutuation = 0.2f;

        public readonly uint Height;
        public readonly byte[] PreviousHash;
        public readonly uint Timestamp;
        public readonly byte[] MinerPublicKey;
        public readonly byte[] TokenPublicKey;
        public uint Nonce { get; private set; }
        public byte[] Hash { get; private set; }
        public readonly BigInteger difficulty;

        private List<Transaction> _transactions;
        public IEnumerable<Transaction> Transactions => _transactions;

        public List<Event> Events = new List<Event>();

        public Block(uint timestamp, byte[] minerPublicKey, byte[] tokenPublicKey, IEnumerable<Transaction> transactions, Block previous = null)
        {
            this.Height = previous != null ? previous.Height + 1 : 0;
            this.PreviousHash = previous != null ? previous.Hash : null;
            this.Timestamp = timestamp;
            this.MinerPublicKey = minerPublicKey;
            this.TokenPublicKey = tokenPublicKey;

            _transactions = new List<Transaction>();
            foreach (var tx in transactions)
            {
                _transactions.Add(tx);
            }

            if (previous != null)
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
            }

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

        internal void Notify(Event evt)
        {
            this.Events.Add(evt);
        }

        // TODO - Optimize this to avoid recalculating the arrays if only the nonce changed
        internal void UpdateHash(uint nonce)
        {
            this.Nonce = nonce;
            var data = ToArray();
            this.Hash = CryptoUtils.Sha256(data);
        }

        #region SERIALIZATION

        internal void Serialize(BinaryWriter writer) {
            writer.Write(Height);
            writer.Write(Timestamp);
            writer.WriteByteArray(PreviousHash);
            writer.WriteByteArray(MinerPublicKey);
            writer.Write((ushort)Events.Count);
            foreach (var evt in Events)
            {
                evt.Serialize(writer);
            }
            writer.Write(Nonce);
        }

        internal static Block Unserialize(BinaryReader reader) {
            var height = reader.ReadUInt32();
            var timestamp = reader.ReadUInt32();
            var prevHash = reader.ReadByteArray();
            var minerPubKey = reader.ReadByteArray();
            var tokenPublicKey = reader.ReadByteArray();

            var evtCount = reader.ReadUInt16();
            var evts = new Event[evtCount];
            for (int i=0;i<evtCount; i++)
            {
                evts[i] = Event.Unserialize(reader); 
            }
            var nonce = reader.ReadUInt32();
            return new Block(timestamp, minerPubKey, tokenPublicKey, null);
        }
        #endregion
    }
}
