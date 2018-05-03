using PhantasmaChain.Cryptography;
using PhantasmaChain.Transactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace PhantasmaChain.Core
{
    public class Chain
    {
        private Dictionary<byte[], Transaction> _transactions = new Dictionary<byte[], Transaction>(new ByteArrayComparer());
        private Dictionary<uint, Block> _blocks = new Dictionary<uint, Block>();
        private Dictionary<byte[], Account> _accounts = new Dictionary<byte[], Account>(new ByteArrayComparer());

        private Dictionary<byte[], Token> _tokenIDMap = new Dictionary<byte[], Token>(new ByteArrayComparer());
        private Dictionary<string, Token> _tokenNameMap = new Dictionary<string, Token>();

        private List<Order> _orderBook = new List<Order>();

        public Token NativeToken { get; private set; }
        public IEnumerable<Block> Blocks => _blocks.Values;

        public uint Height => (uint)_blocks.Count;
        
        public Block lastBlock { get; private set; }

        public Logger Log;

        public Chain(KeyPair owner, Logger log = null)
        {
            this.Log = log != null ? log: LogIgnore;
            
            var block = CreateGenesisBlock(owner);
            if (!AddBlock(block))
            {
                throw new ChainException("Genesis block failure");
            }
        }

        private void LogIgnore(string s)
        {

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

            Log($"Increased chain height to {block.Height}");

            return true;
        }

        private Block CreateGenesisBlock(KeyPair owner)
        {
            var tx = new CreateTransaction(owner.PublicKey, 0, 0, Encoding.UTF8.GetBytes("SOUL"), "SOUL", 100000000, 100000000, Token.Attribute.Burnable | Token.Attribute.Tradable);
            tx.Sign(owner);

            var block = new Block(DateTime.UtcNow.ToTimestamp(), owner.PublicKey, new Transaction[] { tx });

            return block;
        }

        public Account GetAccount(byte[] publicKey)
        {
            if (_accounts.ContainsKey(publicKey))
            {
                return _accounts[publicKey];
            }

            return null;
        }

        public Account GetOrCreateAccount(byte[] publicKey)
        {
            var account = GetAccount(publicKey);

            if (account == null)
            {
                account = new Account(this, publicKey);
                _accounts[publicKey] = account;
            }

            return account;
        }

        public Token GetTokenByID(byte[] ID)
        {
            if (ID != null && _tokenIDMap.ContainsKey(ID))
            {
                return _tokenIDMap[ID];
            }

            return null;
        }

        public Token GetTokenByName(string name)
        {
            if (!string.IsNullOrEmpty(name) && _tokenNameMap.ContainsKey(name))
            {
                return _tokenNameMap[name];
            }

            return null;
        }

        internal Token CreateToken(byte[] witnessPublicKey, byte[] ID, string name, BigInteger initialSupply, BigInteger totalSupply, Token.Attribute flags, Action<Event> notify)
        {
            if (_tokenIDMap.ContainsKey(ID))
            {
                throw new ArgumentException($"ID {ID} already exists");
            }

            var token = new Token(ID, name, initialSupply, totalSupply, flags, witnessPublicKey);
            _tokenIDMap[ID] = token;

            Log($"Creating token {name} with owner {ChainUtils.PublicKeyToAddress(witnessPublicKey)}");

            var account = GetOrCreateAccount(witnessPublicKey);
            account.Deposit(token, initialSupply, notify);

            if (this.NativeToken == null)
            {
                this.NativeToken = token;
            }

            return token;
        }

        internal Order CreateOrder(UInt256 orderID, byte[] ownerPublicKey, byte[] baseTokenID, byte[] quoteTokenID, OrderKind orderKind, OrderSide side, BigInteger amount, BigInteger price, uint expiration, Action<Event> notify)
        {
            var currentBlock = this.Height;
            var order = new Order(orderID, ownerPublicKey, baseTokenID, quoteTokenID, orderKind, side, amount, price, currentBlock, expiration);

            var removed_orders = new HashSet<Order>();

            var amount_left = order.amount;

            // _orderBook must be sorted by blocktime
            foreach (var other in _orderBook)
            {
                if (other.side == order.side)
                {
                    continue;
                }

                if (!other.quoteTokenID.SequenceEqual(order.quoteTokenID))
                {
                    continue;
                }

                if (!other.baseTokenID.SequenceEqual(order.baseTokenID))
                {
                    continue;
                }

                switch (order.side)
                {
                    case OrderSide.Buy:
                        {
                            if (other.price <= order.price)
                            {
                                var total = order.amount;
                                if (other.amount < total)
                                {
                                    total = other.amount;
                                }

                                other.Fill(total, notify);
                            }
                            break;
                        }

                    case OrderSide.Sell:
                        {
                            break;
                        }
                }
            }

            foreach (var other in _orderBook)
            {                
                if (currentBlock >= other.expiration)
                {
                    removed_orders.Add(other);
                    continue;
                }
            }

            foreach (var entry in removed_orders)
            {
                _orderBook.Remove(entry);
            }

            if (!removed_orders.Contains(order))
            {
                _orderBook.Add(order);
            }

            return order;
        }
    }
}
