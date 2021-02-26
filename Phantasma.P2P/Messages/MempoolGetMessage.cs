using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal class MempoolGetMessage : Message
    {
        public MempoolGetMessage(Address address, string host) : base(Opcode.MEMPOOL_Add, address, host)
        {
        }

        internal static MempoolGetMessage FromReader(Address pubKey, string host, BinaryReader reader)
        {
            return new MempoolGetMessage(pubKey, host);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }
    }
}