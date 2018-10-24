using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.VM.Utils;
using System;
using System.Collections.Generic;

namespace Phantasma.Blockchain
{
    // TODO this should be moved to a better place, refactored or even just deleted if no longer useful
    public class ChainSimulator
    {
        public Nexus Nexus { get; private set; }

        private System.Random _rnd;
        private List<KeyPair> _keys = new List<KeyPair>();
        private KeyPair _owner;

        private DateTime _currentTime;

        private Chain bankChain;

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

            int transferCount = 1 + _rnd.Next() % 10;

            while (transactions.Count < transferCount)
            {
                var source = _keys[_rnd.Next() % _keys.Count];

                var chain = Nexus.RootChain;
                var token = Nexus.NativeToken;

                switch (_rnd.Next() % 4)
                {
                    case 1:
                        {
                            var balance = chain.GetTokenBalance(token, source.Address);

                            var total = balance / 10;
                            if (total > 0)
                            {
                                var script = ScriptUtils.CallContractScript(chain, "SendTokens", bankChain.Address, source.Address, source.Address, token.Symbol, total);
                                var tx = new Transaction(script, 0, 0);
                                tx.Sign(source);
                                transactions.Add(tx);
                            }

                            break;
                        }

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
                                    transactions.Add(tx);
                                }
                            }
                            break;
                        }
                }

            }

            var block = new Block(Nexus.RootChain, _owner.Address, _currentTime, transactions, Nexus.RootChain.LastBlock);
            if (block.Chain.AddBlock(block))
            {
                _currentTime += TimeSpan.FromSeconds(15);
            }
            else
            { 
                throw new Exception("add block failed");
            }

        }
    }

}
