using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using Phantasma.Storage;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.CodeGen.Assembler;

namespace Phantasma.Simulator
{
    public class SideChainPendingBlock
    {
        public Hash hash;
        public Chain sourceChain;
        public Chain destChain;
        public string tokenSymbol;
    }

    public struct SimNFTData
    {
        public byte A;
        public byte B;
        public byte C;
    }

    public struct SimulatorChainSwap
    {
        public Hash hash;
        public string platformName;
        public string sourceAddress;
        public string symbol;
        public decimal amount;
    }

    // TODO this should be moved to a better place, refactored or even just deleted if no longer useful
    public class NexusSimulator
    {
        public Nexus Nexus { get; private set; }
        public DateTime CurrentTime;

        private Random _rnd;
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

        public TimeSpan blockTimeSkip = TimeSpan.FromMinutes(45);
        public BigInteger MinimumFee = 1;

        public NexusSimulator(KeyPair ownerKey, int seed, Logger logger = null) : this(new Nexus(null, null, (n) => new OracleSimulator(n)), ownerKey, seed, logger)
        {

        }
       
        public NexusSimulator(Nexus nexus, KeyPair ownerKey, int seed, Logger logger = null)
        {
            this.Logger = logger != null ? logger : new DummyLogger();

            _owner = ownerKey;
            this.Nexus = nexus;

            CurrentTime = new DateTime(2018, 8, 26);

            if (!Nexus.Ready)
            {
                if (!Nexus.CreateGenesisBlock("simnet", _owner, CurrentTime))
                {
                    throw new ChainException("Genesis block failure");
                }
            }

            this.bankChain = Nexus.FindChainByName("bank");

            _rnd = new Random(seed);
            _keys.Add(_owner);

            var oneFuel = UnitConversion.ToBigInteger(1, Nexus.FuelTokenDecimals);
            var localBalance = Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, _owner.Address);

            if (localBalance < oneFuel)
            {
                throw new Exception("Funds missing oops");
            }

            var neoPlatform = Pay.Chains.NeoWallet.NeoPlatform;
            var neoKeys = InteropUtils.GenerateInteropKeys(_owner, neoPlatform);
            var neoText = Phantasma.Neo.Core.NeoKey.FromWIF(neoKeys.ToWIF()).address;
            var neoAddress = Phantasma.Pay.Chains.NeoWallet.EncodeAddress(neoText);

            var ethPlatform = Pay.Chains.EthereumWallet.EthereumPlatform;
            var ethKeys = InteropUtils.GenerateInteropKeys(_owner, ethPlatform);
            var ethText = Phantasma.Ethereum.EthereumKey.FromWIF(ethKeys.ToWIF()).address;
            var ethAddress = Phantasma.Pay.Chains.EthereumWallet.EncodeAddress(ethText);

            BeginBlock();
            GenerateAppRegistration(_owner, "mystore", "https://my.store", "The future of digital content distribution!");

            GenerateCustomTransaction(_owner, 0, () => new ScriptBuilder().AllowGas(_owner.Address, Address.Null, MinimumFee, 9999).
                CallContract("nexus", "CreatePlatform", neoAddress, "GAS").
                CallContract("nexus", "CreatePlatform", ethAddress, "ETH").
            SpendGas(_owner.Address).EndScript());

