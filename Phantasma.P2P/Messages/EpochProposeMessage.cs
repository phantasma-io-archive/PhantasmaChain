using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Consensus;
using Phantasma.Cryptography;

namespace Phantasma.Network.P2P.Messages
{
    internal class EpochProposeMessage : Message
    {
        public readonly uint Index;
        public IEnumerable<Transaction> Transactions => _transactions;
        public IEnumerable<Block> Blocks  => _blocks;

        public readonly Epoch Epoch;

        private Block[] _blocks;
        private Transaction[] _transactions;

        public EpochProposeMessage(Nexus nexus, Address address, Epoch epoch, IEnumerable<Block> blocks, IEnumerable<Transaction> transactions) : base(nexus, Opcode.EPOCH_Propose, address)
        {
            this.Epoch = epoch;
            this._transactions = transactions.ToArray();
            this._blocks = blocks.ToArray();
        }

        internal static Message FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            /*
            var shardID = reader.ReadUInt32();
            var txCount = reader.ReadUInt16();
            var txs = new Transaction[txCount];
            for (int i=0; i<txCount; i++)
            {
                var tx = Transaction.Unserialize(reader);
            }

            return new EpochProposeMessage(nexus, address, shardID, txs);
            */
            throw new System.NotImplementedException();
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }

    }
}