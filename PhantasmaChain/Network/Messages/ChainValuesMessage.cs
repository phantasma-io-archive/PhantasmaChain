using System.IO;

namespace Phantasma.Network.Messages
{
    internal class ChainValuesMessage : Message
    {
        public ChainValuesMessage(byte[] pubKey) : base(Opcode.CHAIN_Values, pubKey)
        {
        }

        internal static ChainValuesMessage FromReader(byte[] pubKey, BinaryReader reader)
        {
            throw new System.NotImplementedException();
//            return new ChainValuesMessage(code, text);
        }

    }
}