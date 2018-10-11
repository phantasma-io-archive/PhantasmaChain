using Phantasma.Blockchain;
using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal class ChainRequestMessage : Message
    {
        public readonly uint Flags;

        public ChainRequestMessage(Nexus nexus, Address address, uint flags) :base (nexus, Opcode.CHAIN_Request, address)
        {
            this.Flags = flags;
        }

        internal static ChainRequestMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            var flags = reader.ReadUInt32();
            return new ChainRequestMessage(nexus, address, flags);
        }
    }
}