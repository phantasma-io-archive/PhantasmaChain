using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.VM.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain
{
    public class SideChainPendingTransaction
    {
        public Address address;
        public Hash hash;
        public Chain sourceChain;
        public Chain destChain;
        public Block block;
    }

    // TODO this should be moved to a better place, refactored or even just deleted if no longer useful
    public class ChainSimulator
    {
        public Nexus Nexus { get; private set; }

        private System.Random _rnd;
        private List<KeyPair> _keys = new List<KeyPair>();
        private KeyPair _owner;

        private DateTime _currentTime;

        private Chain bankChain;

        private List<SideChainPendingTransaction> _pendingTxs = new List<SideChainPendingTransaction>();

        public ChainSimulator(KeyPair ownerKey, int seed)
        {
            _owner = ownerKey;
            this.Nexus = new Nexus(_owner);

            this.bankChain = Nexus.FindChainByName("bank");

            var miner = KeyPair.Generate();
            var third = KeyPair.Generate();

            _rnd = new System.Random(seed);
            _keys.Add(_owner);

            _currentTime = new DateTime(2018, 8, 26);
        }

        public void GenerateBlock()
        {
            var transactions = new List<Transaction>();

            // there are more elegant ways of doing this...
            var txChainMap = new Dictionary<Hash, Chain>();
            var txHashMap = new Dictionary<Hash, Transaction>();

            int transferCount = 1 + _rnd.Next() % 10;
            while (transactions.Count < transferCount)
            {
                var source = _keys[_rnd.Next() % _keys.Count];

                var chain = Nexus.RootChain;
                Token token;

                switch (_rnd.Next() % 4)
                {
                    case 1: token = Nexus.StableToken; break;
                    default: token = Nexus.NativeToken; break;
                }


                switch (_rnd.Next() % 5)
                {
                    // side-chain send
                    case 1:
                        {
                            var balance = chain.GetTokenBalance(token, source.Address);

                            var total = balance / 10;
                            if (total > 0)
                            {
                                var script = ScriptUtils.CallContractScript(chain, "SendTokens", bankChain.Address, source.Address, source.Address, token.Symbol, total);
                                var tx = new Transaction(script, 0, 0);
                                tx.Sign(source);

                                txChainMap[tx.Hash] = chain;
                                txHashMap[tx.Hash] = tx;
                                transactions.Add(tx);

                                var pending = new SideChainPendingTransaction()
                                {
                                    address = source.Address,
                                    sourceChain = chain,
                                    destChain = bankChain,
                                    hash = tx.Hash,
                                    block = null,
                                };
                                _pendingTxs.Add(pending);
                            }

                            break;
                        }

                    // side-chain receive
                    case 2:
                        {
                            SideChainPendingTransaction targetTransaction = null;
                            foreach (var entry in _pendingTxs)
                            {
                                if (entry.address == source.Address && entry.sourceChain == chain)
                                {
                                    targetTransaction = entry;
                                    break;
                                }
                            }

                            if (targetTransaction != null && targetTransaction.block != null)
                            {
                                _pendingTxs.RemoveAll(x => x.hash == targetTransaction.hash);

                                chain = targetTransaction.destChain;

                                var script = ScriptUtils.CallContractScript(chain, "ReceiveTokens", targetTransaction.sourceChain.Address, targetTransaction.address, targetTransaction.hash);
                                var tx = new Transaction(script, 0, 0);
                                tx.Sign(source);

                                txChainMap[tx.Hash] = chain;
                                txHashMap[tx.Hash] = tx;
                                transactions.Add(tx);
                            }

                            break;
                        }

                    // stable claim
                    case 3:
                        {
                            chain = bankChain;
                            token = Nexus.NativeToken;

                            var balance = chain.GetTokenBalance(token, source.Address);

                            var total = balance / 10;
                            if (total > 0)
                            {
                                var script = ScriptUtils.CallContractScript(chain, "Claim", source.Address, total);
                                var tx = new Transaction(script, 0, 0);
                                tx.Sign(source);

                                txChainMap[tx.Hash] = chain;
                                txHashMap[tx.Hash] = tx;
                                transactions.Add(tx);
                            }

                            break;
                        }

                    // stable redeem
                    case 4:
                        {
                            chain = bankChain;
                            token = Nexus.StableToken;

                            var balance = chain.GetTokenBalance(token, source.Address);

                            var total = balance / 10;
                            if (total > 0)
                            {
                                var script = ScriptUtils.CallContractScript(chain, "Redeem", source.Address, total);
                                var tx = new Transaction(script, 0, 0);
                                tx.Sign(source);

                                txChainMap[tx.Hash] = chain;
                                txHashMap[tx.Hash] = tx;
                                transactions.Add(tx);
                            }

                            break;
                        }

                    // normal transfer
                    default:
                        {
                            var temp = _rnd.Next() % 5;
                            Address targetAddress;

                            if (_keys.Count < 2 || temp == 0)
                            {
                                var key = KeyPair.Generate();
                                _keys.Add(key);
                                targetAddress = key.Address;
                            }
                            else
                            {
                                targetAddress = _keys[_rnd.Next() % _keys.Count].Address;
                            }

                            if (source.Address != targetAddress)
                            {
                                var balance = chain.GetTokenBalance(token, source.Address);

                                var total = balance / 10;
                                if (total > 0)
                                {
                                    var script = ScriptUtils.CallContractScript(chain, "TransferTokens", source.Address, targetAddress, token.Symbol, total);
                                    var tx = new Transaction(script, 0, 0);
                                    tx.Sign(source);

                                    txChainMap[tx.Hash] = chain;
                                    txHashMap[tx.Hash] = tx;
                                    transactions.Add(tx);
                                }
                            }
                            break;
                        }
                }
            }

            var chains = txChainMap.Values.Distinct();

            foreach (var chain in chains)
            {
                var hashes = txChainMap.Where((p) => p.Value == chain).Select(x => x.Key);
                if (hashes.Any())
                {
                    var txs = new List<Transaction>();
                    foreach (var hash in hashes)
                    {
                        txs.Add(txHashMap[hash]);
                    }

                    var block = new Block(chain, _owner.Address, _currentTime, txs, chain.LastBlock);
                    if (block.Chain.AddBlock(block))
                    {
                        _currentTime += TimeSpan.FromSeconds(15);

                        foreach (var entry in _pendingTxs)
                        {
                            if (txHashMap.ContainsKey(entry.hash))
                            {
                                entry.block = block;
                            }
                        }
                    }
                    else
                    {
                        throw new Exception($"add block in {chain.Name} failed");
                    }
                }
            }
        }
    }

}
