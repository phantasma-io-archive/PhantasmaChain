using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.IO;
using Phantasma.Numerics;
using Phantasma.VM.Utils;

namespace Phantasma.Blockchain.Utils
{
    public class SideChainPendingBlock
    {
        public Hash hash;
        public Chain sourceChain;
        public Chain destChain;
        public Token token;
    }

    public struct SimNFTData
    {
        public byte A;
        public byte B;
        public byte C;
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

        public ChainSimulator(KeyPair ownerKey, int seed, int cacheSize, Logger logger = null)
        {
            this.Logger = logger != null ? logger : new DummyLogger();

            _owner = ownerKey;
            this.Nexus = new Nexus("simnet", ownerKey.Address, cacheSize);

            CurrentTime = new DateTime(2018, 8, 26);

            if (!Nexus.CreateGenesisBlock(_owner, CurrentTime))
            {
                throw new ChainException("Genesis block failure");
            }

            this.bankChain = Nexus.FindChainByName("bank");

            _rnd = new System.Random(seed);
            _keys.Add(_owner);

            var oneFuel = UnitConversion.ToBigInteger(1, Nexus.FuelTokenDecimals);
            var localBalance = Nexus.RootChain.GetTokenBalance(Nexus.FuelToken, _owner.Address);

            if (localBalance < oneFuel)
            {
                throw new Exception("Funds missing oops");
            }

            var nachoAddress = Address.FromText("PGasVpbFYdu7qERihCsR22nTDQp1JwVAjfuJ38T8NtrCB");
            var nachoFuel = UnitConversion.ToBigInteger(5, Nexus.FuelTokenDecimals);
            var nachoChain = Nexus.FindChainByName("nacho");

            var appsChain = Nexus.FindChainByName("apps");

            BeginBlock();
            GenerateSideChainSend(_owner, Nexus.FuelToken, Nexus.RootChain, _owner.Address, appsChain, oneFuel, 0);
            GenerateSideChainSend(_owner, Nexus.FuelToken, Nexus.RootChain, nachoAddress, nachoChain, nachoFuel, 9999);
            var blockTx = EndBlock().First();

            BeginBlock();
            GenerateSideChainSettlement(_owner, Nexus.RootChain, appsChain, blockTx.Hash);
            GenerateSideChainSettlement(_owner, Nexus.RootChain, nachoChain, blockTx.Hash);
            EndBlock();

            BeginBlock();
            GenerateChain(_owner, Nexus.RootChain, "dex");
            GenerateChain(_owner, Nexus.RootChain, "market");
            EndBlock();

            BeginBlock();
            GenerateAppRegistration(_owner, "nachomen", "https://nacho.men", "Collect, train and battle against other players in Nacho Men!");
            GenerateAppRegistration(_owner, "mystore", "https://my.store", "The future of digital content distribution!");
            GenerateAppRegistration(_owner, "nftbazar", "https://nft.bazar", "A decentralized NFT market");
            GenerateToken(_owner, "NACHO", "Nachomen", 0, 0, TokenFlags.Transferable);
            GenerateToken(_owner, "LUCHA", "Nachomen Luchador", 0, 0, TokenFlags.Transferable);
            GenerateToken(_owner, "ITEM", "Nachomen Item", 0, 0, TokenFlags.Transferable);
            EndBlock();

            var market = Nexus.FindChainByName("market");
            BeginBlock();

            var nacho = Nexus.FindTokenBySymbol("NACHO");
            RandomSpreadNFT(nacho, 150);

            GenerateSetTokenMetadata(_owner, nacho, "details", "https://nacho.men/luchador/*");
            GenerateSetTokenMetadata(_owner, nacho, "viewer", "https://nacho.men/luchador/body/*");
            EndBlock();

            var nftSales = new List<KeyValuePair<KeyPair, BigInteger>>();
            BeginBlock();
            for (int i = 1; i < 7; i++)
            {
                BigInteger ID = i + 100;
                TokenContent info;
                try
                {
                    info = Nexus.GetNFT(nacho, ID);
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

                var nftOwner = chain.GetTokenOwner(nacho, ID);

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
                        GenerateTransfer(_owner, key.Address, Nexus.RootChain, Nexus.FuelToken, UnitConversion.ToBigInteger(0.01m, Nexus.FuelTokenDecimals));
                    }
                }
            }

