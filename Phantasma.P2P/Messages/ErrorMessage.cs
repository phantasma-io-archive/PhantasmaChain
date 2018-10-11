using System.IO;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal sealed class ErrorMessage : Message
    {
        public readonly ushort Code;
        public readonly string Text;

        public ErrorMessage(Nexus nexus, Address address, ushort code, string text = null) :base(nexus, Opcode.ERROR, address)
        {
            this.Code = code;
            this.Text = text;
        }

        internal static ErrorMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            var code = reader.ReadUInt16();
            var text = reader.ReadShortString();
            return new ErrorMessage(nexus, address, code, text);
        }
    }
}