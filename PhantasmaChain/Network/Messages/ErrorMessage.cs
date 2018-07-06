using System;
using System.IO;
using Phantasma.Utils;

namespace Phantasma.Network
{
    internal sealed class ErrorMessage : Message
    {
        public readonly ushort Code;
        public readonly string Text;

        public ErrorMessage(ushort code, string text = null)
        {
            this.Code = code;
            this.Text = text;
        }

        internal static Message FromReader(BinaryReader reader)
        {
            var code = reader.ReadUInt16();
            var text = reader.ReadShortString();
            return new ErrorMessage(code, text);
        }
    }
}