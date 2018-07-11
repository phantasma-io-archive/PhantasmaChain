using System;
using System.IO;
using Phantasma.Utils;

namespace Phantasma.Network
{
    internal sealed class ErrorMessage : Message
    {
        public readonly ushort Code;
        public readonly string Text;

        public ErrorMessage(byte[] pubKey, ushort code, string text = null) :base(Opcode.ERROR, pubKey)
        {
            this.Code = code;
            this.Text = text;
        }

        internal static ErrorMessage FromReader(byte[] pubKey, BinaryReader reader)
        {
            var code = reader.ReadUInt16();
            var text = reader.ReadShortString();
            return new ErrorMessage(pubKey, code, text);
        }
    }
}