using Phantasma.Blockchain;
using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal class ChainListMessage : Message
    {
        public ChainListMessage(Nexus nexus, Address address) : base(nexus, Opcode.CHAIN_List, address)
        {
        }

        internal static ChainListMessage FromReader(Nexus nexus, Address pubKey, BinaryReader reader)
        {
            throw new System.NotImplementedException();
//            return new ChainValuesMessage(code, text);
        }

    }
}