            EndBlock();

            BeginBlock();
            foreach (var sale in nftSales)
            {
                // TODO this later should be the market chain instead of root
                GenerateNftSale(sale.Key, Nexus.RootChain, nacho, sale.Value, UnitConversion.ToBigInteger(100 + 5 * _rnd.Next() % 50, Nexus.FuelTokenDecimals));
            }
            EndBlock();
        }

        private void RandomSpreadNFT(Token token, int amount)
        {
            Throw.IfNull(token, nameof(token));
            Throw.If(token.IsFungible, "expected NFT");

            for (int i = 1; i < amount; i++)
            {
                var nftKey = KeyPair.Generate();
                _keys.Add(nftKey);
                var data = new SimNFTData() { A = (byte)_rnd.Next(), B = (byte)_rnd.Next(), C = (byte)_rnd.Next() };
                GenerateNft(_owner, nftKey.Address, Nexus.RootChain, token, Serialization.Serialize(data), new byte[0]);
            }
        }

        private List<Transaction> transactions = new List<Transaction>();

        // there are more elegant ways of doing this...
        private Dictionary<Hash, Chain> txChainMap = new Dictionary<Hash, Chain>();
        private Dictionary<Hash, Transaction> txHashMap = new Dictionary<Hash, Transaction>();

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

            var readyNames = new List<Address>();
            foreach (var address in pendingNames)
            {
                var currentName = Nexus.LookUpAddress(address);
                if (currentName != AccountContract.ANONYMOUS)
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

                        if (mempool != null)
                        {
                            submitted = true;
                            foreach (var tx in txs)
                            {
                                try
                                {
                                    mempool.Submit(tx);
                                }
                                catch
                                {
                                    submitted = false;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            try
                            {
                                chain.AddBlock(block, txs);
                                submitted = true;
                            }
                            catch (Exception e)
                            {
                                submitted = false;
                            }
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
                                    Logger.Debug($"...Sending {entry.sourceChain.Name}=>{entry.destChain.Name}: {block.Hash}");
                                }
                            }

                            Logger.Message($"End block #{step} @ {chain.Name} chain: {block.Hash}");
                        }
                        else
                        {
                            throw new Exception($"add block @ {chain.Name} failed");
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
            var tx = new Transaction(Nexus.Name, chain.Name, script, CurrentTime + TimeSpan.FromSeconds(Mempool.MaxExpirationTimeDifferenceInSeconds / 2));

            if (source != null)
            {
                tx.Sign(source);
            }

            txChainMap[tx.Hash] = chain;
            txHashMap[tx.Hash] = tx;
            transactions.Add(tx);

            usedAddresses.Add(source.Address);

            return tx;
        }

        public Transaction GenerateCustomTransaction(KeyPair owner, Func<byte[]> scriptGenerator)
        {
            var chain = Nexus.RootChain;

            var script = scriptGenerator();

            var tx = MakeTransaction(owner, chain, script);
            tx.Sign(owner);

            return tx;
        }

        public Transaction GenerateToken(KeyPair owner, string symbol, string name, BigInteger totalSupply, int decimals, TokenFlags flags)
        {
            var chain = Nexus.RootChain;

            var script = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, 1, 9999).
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
            Throw.If(amount <= 0, "positive amount required");

            if (source.Address == targetAddress && token == Nexus.FuelToken)
            {
                Throw.If(fee != 0, "no fees for same address");
            }
            else
            {
                Throw.If(fee <= 0, "fee required when target is different address or token not native");
            }

            var sb = ScriptUtils.
                BeginScript().
                AllowGas(source.Address, Address.Null, 1, 9999);

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
                AllowGas(source.Address, Address.Null, 1, 9999).
                SpendGas(source.Address).
                EndScript();
            var tx = MakeTransaction(source, destChain, script);
            return tx;
        }

