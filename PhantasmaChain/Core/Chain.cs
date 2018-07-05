using Phantasma.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Core
{
    public partial class Chain
    {
        private Dictionary<byte[], Transaction> _transactions = new Dictionary<byte[], Transaction>(new ByteArrayComparer());
        private Dictionary<uint, Block> _blocks = new Dictionary<uint, Block>();
        private Dictionary<byte[], Contract> _contracts = new Dictionary<byte[], Contract>(new ByteArrayComparer());
        private TrieNode _contractLookup = new TrieNode();

        public byte[] NativeTokenPubKey { get; private set; }
        public byte[] DistributionPubKey { get; private set; }

        public IEnumerable<Block> Blocks => _blocks.Values;

        public uint Height => (uint)_blocks.Count;
        
        public Block lastBlock { get; private set; }

        public Chain(KeyPair owner)
        {          
            var block = CreateGenesisBlock(owner);
            if (!AddBlock(block))
            {
                throw new ChainException("Genesis block failure");
            }
        }

        public bool AddBlock(Block block)
        {
            if (lastBlock != null)
            {
                if (lastBlock.Height != block.Height - 1)
                {
                    return false;
                }

                if (!block.PreviousHash.SequenceEqual(lastBlock.Hash))
                {
                    return false;
                }
            }

            foreach (var tx in block.Transactions)
            {
                if (!tx.IsValid(this))
                {
                    return false;
                }
            }

            _blocks[block.Height] = block;
            lastBlock = block;

            foreach (var tx in block.Transactions)
            {
                tx.Execute(this, block.Notify);
                _transactions[tx.Hash] = tx;
            }

            Logger.Message($"Increased chain height to {block.Height}");

            return true;
        }

        public bool HasContract(byte[] publicKey)
        {
            return _contracts.ContainsKey(publicKey);
        }

        public Contract FindContract(byte[] publicKey)
        {
            if (_contracts.ContainsKey(publicKey))
            {
                return _contracts[publicKey];
            }

            return null;
        }

/*        public Contract GetOrCreateAccount(byte[] publicKey)
        {
            var account = FindContract(publicKey);

            if (account == null)
            {
                account = new AccountContract(this, publicKey);
                _contracts[publicKey] = account;
            }

            return account;
        }*/

        // returns public key of the specified name if existing
        public byte[] LookUpContract(string name)
        {
            var pubKey = _contractLookup.Find(name);
            return pubKey;
        }

    }
}
