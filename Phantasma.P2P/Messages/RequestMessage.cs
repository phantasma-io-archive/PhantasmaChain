using System;
using System.Collections.Generic;
using System.IO;
using Phantasma.Core;
using Phantasma.IO;
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
        Blocks = 0x8,
    }

    public sealed class RequestMessage : Message
    {
        public readonly RequestKind Kind;

        private Dictionary<string, uint> _blockFetches;

        public RequestMessage(RequestKind kind, Address address) :base(Opcode.REQUEST, address)
        {
            Kind = kind;
        }

        public void SetBlocks(Dictionary<string, uint> blockFetches)
        {
            this._blockFetches = blockFetches;
        }

        internal static RequestMessage FromReader(Address address, BinaryReader reader)
        {
            var kind = (RequestKind)reader.ReadByte();
            var msg = new RequestMessage(kind, address);

            if (kind.HasFlag(RequestKind.Blocks))
            {
                var count = reader.ReadVarInt();
                var fetches = new Dictionary<string, uint>();
                while (count > 0)
                {
                    var key = reader.ReadVarString();
                    var height = reader.ReadUInt32();
                    fetches[key] = height;
                }

                msg.SetBlocks(fetches);
            }

            return msg;
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            writer.Write((byte)Kind);

            if (Kind.HasFlag(RequestKind.Blocks))
            {
                Throw.IfNull(_blockFetches, nameof(_blockFetches));
                Throw.If(_blockFetches.Count > 255, "max chain block fetches per request reached");
                writer.WriteVarInt(_blockFetches.Count);
                foreach (var entry in _blockFetches)
                {
                    writer.WriteVarString(entry.Key);
                    writer.Write((uint)entry.Value);
                }
            }
        }

    }
}