using Phantasma.Contracts;
using Phantasma.Cryptography;
using Phantasma.Utils;
using Phantasma.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Phantasma.Core
{
    public class ChainException : Exception
    {
        public ChainException(string msg) : base(msg)
        {

        }
    }

    public class Chain
    {
        private Dictionary<byte[], Transaction> _transactions = new Dictionary<byte[], Transaction>(new ByteArrayComparer());
        private Dictionary<uint, Block> _blocks = new Dictionary<uint, Block>();
        private Dictionary<byte[], Account> _accounts = new Dictionary<byte[], Account>(new ByteArrayComparer());

        private Dictionary<string, Token> _tokenIDMap = new Dictionary<string, Token>();

        private Dictionary<string, Token> _tokenNameMap = new Dictionary<string, Token>();

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
            var script = ScriptUtils.TokenIssueScript("Phantasma","SOUL", 100000000, 100000000, TokenAttribute.Burnable | TokenAttribute.Tradable);
            var tx = new Transaction(owner.PublicKey, script, 0, 0);
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

        public Token GetTokenByID(string symbol)
        {
            if (symbol != null && _tokenIDMap.ContainsKey(symbol))
            {
                return _tokenIDMap[symbol];
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

        internal Token CreateToken(byte[] witnessPublicKey, string symbol, string name, BigInteger initialSupply, BigInteger totalSupply, TokenAttribute flags, Action<Event> notify)
        {
            if (_tokenIDMap.ContainsKey(symbol))
            {
                throw new ArgumentException($"{symbol} already exists");
            }

            var token = new Token(symbol, name, initialSupply, totalSupply, flags, witnessPublicKey);
            _tokenIDMap[symbol] = token;

            Log($"Creating token {name} with owner {CryptoUtils.PublicKeyToAddress(witnessPublicKey)}");

            var account = GetOrCreateAccount(witnessPublicKey);
            //account.Deposit(token, initialSupply, notify);

            if (this.NativeToken == null)
            {
                this.NativeToken = token;
            }

            return token;
        }

        internal void RegisterInterop(RuntimeVM vm)
        {
            vm.RegisterMethod("Asset.Deploy", Asset_Deploy);
        }

        #region interop API 
        private void Asset_Deploy(VirtualMachine vm)
        {
            var name = vm.GetRegister(0).AsString();
            var symbol = vm.GetRegister(1).AsString();

            var initialSupply = vm.GetRegister(2).AsNumber();
            var totalSupply = vm.GetRegister(3).AsNumber();
            var flags = (TokenAttribute)((byte)vm.GetRegister(4).AsNumber()); 

            var runtime = (RuntimeVM)vm;
            var tx = runtime.Transaction;
            var token = CreateToken(tx.PublicKey, symbol, name, initialSupply, totalSupply, flags, null);
        }
        #endregion

    }
}
