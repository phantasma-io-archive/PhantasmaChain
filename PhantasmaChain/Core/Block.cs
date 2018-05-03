using PhantasmaChain.Cryptography;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace PhantasmaChain.Core
{
    public class Block
    {
        public static readonly BigInteger MinimumDifficulty = 0;

        public readonly uint Height;
        public readonly byte[] PreviousHash;
        public readonly uint Timestamp;
        public readonly byte[] MinerPublicKey;
        public uint Nonce { get; private set; }
        public byte[] Hash { get; private set; }
        public readonly BigInteger difficulty;

        private List<Transaction> _transactions;
        public IEnumerable<Transaction> Transactions => _transactions;

        public List<Event> Events = new List<Event>();

        public Block(uint timestamp, byte[] minerPublicKey, IEnumerable<Transaction> transactions, Block previous = null)
        {
            this.Height = previous != null ? previous.Height + 1 : 0;
            this.PreviousHash = previous != null ? previous.Hash : null;
            this.Timestamp = timestamp;
            this.MinerPublicKey = minerPublicKey;

            _transactions = new List<Transaction>();
            foreach (var tx in transactions)
            {
                _transactions.Add(tx);
            }

            if (previous != null)
            {
                this.difficulty = previous.difficulty + previous.difficulty / 2048 * Math.Max(1 - (this.Timestamp - previous.Timestamp) / 10, -99) + (int)(Math.Pow(2, (this.Height / 100000) - 2));
            }
            else
            {
                this.difficulty = MinimumDifficulty;
            }

            this.UpdateHash(0);
        }

        private byte[] ToArray()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
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

                return stream.ToArray();
            }
        }

        internal void Notify(Event evt)
        {
            this.Events.Add(evt);
        }

        private void UpdateHash(uint nonce)
        {
            this.Nonce = nonce;
            var data = ToArray();
            this.Hash = CryptoUtils.Sha256(data);
        }
    }
}
