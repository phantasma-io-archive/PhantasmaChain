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

    public sealed class RequestMessage : Message
    {
        public readonly RequestKind Kind;
        public readonly string NexusName;

        private Dictionary<string, BigInteger> _blockFetches;
        public IEnumerable<KeyValuePair<string, BigInteger>> Blocks => _blockFetches;

        public RequestMessage(RequestKind kind, string nexusName, Address address) :base(Opcode.REQUEST, address)
        {
            Kind = kind;
            NexusName = nexusName;
        }

        public void SetBlocks(Dictionary<string, BigInteger> blockFetches)
        {
            this._blockFetches = blockFetches;
        }

        internal static RequestMessage FromReader(Address address, BinaryReader reader)
        {
            var kind = (RequestKind)reader.ReadByte();
            var nexusName = reader.ReadVarString();
            var msg = new RequestMessage(kind, nexusName, address);

            if (kind.HasFlag(RequestKind.Blocks))
            {
                var count = reader.ReadVarInt();
                var fetches = new Dictionary<string, BigInteger>();
                while (count > 0)
                {
                    var key = reader.ReadVarString();
                    var height = reader.ReadBigInteger();
                    fetches[key] = height;
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
                    writer.WriteBigInteger(entry.Value);
                }
            }
        }

    }
}
