using System.IO;

namespace Phantasma.Network.Messages
{
    internal class ChainRequestMessage : Message
    {
        public readonly uint Flags;

        public ChainRequestMessage(byte[] pubKey, uint flags) :base (Opcode.CHAIN_Request, pubKey)
        {
            this.Flags = flags;
        }

        internal static ChainRequestMessage FromReader(byte[] pubKey, BinaryReader reader)
        {
            var flags = reader.ReadUInt32();
            return new ChainRequestMessage(pubKey, flags);
        }
    }
}