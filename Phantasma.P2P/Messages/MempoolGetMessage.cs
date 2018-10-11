using Phantasma.Blockchain;
using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal class MempoolGetMessage : Message
    {
        public MempoolGetMessage(Nexus nexus, Address address) :base(nexus, Opcode.MEMPOOL_Get, address)
        {
        }

        internal static MempoolGetMessage FromReader(Nexus nexus, Address pubKey, BinaryReader reader)
        {
            return new MempoolGetMessage(nexus, pubKey);
        }
    }
}