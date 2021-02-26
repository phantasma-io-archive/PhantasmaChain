using System.IO;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.Network.P2P.Messages
{
    public sealed class ErrorMessage : Message
    {
        public readonly P2PError Code;
        public readonly string Text;

        public ErrorMessage(Address address, string host, P2PError code, string text = null) : base(Opcode.ERROR, address, host)
        {
            this.Code = code;
            this.Text = text;
        }

        internal static ErrorMessage FromReader(Address address, string host, BinaryReader reader)
        {
            var code = (P2PError)reader.ReadByte();
            var text = reader.ReadVarString();
            return new ErrorMessage(address, host, code, text);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            writer.Write((byte)Code);
            writer.WriteVarString(Text);
        }
    }
}