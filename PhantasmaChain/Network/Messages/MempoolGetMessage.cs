using Phantasma.VM.Types;
using System.IO;

namespace Phantasma.Network.Messages
{
    internal class MempoolGetMessage : Message
    {
        public MempoolGetMessage(Address address) :base(Opcode.MEMPOOL_Get, address)
        {
        }

        internal static MempoolGetMessage FromReader(Address pubKey, BinaryReader reader)
        {
            return new MempoolGetMessage(pubKey);
        }
    }
}