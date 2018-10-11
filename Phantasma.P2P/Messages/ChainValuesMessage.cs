using Phantasma.Blockchain;
using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal class ChainValuesMessage : Message
    {
        public ChainValuesMessage(Nexus nexus, Address address) : base(nexus, Opcode.CHAIN_Values, address)
        {
        }

        internal static ChainValuesMessage FromReader(Nexus nexus, Address pubKey, BinaryReader reader)
        {
            throw new System.NotImplementedException();
//            return new ChainValuesMessage(code, text);
        }

    }
}