            GenerateToken(_owner, "NEO", "NEO", neoPlatform, Hash.FromUnpaddedHex("c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b"), UnitConversion.ToBigInteger(100000000, 0), 0, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.External);
            GenerateToken(_owner, "GAS", "GAS", neoPlatform, Hash.FromUnpaddedHex("602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7"), UnitConversion.ToBigInteger(100000000, 8), 8, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Finite | TokenFlags.External);
            GenerateToken(_owner, "ETH", "Ethereum", ethPlatform, Hash.FromString("ETH"), UnitConversion.ToBigInteger(0, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.External);
            GenerateToken(_owner, "DAI", "Dai Stablecoin", ethPlatform, Hash.FromUnpaddedHex("89d24a6b4ccb1b6faa2625fe562bdd9a23260359"), UnitConversion.ToBigInteger(0, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.External);
            //GenerateToken(_owner, "EOS", "EOS", "EOS", UnitConversion.ToBigInteger(1006245120, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.External);

            EndBlock();

            /*
            var market = Nexus.FindChainByName("market");
            var nftSales = new List<KeyValuePair<KeyPair, BigInteger>>();

            BeginBlock();
            for (int i = 1; i < 7; i++)
            {
                BigInteger ID = i + 100;
                TokenContent info;
                try
                {
                    info = Nexus.GetNFT(nachoSymbol, ID);
                }
                catch  
                {
                    continue;
                }

                var chain = Nexus.FindChainByAddress(info.CurrentChain);
                if (chain == null)
                {
                    continue;
                }

                var nftOwner = chain.GetTokenOwner(nachoSymbol, ID);

                if (nftOwner == Address.Null)
                {
                    continue;
                }

                foreach (var key in _keys)
                {
                    if (key.Address == nftOwner)
                    {
                        nftSales.Add(new KeyValuePair<KeyPair, BigInteger>(key, ID));
                        // send some gas to the sellers
                        GenerateTransfer(_owner, key.Address, Nexus.RootChain, Nexus.FuelTokenSymbol, UnitConversion.ToBigInteger(0.01m, Nexus.FuelTokenDecimals));
                    }
                }
            }

            EndBlock();

            BeginBlock();
            foreach (var sale in nftSales)
            {
                // TODO this later should be the market chain instead of root
                GenerateNftSale(sale.Key, Nexus.RootChain, nachoSymbol, sale.Value, UnitConversion.ToBigInteger(100 + 5 * _rnd.Next() % 50, Nexus.FuelTokenDecimals));
            }
            EndBlock();
            */
        }

        private void RandomSpreadNFT(string tokenSymbol, int amount)
        {
            Throw.If(!Nexus.TokenExists(tokenSymbol), "Token does not exist: "+tokenSymbol);
            var tokenInfo = Nexus.GetTokenInfo(tokenSymbol);
            Throw.If(tokenInfo.IsFungible, "expected NFT");

            for (int i = 1; i < amount; i++)
            {
                var nftKey = KeyPair.Generate();
                _keys.Add(nftKey);
                var data = new SimNFTData() { A = (byte)_rnd.Next(), B = (byte)_rnd.Next(), C = (byte)_rnd.Next() };
                MintNonFungibleToken(_owner, nftKey.Address, tokenSymbol, Serialization.Serialize(data), new byte[0], 0);
            }
        }

        private List<Transaction> transactions = new List<Transaction>();

        // there are more elegant ways of doing this...
        private Dictionary<Hash, Chain> txChainMap = new Dictionary<Hash, Chain>();
        private Dictionary<Hash, Transaction> txHashMap = new Dictionary<Hash, Transaction>();

        private HashSet<Address> pendingNames = new HashSet<Address>();

        private bool blockOpen = false;
        private KeyPair blockValidator;

        public void BeginBlock()
        {
            BeginBlock(_owner);
        }

        public void BeginBlock(KeyPair validator)
        {
            if (blockOpen)
            {
                throw new Exception("Simulator block not terminated");
            }

            this.blockValidator = validator;

            transactions.Clear();
            txChainMap.Clear();
            txHashMap.Clear();

            var readyNames = new List<Address>();
            foreach (var address in pendingNames)
            {
                var currentName = Nexus.LookUpAddressName(address);
                if (currentName != ValidationUtils.ANONYMOUS)
                {
                    readyNames.Add(address);
                }
            }
            foreach (var address in readyNames)
            {
                pendingNames.Remove(address);
            }

            blockOpen = true;

            step++;
            Logger.Message($"Begin block #{step}");
        }

        public void CancelBlock()
        {
            if (!blockOpen)
            {
                throw new Exception("Simulator block not started");
            }

            blockOpen = false;
            Logger.Message($"Cancel block #{step}");
            step--;
        }

        public IEnumerable<Block> EndBlock(Mempool mempool = null)
        {
            if (!blockOpen)
            {
                throw new Exception("Simulator block not open");
            }

            usedAddresses.Clear();

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

                        string reason = "unknown";

                        if (mempool != null)
                        {
                            submitted = true;
                            foreach (var tx in txs)
                            {
                                try
                                {
                                    mempool.Submit(tx);
                                }
                                catch (Exception e)
                                {
                                    reason = e.Message;
                                    submitted = false;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            try
                            {
                                chain.BakeBlock(ref block, ref txs, MinimumFee, blockValidator, CurrentTime);
                                chain.AddBlock(block, txs, MinimumFee);
                                submitted = true;
                            }
                            catch (Exception e)
                            {
                                reason = e.Message;
                                submitted = false;
                            }
                        }

                        if (submitted)
                        {
                            blocks.Add(block);

                            CurrentTime += blockTimeSkip;

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
                                        tokenSymbol = entry.tokenSymbol
                                    };

                                    _pendingBlocks.Add(pendingBlock);
                                    Logger.Debug($"...Sending {entry.sourceChain.Name}=>{entry.destChain.Name}: {block.Hash}");
                                }
                            }

                            Logger.Message($"End block #{step} @ {chain.Name} chain: {block.Hash}");
                        }
                        else
                        {
                            throw new ChainException($"add block @ {chain.Name} failed, reason: {reason}");
                        }
                    }
                }

