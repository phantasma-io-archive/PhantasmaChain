using Phantasma.Blockchain;
using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal class MempoolGetMessage : Message
    {
        public MempoolGetMessage(Address address) : base(Opcode.MEMPOOL_List, address)
        {
        }

        internal static MempoolGetMessage FromReader(Address pubKey, BinaryReader reader)
        {
            return new MempoolGetMessage(pubKey);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }
    }
}