        public Transaction GenerateStableClaim(KeyPair source, Chain sourceChain, BigInteger amount)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, 1, 9999).CallContract("bank", "Claim", source.Address, amount).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, sourceChain, script);
            tx.Sign(source);
            return tx;
        }

        public Transaction GenerateStableRedeem(KeyPair source, Chain sourceChain, BigInteger amount)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, 1, 9999).CallContract("bank", "Redeem", source.Address, amount).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, sourceChain, script);
            return tx;
        }

        public Transaction GenerateAccountRegistration(KeyPair source, string name)
        {
            var sourceChain = this.Nexus.RootChain;
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, 1, 9999).CallContract("account", "Register", source.Address, name).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, sourceChain, script);

            pendingNames.Add(source.Address);
            return tx;
        }

        public Transaction GenerateChain(KeyPair source, Chain parentchain, string name)
        {
            var script = ScriptUtils.BeginScript().
                AllowGas(source.Address, Address.Null, 1, 9999).
                CallContract("nexus", "CreateChain", source.Address, name, parentchain.Name).
                SpendGas(source.Address).
                EndScript();
            var tx = MakeTransaction(source, Nexus.RootChain, script);
            return tx;
        }

        public Transaction GenerateTransfer(KeyPair source, Address dest, Chain chain, Token token, BigInteger amount)
        {
            var script = ScriptUtils.BeginScript().
                AllowGas(source.Address, Address.Null, 1, 9999).
                CallContract("token", "TransferTokens", source.Address, dest, token.Symbol, amount).
                SpendGas(source.Address).
                EndScript();
            var tx = MakeTransaction(source, chain, script);
            return tx;
        }

        public Transaction GenerateNftTransfer(KeyPair source, Address dest, Chain chain, Token token, BigInteger tokenId)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, 1, 9999).CallContract("token", "TransferToken", source.Address, dest, token.Symbol, tokenId).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, chain, script);
            return tx;
        }

        public Transaction GenerateNftSidechainTransfer(KeyPair source, Address destAddress, Chain sourceChain,
            Chain destChain, Token token, BigInteger tokenId)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, 1, 9999).CallContract("token", "SendToken", destChain.Address, source.Address, destAddress, token.Symbol, tokenId).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, sourceChain, script);
            return tx;
        }

        public Transaction GenerateNftBurn(KeyPair source, Chain chain, Token token, BigInteger tokenId)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, 1, 9999).CallContract("token", "BurnToken", source.Address, token.Symbol, tokenId).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, chain, script);
            return tx;
        }

        public Transaction GenerateNftSale(KeyPair source, Chain chain, Token token, BigInteger tokenId, BigInteger price)
        {
            Timestamp endDate = this.CurrentTime + TimeSpan.FromDays(5);
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, 1, 9999).CallContract("market", "SellToken", source.Address, token.Symbol, Nexus.FuelTokenSymbol, tokenId, price, endDate).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, chain, script);
            return tx;
        }

        public Transaction GenerateNft(KeyPair source, Address destAddress, Chain chain, Token token, byte[] rom, byte[] ram)
        {
            var script = ScriptUtils.
                BeginScript().
                AllowGas(source.Address, Address.Null, 1, 9999).
                CallContract("token", "MintToken", destAddress, token.Symbol, rom, ram).
                SpendGas(source.Address).
                EndScript();

            var tx = MakeTransaction(source, chain, script);
            return tx;
        }

        public Transaction GenerateAppRegistration(KeyPair source, string name, string url, string description)
        {
            var contract = "apps";

            var chain = Nexus.FindChainByName("apps");
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, 1, 9999).CallContract(contract, "RegisterApp", source.Address, name).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, chain, script);

            script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, 1, 9999).CallContract(contract, "SetAppUrl", name, url).SpendGas(source.Address).EndScript();
            tx = MakeTransaction(source, chain, script);

            script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, 1, 9999).CallContract(contract, "SetAppDescription", name, description).SpendGas(source.Address).EndScript();
            tx = MakeTransaction(source, chain, script);

            return tx;
        }

        public Transaction GenerateSetTokenMetadata(KeyPair source, Token token, string key, string value)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);
            return GenerateSetTokenMetadata(source, token, key, bytes);
        }

        public Transaction GenerateSetTokenMetadata(KeyPair source, Token token, string key, byte[] value)
        {
            var chain = Nexus.RootChain;
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, 1, 9999).CallContract("nexus", "SetTokenMetadata", token.Symbol, key, value).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, chain, script);

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

                Token token;

                switch (_rnd.Next() % 4)
                {
                    case 1: token = Nexus.StableToken; break;
                    //case 2: token = Nexus.FuelToken; break;
                    default: token = Nexus.StakingToken; break;
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

                            var total = UnitConversion.ToBigInteger(1 + _rnd.Next() % 100, Nexus.FuelTokenDecimals);

                            var tokenBalance = sourceChain.GetTokenBalance(token, source.Address);
                            var fuelBalance = sourceChain.GetTokenBalance(Nexus.FuelToken, source.Address);

                            var expectedTotal = total;
                            if (token == Nexus.FuelToken)
                            {
                                expectedTotal += fee;
                            }

                            var sideFee = 0;
                            if (token != Nexus.FuelToken)
                            {
                                sideFee = fee;
                            }

                            if (tokenBalance > expectedTotal && fuelBalance > fee + sideFee)
                            {
                                Logger.Debug($"Rnd.SideChainSend: {total} {token.Symbol} from {source.Address}");
                                GenerateSideChainSend(source, token, sourceChain, source.Address, targetChain, total, sideFee);
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
                            token = Nexus.FuelToken;

                            var balance = sourceChain.GetTokenBalance(token, source.Address);

                            var total = UnitConversion.ToBigInteger(1 + _rnd.Next() % 100, Nexus.FuelTokenDecimals - 1);

                            if (balance > total + fee)
                            {
                                Logger.Debug($"Rnd.StableClaim: {total} {token.Symbol} from {source.Address}");
                                GenerateStableClaim(source, sourceChain, total);
                            }

                            break;
                        }

                    // stable redeem
                    case 4:
                        {
                            sourceChain = bankChain;
                            token = Nexus.StableToken;

                            var tokenBalance = sourceChain.GetTokenBalance(token, source.Address);
                            var fuelBalance = sourceChain.GetTokenBalance(Nexus.FuelToken, source.Address);

                            var bankContract = bankChain.FindContract<BankContract>("bank");
                            var rate = bankContract.GetRate(Nexus.FuelTokenSymbol);
                            var total = tokenBalance / 10;
                            if (total >= rate && fuelBalance > fee)
                            {
                                Logger.Debug($"Rnd.StableRedeem: {total} {token.Symbol} from {source.Address}");
                                GenerateStableRedeem(source, sourceChain, total);
                            }

                            break;
                        }

                    // name register
                    case 5:
                        {
                            sourceChain = this.Nexus.RootChain;
                            token = Nexus.FuelToken;

                            var balance = sourceChain.GetTokenBalance(token, source.Address);
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

                                var currentName = Nexus.LookUpAddress(source.Address);
                                if (currentName == AccountContract.ANONYMOUS)
                                {
                                    var lookup = Nexus.LookUpName(randomName);
                                    if (lookup == Address.Null)
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

                                var tokenBalance = sourceChain.GetTokenBalance(token, source.Address);
                                var fuelBalance = sourceChain.GetTokenBalance(Nexus.FuelToken, source.Address);

                                var expectedTotal = total;
                                if (token == Nexus.FuelToken)
                                {
                                    expectedTotal += fee;
                                }

                                if (tokenBalance > expectedTotal && fuelBalance > fee)
                                {
                                    Logger.Debug($"Rnd.Transfer: {total} {token.Symbol} from {source.Address} to {targetAddress}");
                                    GenerateTransfer(source, targetAddress, sourceChain, token, total);
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
            var tx = GenerateCustomTransaction(_owner, () =>
                ScriptUtils.BeginScript().AllowGas(_owner.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "GetUnclaimed", _owner.Address).
                    SpendGas(_owner.Address).EndScript());
            EndBlock();

            var txCost = Nexus.RootChain.GetTransactionFee(tx);
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
            var tx = GenerateCustomTransaction(_owner, () =>
                ScriptUtils.BeginScript().AllowGas(_owner.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "GetUnclaimed", _owner.Address).
                    SpendGas(_owner.Address).EndScript());
            EndBlock();

            var txCost = Nexus.RootChain.GetTransactionFee(tx);
            
        }
    }

}
