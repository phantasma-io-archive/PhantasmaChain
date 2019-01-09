using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core;
using Phantasma.Core.Log;
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
        public Token token;
    }

    // TODO this should be moved to a better place, refactored or even just deleted if no longer useful
    public class ChainSimulator
    {
        public Nexus Nexus { get; private set; }
        public DateTime CurrentTime;

        private System.Random _rnd;
        private List<KeyPair> _keys = new List<KeyPair>();
        private KeyPair _owner;

        private Chain bankChain;

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

        public readonly Logger Logger;

        public ChainSimulator(KeyPair ownerKey, int seed, Logger logger = null)
        {
            this.Logger = logger != null ? logger : new DummyLogger();

            _owner = ownerKey;
            this.Nexus = new Nexus("simnet", ownerKey.Address);

            if (!Nexus.CreateGenesisBlock(_owner))
            {
                throw new ChainException("Genesis block failure");
            }

            this.bankChain = Nexus.FindChainByName("bank");
            
            _rnd = new System.Random(seed);
            _keys.Add(_owner);

            CurrentTime = new DateTime(2018, 8, 26);

            var appsChain = Nexus.FindChainByName("apps");
            BeginBlock();
            GenerateSideChainSend(_owner, Nexus.NativeToken, Nexus.RootChain, _owner.Address, appsChain, TokenUtils.ToBigInteger(1, Nexus.NativeTokenDecimals), 0);
            var blockTx = EndBlock().First();

            BeginBlock();
            GenerateSideChainSettlement(_owner, Nexus.RootChain, appsChain, blockTx.Hash);
            GenerateAppRegistration(_owner, "nachomen", "https://nacho.men", "Collect, train and battle against other players in Nacho Men!");
            GenerateAppRegistration(_owner, "mystore", "https://my.store", "The future of digital content distribution!");

            GenerateToken(_owner, "NACHO", "Nachomen", 0, 0, TokenFlags.Transferable);
            EndBlock();

            BeginBlock();

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
                GenerateNft(_owner, nftKey.Address, Nexus.RootChain, token, new byte[0]);
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
            Logger.Message($"Begin block #{step}");
        }

        public IEnumerable<Block> EndBlock(Mempool mempool = null)
        {
            if (!blockOpen)
            {
                throw new Exception("Simulator block not open");
            }

            blockOpen = false;

            var blocks = new List<Block>();

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

                        var block = new Block(nextHeight, chain.Address, CurrentTime, hashes, prevHash);

                        bool submitted;

                        if (mempool != null)
                        {
                            submitted = true;
                            foreach (var tx in txs)
                            {
                                submitted |= mempool.Submit(tx);
                            }
                        }
                        else
                        {
                            submitted = chain.AddBlock(block, txs);
                        }

                        if (submitted)
                        {
                            blocks.Add(block);

                            CurrentTime += TimeSpan.FromMinutes(45);

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
                                        token = entry.token
                                    };

                                    _pendingBlocks.Add(pendingBlock);
                                    Logger.Message($"...Sending {entry.sourceChain.Name}=>{entry.destChain.Name}: {block.Hash}");
                                }
                            }

                            Logger.Message($"End block #{step}: {block.Hash}");
                        }
                        else
                        {
                            throw new Exception($"add block in {chain.Name} failed");
                        }
                    }
                }

                _pendingEntries.Clear();
                return blocks;
            }

            return Enumerable.Empty<Block>();
        }

        private Transaction MakeTransaction(KeyPair source, Chain chain, byte[] script)
        {
            var tx = new Transaction(Nexus.Name, chain.Name, script, CurrentTime + TimeSpan.FromSeconds(Mempool.MaxExpirationTimeDifferenceInSeconds / 2), 0);

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

            var script = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, 1, 9999).
                CallContract("nexus", "CreateToken", owner.Address, symbol, name, totalSupply, decimals, flags).
                SpendGas(owner.Address).
                EndScript();

            var tx = MakeTransaction(owner, chain, script);
            tx.Sign(owner);

            return tx;
        }

        public Transaction GenerateSideChainSend(KeyPair source, Token token, Chain sourceChain, Address targetAddress, Chain targetChain, BigInteger amount, BigInteger fee)
        {
            Throw.IfNull(source, nameof(source));
            Throw.IfNull(token, nameof(token));
            Throw.IfNull(sourceChain, nameof(sourceChain));
            Throw.IfNull(targetChain, nameof(targetChain));
            Throw.If(amount<=0, "positive amount required");

            if (source.Address == targetAddress && token == Nexus.NativeToken)
            {
                Throw.If(fee != 0, "no fees for same address");
            }
            else
            {
                Throw.If(fee <= 0, "fee required when target is different address or token not native");
            }

            var sb = ScriptUtils.
                BeginScript().
                AllowGas(source.Address, 1, 9999);

            if (targetAddress != source.Address)
            {
                sb.CallContract("token", "SendTokens", targetChain.Address, source.Address, source.Address, token.Symbol, fee);
            }

            var script = 
                sb.CallContract("token", "SendTokens", targetChain.Address, source.Address, targetAddress, token.Symbol, amount).
                SpendGas(source.Address).
                EndScript();

            var tx = MakeTransaction(source, sourceChain, script);

            _pendingEntries[sourceChain] = new SideChainPendingBlock()
            {
                sourceChain = sourceChain,
                destChain = targetChain,
                hash = null,
                token = token
            };
            return tx;
        }

        public Transaction GenerateSideChainSettlement(KeyPair source, Chain sourceChain, Chain destChain, Hash targetHash)
        {
            _pendingBlocks.RemoveAll(x => x.hash == targetHash);

            var script = ScriptUtils.
                BeginScript().
                CallContract("token", "SettleBlock", sourceChain.Address, targetHash).
                AllowGas(source.Address, 1, 9999).
                SpendGas(source.Address).
                EndScript();
            var tx = MakeTransaction(source, destChain, script);
            return tx;
        }

        public Transaction GenerateStableClaim(KeyPair source, Chain sourceChain, BigInteger amount)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, 1, 9999).CallContract("bank", "Claim", source.Address, amount).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, sourceChain, script);
            tx.Sign(source);
            return tx;
        }

        public Transaction GenerateStableRedeem(KeyPair source, Chain sourceChain, BigInteger amount)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, 1, 9999).CallContract("bank", "Redeem", source.Address, amount).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, sourceChain, script);
            return tx;
        }

        public Transaction GenerateAccountRegistration(KeyPair source, string name)
        {
            var sourceChain = this.Nexus.RootChain;
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, 1, 9999).CallContract("account", "Register", source.Address, name).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, sourceChain, script);

            pendingNames.Add(source.Address);
            return tx;
        }

        public Transaction GenerateChain(KeyPair source, Chain parentchain, string name)
        {
            var script = ScriptUtils.BeginScript().
                AllowGas(source.Address, 1, 9999).
                CallContract("nexus", "CreateChain", source.Address, name, parentchain.Name).
                SpendGas(source.Address).
                EndScript();
            var tx = MakeTransaction(source, Nexus.RootChain, script);
            return tx;
        }

        public Transaction GenerateTransfer(KeyPair source, Address dest, Chain chain, Token token, BigInteger amount)
        {
            var script = ScriptUtils.BeginScript().
                AllowGas(source.Address, 1, 9999).
                CallContract("token", "TransferTokens", source.Address, dest, token.Symbol, amount).
                SpendGas(source.Address).
                EndScript();
            var tx = MakeTransaction(source, chain, script);
            return tx;
        }

        public Transaction GenerateNftTransfer(KeyPair source, Address dest, Chain chain, Token token, BigInteger tokenId)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, 1, 9999).CallContract("token", "TransferToken", source.Address, dest, token.Symbol, tokenId).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, chain, script);
            return tx;
        }

        public Transaction GenerateNftSidechainTransfer(KeyPair source, Address destAddress, Chain sourceChain,
            Chain destChain, Token token, BigInteger tokenId)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, 1, 9999).CallContract("token", "SendToken", destChain.Address, source.Address, destAddress, token.Symbol, tokenId).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, sourceChain, script);
            return tx;
        }

        public Transaction GenerateNftBurn(KeyPair source, Chain chain, Token token, BigInteger tokenId)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, 1, 9999).CallContract("token", "BurnToken", source.Address, token.Symbol, tokenId).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, chain, script);
            return tx;
        }

        public Transaction GenerateNft(KeyPair source, Address destAddress, Chain chain, Token token, byte[] data)
        {
            var script = ScriptUtils.
                BeginScript().
                AllowGas(source.Address, 1, 9999).
                CallContract("token", "MintToken", destAddress, token.Symbol, data).
                SpendGas(source.Address).
                EndScript();

            var tx = MakeTransaction(source, chain, script);
            return tx;
        }

        public Transaction GenerateAppRegistration(KeyPair source, string name, string url, string description)
        {
            var contract = "apps";

            var chain = Nexus.FindChainByName("apps");
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, 1, 9999).CallContract(contract, "RegisterApp", source.Address, name).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, chain, script);

            script = ScriptUtils.BeginScript().AllowGas(source.Address, 1, 9999).CallContract(contract, "SetAppUrl", name, url).SpendGas(source.Address).EndScript();
            tx = MakeTransaction(source, chain, script);

            script = ScriptUtils.BeginScript().AllowGas(source.Address, 1, 9999).CallContract(contract, "SetAppDescription", name, description).SpendGas(source.Address).EndScript();
            tx = MakeTransaction(source, chain, script);

            return tx;
        }

        public Transaction GenerateSetTokenViewer(KeyPair source, Token token, string url)
        {
            var chain = Nexus.FindChainByName("apps");
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, 1, 9999).CallContract("apps", "SetTokenViewer", token.Symbol, url).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, chain, script);
            
            return tx;
        }

        private int step;

        public void GenerateRandomBlock(Mempool mempool = null)
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
                                GenerateSideChainSend(source, token, sourceChain, source.Address, targetChain, total, 0);
                            }
                            break;
                        }

                    // side-chain receive
                    case 2:
                        {
                            if (_pendingBlocks.Any())
                            {
                                var pendingBlock = _pendingBlocks.First();

                                if (mempool == null || Nexus.GetConfirmationsOfHash(pendingBlock.hash) > 0)
                                {

                                    var balance = pendingBlock.destChain.GetTokenBalance(pendingBlock.token, source.Address);
                                    if (balance > 0)
                                    {
                                        Logger.Message($"...Settling {pendingBlock.sourceChain.Name}=>{pendingBlock.destChain.Name}: {pendingBlock.hash}");
                                        GenerateSideChainSettlement(source, pendingBlock.sourceChain, pendingBlock.destChain, pendingBlock.hash);
                                    }
                                }
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

                            var bankContract = bankChain.FindContract<BankContract>("bank");
                            var rate = bankContract.GetRate(Nexus.NativeTokenSymbol);
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
                            sourceChain = this.Nexus.RootChain;                            
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

            EndBlock(mempool);
        }
    }

}
