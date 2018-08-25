using System.IO;
using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;

namespace Phantasma.Network.Messages
{
    internal sealed class ErrorMessage : Message
    {
        public readonly ushort Code;
        public readonly string Text;

        public ErrorMessage(Address address, ushort code, string text = null) :base(Opcode.ERROR, address)
        {
            this.Code = code;
            this.Text = text;
        }

        internal static ErrorMessage FromReader(Address address, BinaryReader reader)
        {
            var code = reader.ReadUInt16();
            var text = reader.ReadShortString();
            return new ErrorMessage(address, code, text);
        }
    }
}