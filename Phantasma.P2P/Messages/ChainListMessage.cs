using System.IO;
using Phantasma.Blockchain;
using Phantasma.Cryptography;

namespace Phantasma.Network.P2P.Messages
{
    internal sealed class ChainListMessage : Message
    {
        public ChainListMessage(Nexus nexus, Address address) :base(nexus, Opcode.CHAIN_List, address)
        {
        }

        internal static ChainListMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            return new ChainListMessage(nexus, address);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }

    }
}