                _pendingEntries.Clear();
                return blocks;
            }

            return Enumerable.Empty<Block>();
        }

        private Transaction MakeTransaction(IEnumerable<KeyPair> signees, ProofOfWork pow, Chain chain, byte[] script)
        {
            if (!blockOpen)
            {
                throw new Exception("Call BeginBlock first");
            }

            var tx = new Transaction(Nexus.Name, chain.Name, script, CurrentTime + TimeSpan.FromSeconds(Mempool.MaxExpirationTimeDifferenceInSeconds / 2));

            Throw.If(!signees.Any(), "at least one signer required");

            Signature[] existing = tx.Signatures;
            var msg = tx.ToByteArray(false);

            tx = new Transaction(Nexus.Name, chain.Name, script, CurrentTime + TimeSpan.FromSeconds(Mempool.MaxExpirationTimeDifferenceInSeconds / 2));

            tx.Mine((int)pow);

            foreach (KeyPair kp in signees)
            {
                tx.Sign(kp);
            }

            txChainMap[tx.Hash] = chain;
            txHashMap[tx.Hash] = tx;
            transactions.Add(tx);

            foreach (var signer in signees)
            {
                usedAddresses.Add(signer.Address);
            }

            return tx;
        }

        private Transaction MakeTransaction(KeyPair source, ProofOfWork pow, Chain chain, byte[] script)
        {
            return MakeTransaction(new KeyPair[] { source }, pow, chain, script);
        }

        public Transaction GenerateCustomTransaction(KeyPair owner, ProofOfWork pow, Func<byte[]> scriptGenerator)
        {
            return GenerateCustomTransaction(owner, pow, Nexus.RootChain, scriptGenerator);
        }

        public Transaction GenerateCustomTransaction(KeyPair owner, ProofOfWork pow, Chain chain, Func<byte[]> scriptGenerator)
        {
            var script = scriptGenerator();

            var tx = MakeTransaction(owner, pow, chain, script);
            return tx;
        }

        public Transaction GenerateCustomTransaction(IEnumerable<KeyPair> owners, ProofOfWork pow, Func<byte[]> scriptGenerator)
        {
            return GenerateCustomTransaction(owners, pow, Nexus.RootChain, scriptGenerator);
        }

        public Transaction GenerateCustomTransaction(IEnumerable<KeyPair> owners, ProofOfWork pow, Chain chain, Func<byte[]> scriptGenerator)
        {
            var script = scriptGenerator();
            var tx = MakeTransaction(owners, pow, chain, script);
            return tx;
        }

        public Transaction GenerateToken(KeyPair owner, string symbol, string name, string platform, Hash hash, BigInteger totalSupply, int decimals, TokenFlags flags, byte[] tokenScript = null)
        {
            if (tokenScript == null)
            {
                // small script that restricts minting of tokens to transactions where the owner is a witness
                var addressStr = Base16.Encode(owner.Address.PublicKey);
                var scriptString = new string[] {
                $"alias r1, $triggerMint",
                $"alias r2, $currentTrigger",
                $"alias r3, $result",
                $"alias r4, $owner",

                $@"load $triggerMint, ""{AccountContract.TriggerMint}""",
                $"pop $currentTrigger",

                $"equal $triggerMint, $currentTrigger, $result",
                $"jmpif $result, @mintHandler",
                $"jmp @end",

                $"@mintHandler: nop",
                $"load $owner 0x{addressStr}",
                "push $owner",
                "extcall \"Address()\"",
                "extcall \"Runtime.IsWitness\"",
                "pop $result",
                $"jmpif $result, @end",
                $"throw",

                $"@end: ret"
                };
                tokenScript = AssemblerUtils.BuildScript(scriptString);
            }

            var script = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, MinimumFee, 9999).
                CallContract("nexus", "CreateToken", owner.Address, symbol, name, platform, hash, totalSupply, decimals, flags, tokenScript).
                SpendGas(owner.Address).
                EndScript();

            var tx = MakeTransaction(owner, ProofOfWork.Minimal, Nexus.RootChain, script);

            return tx;
        }

        public Transaction MintTokens(KeyPair owner, Address destination, string symbol, BigInteger amount)
        {
            var chain = Nexus.RootChain;

            var script = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, MinimumFee, 9999).
                CallContract("token", "MintTokens", owner.Address, destination, symbol, amount).
                SpendGas(owner.Address).
                EndScript();

            var tx = MakeTransaction(owner, ProofOfWork.None, chain, script);

            return tx;
        }

        public Transaction GenerateSideChainSend(KeyPair source, string tokenSymbol, Chain sourceChain, Address targetAddress, Chain targetChain, BigInteger amount, BigInteger fee)
        {
            Throw.IfNull(source, nameof(source));
            Throw.If(!Nexus.TokenExists(tokenSymbol), "Token does not exist: "+ tokenSymbol);
            Throw.IfNull(sourceChain, nameof(sourceChain));
            Throw.IfNull(targetChain, nameof(targetChain));
            Throw.If(amount <= 0, "positive amount required");

            if (source.Address == targetAddress && tokenSymbol == Nexus.FuelTokenSymbol)
            {
                Throw.If(fee != 0, "no fees for same address");
            }
            else
            {
                Throw.If(fee <= 0, "fee required when target is different address or token not native");
            }

            var sb = ScriptUtils.
                BeginScript().
                AllowGas(source.Address, Address.Null, MinimumFee, 9999);

            if (targetAddress != source.Address)
            {
                sb.CallContract("token", "SendTokens", targetChain.Address, source.Address, source.Address, Nexus.FuelTokenSymbol, fee);
            }

            var script =
                sb.CallContract("token", "SendTokens", targetChain.Address, source.Address, targetAddress, tokenSymbol, amount).
                SpendGas(source.Address).
                EndScript();

            var tx = MakeTransaction(source, ProofOfWork.None, sourceChain, script);

            _pendingEntries[sourceChain] = new SideChainPendingBlock()
            {
                sourceChain = sourceChain,
                destChain = targetChain,
                hash = null,
                tokenSymbol = tokenSymbol
            };
            return tx;
        }

        public Transaction GenerateSideChainSettlement(KeyPair source, Chain sourceChain, Chain destChain, Hash targetHash)
        {
            _pendingBlocks.RemoveAll(x => x.hash == targetHash);

            var script = ScriptUtils.
                BeginScript().
                CallContract("token", "SettleBlock", sourceChain.Address, targetHash).
                AllowGas(source.Address, Address.Null, MinimumFee, 9999).
                SpendGas(source.Address).
                EndScript();
            var tx = MakeTransaction(source, ProofOfWork.None, destChain, script);
            return tx;
        }

        public Transaction GenerateAccountRegistration(KeyPair source, string name)
        {
            var sourceChain = this.Nexus.RootChain;
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, 9999).CallContract("account", "RegisterName", source.Address, name).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, ProofOfWork.Minimal, sourceChain, script);

            pendingNames.Add(source.Address);
            return tx;
        }

        public Transaction GenerateChain(KeyPair source, Chain parentchain, string name, params string[] contracts)
        {
            Throw.IfNull(parentchain, nameof(parentchain));

            var script = ScriptUtils.BeginScript().
                AllowGas(source.Address, Address.Null, MinimumFee, 9999).
                CallContract("nexus", "CreateChain", source.Address, name, parentchain.Name, contracts).
                SpendGas(source.Address).
                EndScript();
            var tx = MakeTransaction(source, ProofOfWork.Minimal, Nexus.RootChain, script);
            return tx;
        }

        public Transaction GenerateTransfer(KeyPair source, Address dest, Chain chain, string tokenSymbol, BigInteger amount, List<KeyPair> signees = null)
        {
            signees = signees ?? new List<KeyPair>();
            var found = false;
            foreach (var signer in signees)
            {
                if (signer.Address == source.Address)
                {
                    found = true;
                }
            }

            if (!found)
            {
                signees.Add(source);
            }

            var script = ScriptUtils.BeginScript().
                AllowGas(source.Address, Address.Null, MinimumFee, 9999).
                CallContract("token", "TransferTokens", source.Address, dest, tokenSymbol, amount).
                SpendGas(source.Address).
                EndScript();

            var tx = MakeTransaction(signees, ProofOfWork.None, chain, script);
            return tx;
        }

        public Transaction GenerateLoanTransfer(KeyPair source, Address dest, Chain chain, string tokenSymbol, BigInteger amount, List<KeyPair> signees = null)
        {
            signees = signees ?? new List<KeyPair>();
            var found = false;
            foreach (var signer in signees)
            {
                if (signer.Address == source.Address)
                {
                    found = true;
                }
            }

            if (!found)
            {
                signees.Add(source);
            }

            var script = ScriptUtils.BeginScript().
                LoanGas(source.Address, MinimumFee, 9999).
                AllowGas(source.Address, Address.Null, MinimumFee, 9999).
                CallContract("token", "TransferTokens", source.Address, dest, tokenSymbol, amount).
                SpendGas(source.Address).
                EndScript();

            var tx = MakeTransaction(signees, ProofOfWork.Moderate, chain, script);
            return tx;
        }

        public Transaction GenerateSwap(KeyPair source, Chain chain, string fromSymbol, string toSymbol, BigInteger amount)
        {
            var script = ScriptUtils.BeginScript().
                CallContract("swap", "SwapTokens", source.Address, fromSymbol, toSymbol, amount).
                AllowGas(source.Address, Address.Null, MinimumFee, 9999).
                SpendGas(source.Address).
                EndScript();
            var tx = MakeTransaction(source, ProofOfWork.None, chain, script);
            return tx;
        }

        public Transaction GenerateNftTransfer(KeyPair source, Address dest, Chain chain, string tokenSymbol, BigInteger tokenId)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, 9999).CallContract("token", "TransferToken", source.Address, dest, tokenSymbol, tokenId).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, ProofOfWork.None, chain, script);
            return tx;
        }

        public Transaction GenerateNftSidechainTransfer(KeyPair source, Address destAddress, Chain sourceChain,
            Chain destChain, string tokenSymbol, BigInteger tokenId)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, 9999).CallContract("token", "SendToken", destChain.Address, source.Address, destAddress, tokenSymbol, tokenId).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, ProofOfWork.None, sourceChain, script);
            return tx;
        }

        public Transaction GenerateNftBurn(KeyPair source, Chain chain, string tokenSymbol, BigInteger tokenId)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, 9999).CallContract("token", "BurnToken", source.Address, tokenSymbol, tokenId).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, ProofOfWork.None, chain, script);
            return tx;
        }

        public Transaction GenerateNftSale(KeyPair source, Chain chain, string tokenSymbol, BigInteger tokenId, BigInteger price)
        {
            Timestamp endDate = this.CurrentTime + TimeSpan.FromDays(5);
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, 9999).CallContract("market", "SellToken", source.Address, tokenSymbol, Nexus.FuelTokenSymbol, tokenId, price, endDate).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, ProofOfWork.None, chain, script);
            return tx;
        }

        public Transaction MintNonFungibleToken(KeyPair source, Address destAddress, string tokenSymbol, byte[] rom, byte[] ram, BigInteger value)
        {
            var chain = Nexus.RootChain;
            var script = ScriptUtils.
                BeginScript().
                AllowGas(source.Address, Address.Null, MinimumFee, 9999).
                CallContract("token", "MintToken", source.Address, destAddress, tokenSymbol, rom, ram, value).
                SpendGas(source.Address).
                EndScript();

            var tx = MakeTransaction(source, ProofOfWork.None, chain, script);
            return tx;
        }

        public Transaction GenerateAppRegistration(KeyPair source, string name, string url, string description)
        {
            var contract = "apps";

            var chain = Nexus.RootChain;
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, 9999).CallContract(contract, "RegisterApp", source.Address, name).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, ProofOfWork.None, chain, script);

            script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, 9999).CallContract(contract, "SetAppUrl", name, url).SpendGas(source.Address).EndScript();
            tx = MakeTransaction(source, ProofOfWork.None, chain, script);

            script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, 9999).CallContract(contract, "SetAppDescription", name, description).SpendGas(source.Address).EndScript();
            tx = MakeTransaction(source, ProofOfWork.None, chain, script);

            return tx;
        }

        public Transaction GenerateSetTokenMetadata(KeyPair source, string tokenSymbol, string key, string value)
        {
            var chain = Nexus.RootChain;
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, 9999).CallContract("token", "SetMetadata", source.Address, tokenSymbol, key, value).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, ProofOfWork.None, chain, script);

            return tx;
        }

        private int step;
        private HashSet<Address> usedAddresses = new HashSet<Address>();

        public void GenerateRandomBlock(Mempool mempool = null)
        {
            //Console.WriteLine("begin block #" + Nexus.RootChain.BlockHeight);
            BeginBlock();

            int transferCount = 1 + _rnd.Next() % 10;
            int tries = 0;
            while (tries < 10000)
            {
                if (transactions.Count >= transferCount)
                {
                    break;
                }

                tries++;
                var source = _keys[_rnd.Next() % _keys.Count];

                if (usedAddresses.Contains(source.Address))
                {
                    continue;
                }

                var prevTxCount = transactions.Count;

                var sourceChain = Nexus.RootChain;
                var fee = 9999;

                string tokenSymbol;

                switch (_rnd.Next() % 4)
                {
                    case 1: tokenSymbol = Nexus.FiatTokenSymbol; break;
                    //case 2: token = Nexus.FuelTokenSymbol; break;
                    default: tokenSymbol = Nexus.StakingTokenSymbol; break;
                }

                switch (_rnd.Next() % 7)
                {
                    // side-chain send
                    case 1:
                        {
                            var sourceChainList = Nexus.Chains.ToArray();
                            sourceChain = Nexus.FindChainByName( sourceChainList[_rnd.Next() % sourceChainList.Length]);

                            var targetChainList = Nexus.Chains.Select(x => Nexus.FindChainByName(x)).Where(x => Nexus.GetParentChainByName(x.Name) == sourceChain.Name || Nexus.GetParentChainByName(sourceChain.Name) == x.Name).ToArray();
                            var targetChain = targetChainList[_rnd.Next() % targetChainList.Length];

                            var total = UnitConversion.ToBigInteger(1 + _rnd.Next() % 100, Nexus.FuelTokenDecimals);

                            var tokenBalance = sourceChain.GetTokenBalance(tokenSymbol, source.Address);
                            var fuelBalance = sourceChain.GetTokenBalance(Nexus.FuelTokenSymbol, source.Address);

                            var expectedTotal = total;
                            if (tokenSymbol == Nexus.FuelTokenSymbol)
                            {
                                expectedTotal += fee;
                            }

                            var sideFee = 0;
                            if (tokenSymbol != Nexus.FuelTokenSymbol)
                            {
                                sideFee = fee;
                            }

                            if (tokenBalance > expectedTotal && fuelBalance > fee + sideFee)
                            {
                                Logger.Debug($"Rnd.SideChainSend: {total} {tokenSymbol} from {source.Address}");
                                GenerateSideChainSend(source, tokenSymbol, sourceChain, source.Address, targetChain, total, sideFee);
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

                                    var balance = pendingBlock.destChain.GetTokenBalance(pendingBlock.tokenSymbol, source.Address);
                                    if (balance > 0)
                                    {
                                        Logger.Message($"...Settling {pendingBlock.sourceChain.Name}=>{pendingBlock.destChain.Name}: {pendingBlock.hash}");
                                        GenerateSideChainSettlement(source, pendingBlock.sourceChain, pendingBlock.destChain, pendingBlock.hash);
                                    }
                                }
                            }

                            break;
                        }

                        /*
                    // stable claim
                    case 3:
                        {
                            sourceChain = bankChain;
                            tokenSymbol = Nexus.FuelTokenSymbol;

                            var balance = sourceChain.GetTokenBalance(tokenSymbol, source.Address);

                            var total = UnitConversion.ToBigInteger(1 + _rnd.Next() % 100, Nexus.FuelTokenDecimals - 1);

                            if (balance > total + fee)
                            {
                                Logger.Debug($"Rnd.StableClaim: {total} {tokenSymbol} from {source.Address}");
                                GenerateStableClaim(source, sourceChain, total);
                            }

                            break;
                        }

                    // stable redeem
                    case 4:
                        {
                            sourceChain = bankChain;
                            tokenSymbol = Nexus.FiatTokenSymbol;

                            var tokenBalance = sourceChain.GetTokenBalance(tokenSymbol, source.Address);
                            var fuelBalance = sourceChain.GetTokenBalance(Nexus.FuelTokenSymbol, source.Address);

                            var rate = (BigInteger) bankChain.InvokeContract("bank", "GetRate", Nexus.FuelTokenSymbol);
                            var total = tokenBalance / 10;
                            if (total >= rate && fuelBalance > fee)
                            {
                                Logger.Debug($"Rnd.StableRedeem: {total} {tokenSymbol} from {source.Address}");
                                GenerateStableRedeem(source, sourceChain, total);
                            }

                            break;
                        }*/

                    // name register
                    case 5:
                        {
                            sourceChain = this.Nexus.RootChain;
                            tokenSymbol = Nexus.FuelTokenSymbol;

                            var balance = sourceChain.GetTokenBalance(tokenSymbol, source.Address);
                            if (balance > fee + AccountContract.RegistrationCost && !pendingNames.Contains(source.Address))
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
                                        randomName += (100 + _rnd.Next() % 900).ToString();
                                        break;
                                }

                                var currentName = Nexus.LookUpAddressName(source.Address);
                                if (currentName == ValidationUtils.ANONYMOUS)
                                {
                                    var lookup = Nexus.LookUpName(randomName);
                                    if (lookup.IsNull)
                                    {
                                        Logger.Debug($"Rnd.GenerateAccount: {source.Address} => {randomName}");
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

                            if ((_keys.Count < 2 || temp == 0) && _keys.Count < 2000)
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
                                var total = UnitConversion.ToBigInteger(1 + _rnd.Next() % 100, Nexus.FuelTokenDecimals - 1);

                                var tokenBalance = sourceChain.GetTokenBalance(tokenSymbol, source.Address);
                                var fuelBalance = sourceChain.GetTokenBalance(Nexus.FuelTokenSymbol, source.Address);

                                var expectedTotal = total;
                                if (tokenSymbol == Nexus.FuelTokenSymbol)
                                {
                                    expectedTotal += fee;
                                }

                                if (tokenBalance > expectedTotal && fuelBalance > fee)
                                {
                                    Logger.Debug($"Rnd.Transfer: {total} {tokenSymbol} from {source.Address} to {targetAddress}");
                                    GenerateTransfer(source, targetAddress, sourceChain, tokenSymbol, total);
                                }
                            }
                            break;
                        }
                }
            }

            if (transactions.Count > 0)
            {
                EndBlock(mempool);
            }
            else{
                CancelBlock();
            }
        }

        public void TimeSkipYears(int years)
        {
            CurrentTime = CurrentTime.AddYears(years);

            BeginBlock();
            var tx = GenerateCustomTransaction(_owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(_owner.Address, Address.Null, MinimumFee, 9999)
                    .CallContract(Nexus.StakeContractName, "GetUnclaimed", _owner.Address).
                    SpendGas(_owner.Address).EndScript());
            EndBlock();

            var txCost = Nexus.RootChain.GetTransactionFee(tx);
        }

        public void TimeSkipToDate(DateTime date)
        {
            CurrentTime = date;

            BeginBlock();
            var tx = GenerateCustomTransaction(_owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(_owner.Address, Address.Null, MinimumFee, 9999)
                    .CallContract(Nexus.StakeContractName, "GetUnclaimed", _owner.Address).
                    SpendGas(_owner.Address).EndScript());
            EndBlock();
        }

        public void TimeSkipDays(double days, bool roundUp = false)
        {
            CurrentTime = CurrentTime.AddDays(days);

            if (roundUp)
            {
                CurrentTime = CurrentTime.AddDays(1);
                CurrentTime = new DateTime(CurrentTime.Year, CurrentTime.Month, CurrentTime.Day);

                var timestamp = (Timestamp) CurrentTime;
                var datetime = (DateTime) timestamp;
                if (datetime.Hour == 23)
                    datetime = datetime.AddHours(2);
                
                CurrentTime = new DateTime(datetime.Year, datetime.Month, datetime.Day, datetime.Hour, 0 , 0);   //to set the time of day component to 0
            }

            BeginBlock();
            var tx = GenerateCustomTransaction(_owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(_owner.Address, Address.Null, MinimumFee, 9999)
                    .CallContract(Nexus.StakeContractName, "GetUnclaimed", _owner.Address).
                    SpendGas(_owner.Address).EndScript());
            EndBlock();

            var txCost = Nexus.RootChain.GetTransactionFee(tx);
            
        }
    }

}
