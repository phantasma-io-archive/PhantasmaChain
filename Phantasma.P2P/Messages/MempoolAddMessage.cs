using System.IO;
using Phantasma.Blockchain;
using Phantasma.Cryptography;

namespace Phantasma.Network.P2P.Messages
{
    internal class MempoolAddMessage : Message
    {
        public readonly Transaction transaction;

        public MempoolAddMessage(Nexus nexus, Address pubKey, Transaction tx) : base(nexus, Opcode.MEMPOOL_Add, pubKey)
        {
            this.transaction = tx;
        }

        internal static MempoolAddMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            var tx = Transaction.Unserialize(reader);
            return new MempoolAddMessage(nexus, address, tx);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }
    }
}