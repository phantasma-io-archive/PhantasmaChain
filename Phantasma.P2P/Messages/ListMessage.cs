using Phantasma.Core;
using Phantasma.IO;
using Phantasma.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Network.P2P.Messages
{
    public struct ChainInfo
    {
        public readonly string name;
        public readonly string parentName;
        public readonly uint height;

        public ChainInfo(string name, string parentName, uint height)
        {
            this.name = name;
            this.parentName = parentName;
            this.height = height;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteVarString(name);
            writer.WriteVarString(parentName);
            writer.WriteVarInt(height);
        }

        public static ChainInfo Unserialize(BinaryReader reader)
        {
            var name = reader.ReadVarString();
            var parentName = reader.ReadVarString();
            var height = (uint)reader.ReadVarInt();
            return new ChainInfo(name, parentName, height);
        }
    }

    public struct BlockRange
    {
        public uint startHeight;
        public string[] rawBlocks;
    }

    public class ListMessage : Message
    {
        public readonly RequestKind Kind;

        private Endpoint[] _peers = null;
        public IEnumerable<Endpoint> Peers => _peers;

        private string[] _mempool = null;
        public IEnumerable<string> Mempool => _mempool;

        private ChainInfo[] _chains = null;
        public IEnumerable<ChainInfo> Chains => _chains;

        private Dictionary<string, BlockRange> _blockRanges;
        public IEnumerable<KeyValuePair<string, BlockRange>> Blocks => _blockRanges;

        public ListMessage(Address address, RequestKind kind) : base(Opcode.LIST, address)
        {
            this.Kind = kind;
        }

        public void SetPeers(IEnumerable<Endpoint> peers)
        {
            this._peers = peers.ToArray();
            Throw.If(_peers.Length > 255, "too many peers");
        }

        public void SetMempool(IEnumerable<string> txs)
        {
            this._mempool = txs.ToArray();
            Throw.If(_mempool.Length > 65535, "too many txs");
        }

        public void SetChains(IEnumerable<ChainInfo> chains)
        {
            this._chains = chains.ToArray();
            Throw.If(_chains.Length > 65535, "too many chains");
        }

        internal static ListMessage FromReader(Address address, BinaryReader reader)
        {
            var kind = (RequestKind)reader.ReadByte();

            var result = new ListMessage(address, kind);

            if (kind.HasFlag(RequestKind.Peers))
            {
                var peerCount = reader.ReadByte();
                var peers = new Endpoint[peerCount];
                for (int i = 0; i < peerCount; i++)
                {
                    peers[i] = Endpoint.Unserialize(reader);
                }

                result.SetPeers(peers);
            }

            if (kind.HasFlag(RequestKind.Chains))
            {
                var chainCount = reader.ReadUInt16();
                var chains = new ChainInfo[chainCount];
                for (int i = 0; i < chainCount; i++)
                {
                    chains[i] = ChainInfo.Unserialize(reader);
                }

                result.SetChains(chains);
            }

            if (kind.HasFlag(RequestKind.Mempool))
            {
                var txCount = reader.ReadUInt16();
                var txs = new string[txCount];
                for (int i = 0; i < txCount; i++)
                {
                    txs[i] = reader.ReadVarString();
                }

                result.SetMempool(txs);
            }

            if (kind.HasFlag(RequestKind.Blocks))
            {
                var chainCount = reader.ReadUInt16();
                while (chainCount > 0)
                {
                    var chainName = reader.ReadVarString();
                    var blockHeight = reader.ReadUInt32();
                    var blockCount = reader.ReadUInt16();

                    var rawBlocks = new string[blockCount];
                    for (int i=0; i<blockCount; i++)
                    {
                        rawBlocks[i] = reader.ReadVarString();
                    }

                    result.AddBlockRange(chainName, blockHeight, rawBlocks);

                    chainCount--;
                }
            }

            return result;
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            writer.Write((byte)Kind);

            if (Kind.HasFlag(RequestKind.Peers))
            {
                writer.Write((byte)_peers.Length);
                foreach (var peer in _peers)
                {
                    peer.Serialize(writer);
                }
            }

            if (Kind.HasFlag(RequestKind.Chains))
            {
                writer.Write((ushort)_chains.Length);
                foreach (var chain in _chains)
                {
                    chain.Serialize(writer);
                }
            }

            if (Kind.HasFlag(RequestKind.Mempool))
            {
                writer.Write((ushort)_mempool.Length);
                foreach (var tx in _mempool)
                {
                    writer.WriteVarString(tx);
                }
            }

            if (Kind.HasFlag(RequestKind.Blocks))
            {
                writer.Write((ushort)_blockRanges.Count);
                foreach (var entry in _blockRanges)
                {
                    writer.WriteVarString(entry.Key);
                    writer.Write((uint)entry.Value.startHeight);
                    writer.Write((ushort)entry.Value.rawBlocks.Length);
                    foreach (var rawBlock in entry.Value.rawBlocks)
                    {
                        writer.WriteVarString(rawBlock);
                    }
                }
            }
        }

        public override IEnumerable<string> GetDescription()
        {
            yield return Kind.ToString();

            if (Kind.HasFlag(RequestKind.Peers))
            {
                foreach (var peer in Peers)
                {
                    yield return "Peer: " + peer.ToString();
                }
            }

            if (Kind.HasFlag(RequestKind.Chains))
            {
                foreach (var chain in Chains)
                {
                    yield return "Chain: " + chain.name+" ("+chain.height+" blocks)";
                }
            }

            if (Kind.HasFlag(RequestKind.Mempool))
            {
                foreach (var tx in Mempool)
                {
                    yield return "Tx: " + tx;
                }
            }

            if (Kind.HasFlag(RequestKind.Blocks))
            {
                foreach (var entry in _blockRanges)
                {
                    yield return $"{entry.Key} blocks #{entry.Value.startHeight} to #{entry.Value.startHeight + (entry.Value.rawBlocks.Length-1)}";
                }
            }

            yield break;
        }

        public void AddBlockRange(string chainName, uint startHeight, IEnumerable<string> rawBlocks)
        {
            if (_blockRanges == null)
            {
                _blockRanges = new Dictionary<string, BlockRange>();
            }

            _blockRanges[chainName] = new BlockRange() { startHeight = startHeight, rawBlocks = rawBlocks.ToArray() };
        }
    }
}