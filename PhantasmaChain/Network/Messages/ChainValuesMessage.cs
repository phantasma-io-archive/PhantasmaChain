using Phantasma.VM.Types;
using System.IO;

namespace Phantasma.Network.Messages
{
    internal class ChainValuesMessage : Message
    {
        public ChainValuesMessage(Address address) : base(Opcode.CHAIN_Values, address)
        {
        }

        internal static ChainValuesMessage FromReader(Address pubKey, BinaryReader reader)
        {
            throw new System.NotImplementedException();
//            return new ChainValuesMessage(code, text);
        }

    }
}