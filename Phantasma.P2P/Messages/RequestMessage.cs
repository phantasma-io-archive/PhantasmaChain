using System;
using System.IO;
using Phantasma.Cryptography;

namespace Phantasma.Network.P2P.Messages
{
    [Flags]
    public enum RequestKind
    {
        None = 0,
        Peers = 0x1,
        Chains = 0x2,
        Mempool = 0x4,
    }

    public sealed class RequestMessage : Message
    {
        public readonly RequestKind Kind;

        public RequestMessage(RequestKind kind, Address address) :base(Opcode.REQUEST, address)
        {
            Kind = kind;
        }

        internal static RequestMessage FromReader(Address address, BinaryReader reader)
        {
            var kind = (RequestKind)reader.ReadByte();
            return new RequestMessage(kind, address);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            writer.Write((byte)Kind);
        }

    }
}