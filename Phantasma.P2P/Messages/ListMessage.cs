using Phantasma.Core;
using Phantasma.Storage;
using Phantasma.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phantasma.Storage.Utils;
using Phantasma.Numerics;
using Phantasma.Blockchain;

namespace Phantasma.Network.P2P.Messages
{
    public struct ChainInfo
    {
        public readonly string name;
        public readonly string parentName;
        public readonly BigInteger height;

        public ChainInfo(string name, string parentName, BigInteger height)
        {
            this.name = name;
            this.parentName = parentName;
            this.height = height;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteVarString(name);
            writer.WriteVarString(parentName);
            writer.WriteBigInteger(height);
        }

        public static ChainInfo Unserialize(BinaryReader reader)
        {
            var name = reader.ReadVarString();
            var parentName = reader.ReadVarString();
            var height = reader.ReadBigInteger();
            return new ChainInfo(name, parentName, height);
        }
    }

    public struct BlockRange
    {
        public readonly Block[] blocks;
        public readonly Dictionary<Hash, Transaction> transactions;

        public BlockRange(Block[] blocks, Dictionary<Hash, Transaction> transactions)
        {
            this.blocks = blocks;
            this.transactions = transactions;
        }
    }

    public class ListMessage : Message
    {
        public readonly RequestKind Kind;

        private string[] _peers = null;
        public IEnumerable<string> Peers => _peers;

        private string[] _mempool = null;
        public IEnumerable<string> Mempool => _mempool;

        private ChainInfo[] _chains = null;
        public IEnumerable<ChainInfo> Chains => _chains;

        public const int MaxPeers = 255;
        public const int MaxChains = 128;
        public const int MaxTransactions = 1024; // for mempool
        public const int MaxBlocks = 512;

        // here the dictionary key is the chain name
        private Dictionary<string, BlockRange> _blockRanges;
        public IEnumerable<KeyValuePair<string, BlockRange>> Blocks => _blockRanges;

        public ListMessage(Address address, string host, RequestKind kind) : base(Opcode.LIST, address, host)
        {
            this.Kind = kind;
        }

        public void SetPeers(IEnumerable<string> peers)
        {
            this._peers = peers.ToArray();
            Throw.If(_peers.Length > MaxPeers, "too many peers");
        }

        public void SetMempool(IEnumerable<string> txs)
        {
            this._mempool = txs.ToArray();
            Throw.If(_mempool.Length > MaxTransactions, "too many txs");
        }

        public void SetChains(IEnumerable<ChainInfo> chains)
        {
            this._chains = chains.ToArray();
            Throw.If(_chains.Length > MaxChains, "too many chains");
        }

        internal static ListMessage FromReader(Address address, string host, BinaryReader reader)
        {
            var kind = (RequestKind)reader.ReadByte();

            var result = new ListMessage(address, host, kind);

            if (kind.HasFlag(RequestKind.Peers))
            {
                var peerCount = (int)reader.ReadVarInt();
                var peers = new string[peerCount];
                for (int i = 0; i < peerCount; i++)
                {
                    peers[i] = reader.ReadVarString();
                }

                result.SetPeers(peers);
            }

            if (kind.HasFlag(RequestKind.Chains))
            {
                var chainCount = (int)reader.ReadVarInt();
                var chains = new ChainInfo[chainCount];
                for (int i = 0; i < chainCount; i++)
                {
                    chains[i] = ChainInfo.Unserialize(reader);
                }

                result.SetChains(chains);
            }

            if (kind.HasFlag(RequestKind.Mempool))
            {
                var txCount = (int)reader.ReadVarInt();
                var txs = new string[txCount];
                for (int i = 0; i < txCount; i++)
                {
                    txs[i] = reader.ReadVarString();
                }

                result.SetMempool(txs);
            }

            if (kind.HasFlag(RequestKind.Blocks))
            {
                var chainCount = (int)reader.ReadVarInt();
                while (chainCount > 0)
                {
                    var chainName = reader.ReadVarString();
                    var blockCount = (int)reader.ReadVarInt();

                    var blocks = new Block[blockCount];
                    var transactions = new Dictionary<Hash, Transaction>();

                    for (int i=0; i<blockCount; i++)
                    {
                        var bytes = reader.ReadByteArray();
                        var block = Block.Unserialize(bytes);
                        blocks[i] = block;

                        foreach (var txHash in block.TransactionHashes)
                        {
                            bytes = reader.ReadByteArray();
                            var tx = Transaction.Unserialize(bytes);
                            transactions[txHash] = tx;
                        }
                    }

                    result.AddBlockRange(chainName, blocks, transactions);

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
                writer.WriteVarInt(_peers.Length);
                foreach (var peer in _peers)
                {
                    writer.WriteVarString(peer);
                }
            }

            if (Kind.HasFlag(RequestKind.Chains))
            {
                writer.WriteVarInt(_chains.Length);
                foreach (var chain in _chains)
                {
                    chain.Serialize(writer);
                }
            }

            if (Kind.HasFlag(RequestKind.Mempool))
            {
                if (_mempool != null)
                {
                    writer.WriteVarInt(_mempool.Length);
                    foreach (var tx in _mempool)
                    {
                        writer.WriteVarString(tx);
                    }
                }
                else
                {
                    writer.WriteVarInt(0);
                }
            }

            if (Kind.HasFlag(RequestKind.Blocks))
            {
                writer.WriteVarInt(_blockRanges.Count);
                foreach (var entry in _blockRanges)
                {
                    var range = entry.Value;
                    writer.WriteVarString(entry.Key);

                    writer.WriteVarInt(range.blocks.Length);
                    foreach (var block in range.blocks)
                    {
                        var bytes = block.ToByteArray(true);
                        writer.WriteByteArray(bytes);

                        foreach (var txHash in block.TransactionHashes)
                        {
                            var tx = range.transactions[txHash];
                            bytes = tx.ToByteArray(true);
                            writer.WriteByteArray(bytes);
                        }
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
                    yield return $"{entry.Key} blocks #{entry.Value.blocks[0].Height} to #{entry.Value.blocks[entry.Value.blocks.Length - 1].Height}";
                }
            }

            yield break;
        }

        public void AddBlockRange(string chainName, Block[] blocks, Dictionary<Hash, Transaction> transactions)
        {
            if (_blockRanges == null)
            {
                _blockRanges = new Dictionary<string, BlockRange>();
            }

            _blockRanges[chainName] = new BlockRange(blocks, transactions);
        }

        public void AddBlockRange(Chain chain, RequestRange range)
        {
            var count = (uint)(1 + range.End - range.Start);
            AddBlockRange(chain, range.Start, count);
        }

        public void AddBlockRange(Chain chain, BigInteger startHeight, uint count)
        {
            var targetHeight = startHeight + count;
            var blocks = new List<Block>();
            var transactions = new Dictionary<Hash, Transaction>();
            var height = startHeight;
            while (height < targetHeight)
            {
                var hash = chain.GetBlockHashAtHeight(height);
                var block = chain.GetBlockByHash(hash);
                blocks.Add(block);

                foreach (var txHash in block.TransactionHashes)
                {
                    var tx = chain.GetTransactionByHash(txHash);
                    transactions[txHash] = tx;
                }
                height++;
            }

            AddBlockRange(chain.Name, blocks.ToArray(), transactions);
        }
    }
}
