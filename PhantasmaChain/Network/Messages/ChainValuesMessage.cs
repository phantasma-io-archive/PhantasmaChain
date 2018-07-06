using System.IO;

namespace Phantasma.Network
{
    internal class ChainValuesMessage : Message
    {
        public ChainValuesMessage()
        {
        }

        internal static ChainValuesMessage FromReader(BinaryReader reader)
        {
            throw new System.NotImplementedException();
//            return new ChainValuesMessage(code, text);
        }

    }
}