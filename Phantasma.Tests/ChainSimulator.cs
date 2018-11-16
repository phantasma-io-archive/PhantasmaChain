using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Tests
{
    public class SideChainPendingBlock
    {
        public Hash hash;
        public Chain sourceChain;
        public Chain destChain;
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
        private Chain accountChain;

        private static readonly string[] accountNames = {
            "aberration", "absence", "aceman", "acid", "alakazam", "alien", "alpha", "angel", "angler", "anomaly", "answer", "antsharer", "aqua", "archangel",
            "aspect", "atom", "avatar", "azure", "behemoth", "beta", "bishop", "bite", "blade", "blank", "blazer", "bliss", "boggle", "bolt",
            "bullet", "bullseye", "burn", "chaos", "charade", "charm", "chase", "chief", "chimera", "chronicle", "cipher", "claw", "cloud", "combo",
            "comet", "complex", "conjurer", "cowboy", "craze", "crotchet", "crow", "crypto", "cryptonic", "curse", "dagger", "dante", "daydream",
            "dexter", "diablo", "doctor", "doppelganger", "drake", "dread", "ecstasy", "enigma", "epitome", "essence", "eternity", "face",
            "fetish", "fiend", "flash", "fragment", "freak", "fury", "ghoul", "gloom", "gluttony", "grace", "griffin", "grim",
            "whiz", "wolf", "wrath", "zero", "zigzag", "zion"
        };

        private Dictionary<Chain, SideChainPendingBlock> _pendingEntries = new Dictionary<Chain, SideChainPendingBlock>();
        private List<SideChainPendingBlock> _pendingBlocks = new List<SideChainPendingBlock>();

        public ChainSimulator(KeyPair ownerKey, int seed)
        {
            _owner = ownerKey;
            this.Nexus = new Nexus("simnet", _owner);

            this.bankChain = Nexus.FindChainByKind(ContractKind.Bank);
            this.accountChain = Nexus.FindChainByKind(ContractKind.Account);

            _rnd = new System.Random(seed);
            _keys.Add(_owner);

            _currentTime = new DateTime(2018, 8, 26);

            BeginBlock();
            GenerateAppRegistration(_owner, "nachomen", "https://nacho.men", "Collect, train and battle against other players in Nacho Men!");
            GenerateAppRegistration(_owner, "mystore", "https://my.store", "The future of digital content distribution!");

            GenerateToken(_owner, "NACHO", "Nachomen", 0, 0, TokenFlags.Transferable);
            EndBlock();

            BeginBlock();

            var trophy = Nexus.FindTokenBySymbol("TROPHY");
            RandomSpreadNFC(trophy);

            var nacho = Nexus.FindTokenBySymbol("NACHO");
            RandomSpreadNFC(nacho);

            GenerateSetTokenViewer(_owner, nacho, "https://nacho.men/luchador/body/$ID");

            EndBlock();
        }

        private void RandomSpreadNFC(Token token)
        {
            Throw.IfNull(token, nameof(token));
            Throw.If(token.IsFungible, "expected NFT");

            for (int i = 1; i < 5; i++)
            {
                var nftKey = KeyPair.Generate();
                GenerateNFT(_owner, nftKey.Address, Nexus.RootChain, token, new byte[0]);
            }
        }

        private List<Transaction> transactions = new List<Transaction>();

        // there are more elegant ways of doing this...
        private Dictionary<Hash, Chain> txChainMap = new Dictionary<Hash, Chain>();
        private Dictionary<Hash, Transaction>  txHashMap = new Dictionary<Hash, Transaction>();

        private HashSet<Address> pendingNames = new HashSet<Address>();

        private bool blockOpen = false;

        public void BeginBlock()
        {
            if (blockOpen)
            {
                throw new Exception("Simulator block not terminated");
            }

            transactions.Clear();
            txChainMap.Clear();
            txHashMap.Clear();
            pendingNames.Clear();

            blockOpen = true;

            step++;
            Console.WriteLine($"Begin block #{step}");
        }

        public bool EndBlock()
        {
            if (!blockOpen)
            {
                throw new Exception("Simulator block not open");
            }

            blockOpen = false;

            if (txChainMap.Count > 0)
            {
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

                        uint nextHeight = chain.LastBlock != null ? chain.LastBlock.Height + 1 : Chain.InitialHeight;
                        var prevHash = chain.LastBlock != null ? chain.LastBlock.Hash : Hash.Null;

                        var block = new Block(nextHeight, chain.Address, _owner.Address, _currentTime, hashes, prevHash);
                        if (chain.AddBlock(block, txs))
                        {
                            _currentTime += TimeSpan.FromMinutes(45);

                            // add the finished block hash to each pending side chain tx
                            if (_pendingEntries.Count > 0)
                            {
                                foreach (var entry in _pendingEntries.Values)
                                {
                                    if (entry.sourceChain != chain) continue;

                                    var pendingBlock = new SideChainPendingBlock()
                                    {
                                        sourceChain = entry.sourceChain,
                                        destChain = entry.destChain,
                                        hash = block.Hash,
                                    };

                                    _pendingBlocks.Add(pendingBlock);
                                    Console.WriteLine($"...Sending {entry.sourceChain.Name}=>{entry.destChain.Name}: {block.Hash}");
                                }
                            }

                            Console.WriteLine($"End block #{step}: {block.Hash}");
                        }
                        else
                        {
                            throw new Exception($"add block in {chain.Name} failed");
                        }
                    }
                }

                _pendingEntries.Clear();
                return true;
            }

            return false;
        }

        private Transaction MakeTransaction(KeyPair source, Chain chain, byte[] script)
        {
            var tx = new Transaction(Nexus.Name, script, 0, 0, _currentTime + TimeSpan.FromDays(10), 0);

            if (source != null)
            {
                tx.Sign(source);
            }

            txChainMap[tx.Hash] = chain;
            txHashMap[tx.Hash] = tx;
            transactions.Add(tx);

            return tx;
        }

        public Transaction GenerateToken(KeyPair owner, string symbol, string name, BigInteger totalSupply, int decimals, TokenFlags flags)
        {
            var chain = Nexus.RootChain;

            var script = ScriptUtils.CallContractScript(chain.Address, "CreateToken", owner.Address, symbol, name, totalSupply, decimals, flags);

            var tx = MakeTransaction(owner, chain, script);
            tx.Sign(owner);

            return tx;
        }

        public Transaction GenerateSideChainSend(KeyPair source, Token token, Chain sourceChain, Chain targetChain, BigInteger amount)
        {
            var script = ScriptUtils.CallContractScript(sourceChain.Address, "SendTokens", targetChain.Address, source.Address, source.Address, token.Symbol, amount);
            var tx = MakeTransaction(source, sourceChain, script);

            _pendingEntries[sourceChain] = new SideChainPendingBlock()
            {
                sourceChain = sourceChain,
                destChain = targetChain,
                hash = null,
            };
            return tx;
        }

        public Transaction GenerateSideChainSettlement(Chain sourceChain, Chain destChain, Hash targetHash)
        {
            _pendingBlocks.RemoveAll(x => x.hash == targetHash);

            var script = ScriptUtils.CallContractScript(destChain.Address, "SettleBlock", sourceChain.Address, targetHash);
            var tx = MakeTransaction(null, destChain, script);
            return tx;
        }

        public Transaction GenerateStableClaim(KeyPair source, Chain sourceChain, BigInteger amount)
        {
            var script = ScriptUtils.CallContractScript(sourceChain.Address, "Claim", source.Address, amount);
            var tx = MakeTransaction(source, sourceChain, script);
            tx.Sign(source);
            return tx;
        }

        public Transaction GenerateStableRedeem(KeyPair source, Chain sourceChain, BigInteger amount)
        {
            var script = ScriptUtils.CallContractScript(sourceChain.Address, "Redeem", source.Address, amount);
            var tx = MakeTransaction(source, sourceChain, script);
            return tx;
        }

        public Transaction GenerateAccountRegistration(KeyPair source, string name)
        {
            var sourceChain = accountChain;
            var script = ScriptUtils.CallContractScript(sourceChain.Address, "Register", source.Address, name);
            var tx = MakeTransaction(source, sourceChain, script);

            pendingNames.Add(source.Address);
            return tx;
        }

        public Transaction GenerateTransfer(KeyPair source, Address dest, Chain chain, Token token, BigInteger amount)
        {
            var script = ScriptUtils.CallContractScript(chain.Address, "TransferTokens", source.Address, dest, token.Symbol, amount);
            var tx = MakeTransaction(source, chain, script);
            return tx;
        }

        public Transaction GenerateNFT(KeyPair source, Address address, Chain chain, Token token, byte[] data)
        {
            var script = ScriptUtils.CallContractScript(chain.Address, "MintToken", source.Address, token.Symbol, data);
            var tx = MakeTransaction(source, chain, script);
            return tx;
        }
    
        public Transaction GenerateAppRegistration(KeyPair source, string name, string url, string description)
        {
            var chain = Nexus.FindChainByName("apps");
            var script = ScriptUtils.CallContractScript(chain.Address, "RegisterApp", source.Address, name);
            var tx = MakeTransaction(source, chain, script);

            script = ScriptUtils.CallContractScript(chain.Address, "SetAppUrl", name, url);
            tx = MakeTransaction(source, chain, script);

            script = ScriptUtils.CallContractScript(chain.Address, "SetAppDescription", name, description);
            tx = MakeTransaction(source, chain, script);

            return tx;
        }

        public Transaction GenerateSetTokenViewer(KeyPair source, Token token, string url)
        {
            var chain = Nexus.RootChain;
            var script = ScriptUtils.CallContractScript(chain.Address, "SetTokenViewer", source.Address, token.Symbol, url);
            var tx = MakeTransaction(source, chain, script);
            
            return tx;
        }

        private int step;

        public void GenerateRandomBlock()
        {
            BeginBlock();

            int transferCount = 1 + _rnd.Next() % 10;
            while (transactions.Count < transferCount)
            {
                var source = _keys[_rnd.Next() % _keys.Count];

                var sourceChain = Nexus.RootChain;
                Token token;

                switch (_rnd.Next() % 4)
                {
                    case 1: token = Nexus.StableToken; break;
                    default: token = Nexus.NativeToken; break;
                }


                switch (_rnd.Next() % 7)
                {
                    // side-chain send
                    case 1:
                        {
                            var sourceChainList = Nexus.Chains.ToArray();
                            sourceChain = sourceChainList[_rnd.Next() % sourceChainList.Length];

                            var targetChainList = Nexus.Chains.Where(x => x.ParentChain == sourceChain || sourceChain.ParentChain == x).ToArray();
                            var targetChain = targetChainList[_rnd.Next() % targetChainList.Length];

                            var balance = sourceChain.GetTokenBalance(token, source.Address);

                            var total = balance / 10;
                            if (total > 0)
                            {
                                GenerateSideChainSend(source, token, sourceChain, targetChain, total);
                            }
                            break;
                        }

                    // side-chain receive
                    case 2:
                        {
                            if (_pendingBlocks.Any())
                            {
                                var pendingBlock = _pendingBlocks.First();
                                Console.WriteLine($"...Settling {pendingBlock.sourceChain.Name}=>{pendingBlock.destChain.Name}: {pendingBlock.hash}");

                                GenerateSideChainSettlement(pendingBlock.sourceChain, pendingBlock.destChain, pendingBlock.hash);
                            }

                            break;
                        }

                    // stable claim
                    case 3:
                        {
                            sourceChain = bankChain;
                            token = Nexus.NativeToken;

                            var balance = sourceChain.GetTokenBalance(token, source.Address);

                            var total = balance / 10;
                            if (total > 0)
                            {
                                GenerateStableClaim(source, sourceChain, total);
                            }

                            break;
                        }

                    // stable redeem
                    case 4:
                        {
                            sourceChain = bankChain;
                            token = Nexus.StableToken;

                            var balance = sourceChain.GetTokenBalance(token, source.Address);

                            var rate = ((BankContract)bankChain.Contract).GetRate(Nexus.NativeTokenSymbol);
                            var total = balance / 10;
                            if (total >= rate)
                            {
                                GenerateStableRedeem(source, sourceChain, total);
                            }

                            break;
                        }

                    // name register
                    case 5:
                        {
                            sourceChain = accountChain;
                            token = Nexus.NativeToken;

                            var balance = sourceChain.GetTokenBalance(token, source.Address);
                            if (balance >= AccountContract.RegistrationCost && !pendingNames.Contains(source.Address))
                            {
                                var randomName = accountNames[_rnd.Next() % accountNames.Length];

                                switch (_rnd.Next() % 10)
                                {
                                    case 1:
                                    case 2:
                                        randomName += (_rnd.Next() % 10).ToString();
                                        break;

                                    case 3:
                                    case 4:
                                    case 5:
                                        randomName += (10 + _rnd.Next() % 90).ToString();
                                        break;

                                    case 6:
                                        randomName += (100 +_rnd.Next() % 900).ToString();
                                        break;
                                }

                                var currentName = Nexus.LookUpAddress(source.Address);
                                if (currentName == AccountContract.ANONYMOUS)
                                {
                                    var lookup = Nexus.LookUpName(randomName);
                                    if (lookup == Address.Null)
                                    {
                                        GenerateAccountRegistration(source, randomName);
                                    }
                                }
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
                                var balance = sourceChain.GetTokenBalance(token, source.Address);

                                var total = balance / 10;
                                if (total > 0)
                                {
                                    GenerateTransfer(source, targetAddress, sourceChain, token, total);
                                }
                            }
                            break;
                        }
                }
            }

            EndBlock();
        }
    }

}
