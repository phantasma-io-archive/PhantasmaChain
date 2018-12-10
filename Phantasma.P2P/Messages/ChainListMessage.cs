using System.IO;
using Phantasma.Blockchain;
using Phantasma.Cryptography;

namespace Phantasma.Network.P2P.Messages
{
    internal sealed class ChainListMessage : Message
    {
        public ChainListMessage(Address address) :base(Opcode.CHAIN_List, address)
        {
        }

        internal static ChainListMessage FromReader(Address address, BinaryReader reader)
        {
            return new ChainListMessage(address);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }

    }
}