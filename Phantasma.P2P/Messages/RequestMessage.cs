using System;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Storage.Utils;

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
        Transactions = 0x16,
    }

    public struct RequestRange
    {
        public readonly BigInteger Start;
        public readonly BigInteger End;

        public RequestRange(BigInteger start, BigInteger end)
        {
            Start = start;
            End = end;
        }
    }

    public sealed class RequestMessage : Message
    {
        public readonly RequestKind Kind;
        public readonly string NexusName;

        private Dictionary<string, RequestRange> _blockFetches;
        public IEnumerable<KeyValuePair<string, RequestRange>> Blocks => _blockFetches;

        public RequestMessage(Address address, string host, RequestKind kind, string nexusName) :base(Opcode.REQUEST, address, host)
        {
            Kind = kind;
            NexusName = nexusName;
        }

        public void SetBlocks(Dictionary<string, RequestRange> blockFetches)
        {
            this._blockFetches = blockFetches;
        }

        internal static RequestMessage FromReader(Address address, string host, BinaryReader reader)
        {
            var kind = (RequestKind)reader.ReadByte();
            var nexusName = reader.ReadVarString();
            var msg = new RequestMessage(address, host, kind, nexusName);

            if (kind.HasFlag(RequestKind.Blocks))
            {
                var count = reader.ReadVarInt();
                var fetches = new Dictionary<string, RequestRange>();
                while (count > 0)
                {
                    var key = reader.ReadVarString();
                    var start = reader.ReadBigInteger();
                    var end = reader.ReadBigInteger();
                    fetches[key] = new RequestRange(start, end);
                    count--;
                }

                msg.SetBlocks(fetches);
            }

            return msg;
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            writer.Write((byte)Kind);
            writer.WriteVarString(NexusName);

            if (Kind.HasFlag(RequestKind.Blocks))
            {
                Throw.IfNull(_blockFetches, nameof(_blockFetches));
                Throw.If(_blockFetches.Count > 1024, "max chain block fetches per request reached");
                writer.WriteVarInt(_blockFetches.Count);
                foreach (var entry in _blockFetches)
                {
                    writer.WriteVarString(entry.Key);
                    writer.WriteBigInteger(entry.Value.Start);
                    writer.WriteBigInteger(entry.Value.End);
                }
            }
        }

    }
}
