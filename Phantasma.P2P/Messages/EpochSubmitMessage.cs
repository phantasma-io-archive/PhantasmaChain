using System.Collections.Generic;
using System.IO;
using Phantasma.Blockchain;
using Phantasma.Cryptography;

namespace Phantasma.Network.P2P.Messages
{
    internal class EpochSubmitMessage : Message
    {
        public IEnumerable<KeyValuePair<Address, Signature>> Signatures{ get { return _signatures; } }

        private Dictionary<Address, Signature> _signatures;

        public EpochSubmitMessage(Address address, Epoch epoch, IEnumerable<Block> blocks, IEnumerable<Transaction> transactions) : base(Opcode.EPOCH_Submit, address)
        {
        }

        internal static EpochSubmitMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            throw new System.NotImplementedException();
            /*var shardID = reader.ReadUInt32();
            var txCount = reader.ReadUInt16();
            var txs = new Transaction[txCount];
            for (int i=0; i<txCount; i++)
            {
                var tx = Transaction.Unserialize(reader);
            }
            return new EpochSubmitMessage(nexus, address, shardID, txs);
            */
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }
    }
}