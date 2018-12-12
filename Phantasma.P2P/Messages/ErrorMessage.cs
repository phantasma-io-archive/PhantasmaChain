using System.IO;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.IO;

namespace Phantasma.Network.P2P.Messages
{
    public sealed class ErrorMessage : Message
    {
        public readonly P2PError Code;
        public readonly string Text;

        public ErrorMessage(Address address, P2PError code, string text = null) : base(Opcode.ERROR, address)
        {
            this.Code = code;
            this.Text = text;
        }

        internal static ErrorMessage FromReader(Address address, BinaryReader reader)
        {
            var code = (P2PError)reader.ReadByte();
            var text = reader.ReadVarString();
            return new ErrorMessage(address, code, text);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            writer.Write((byte)Code);
            writer.WriteVarString(Text);
        }
    }
}