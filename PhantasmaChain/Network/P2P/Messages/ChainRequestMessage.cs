using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal class ChainRequestMessage : Message
    {
        public readonly uint Flags;

        public ChainRequestMessage(Address address, uint flags) :base (Opcode.CHAIN_Request, address)
        {
            this.Flags = flags;
        }

        internal static ChainRequestMessage FromReader(Address address, BinaryReader reader)
        {
            var flags = reader.ReadUInt32();
            return new ChainRequestMessage(address, flags);
        }
    }
}