using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.CodeGen.Assembler;
using Phantasma.Domain;
using Phantasma.VM;
using Phantasma.Blockchain.Tokens;

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

    // TODO this should be moved to a better place, refactored or even just deleted if no longer useful
    public class NexusSimulator
    {
        public Nexus Nexus { get; private set; }
        public DateTime CurrentTime;

        private Random _rnd;
        private List<PhantasmaKeys> _keys = new List<PhantasmaKeys>();
        private PhantasmaKeys _owner;

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

        public readonly Logger Logger;

        public TimeSpan blockTimeSkip = TimeSpan.FromSeconds(2);
        public BigInteger MinimumFee = 1;

        //public NexusSimulator(PhantasmaKeys ownerKey, int seed, Logger logger = null) : this(new Nexus("simnet", null, null), ownerKey, seed, logger)
        //{
        //    this.Nexus.SetOracleReader(new OracleSimulator(this.Nexus));
        //}
       
        public NexusSimulator(Nexus nexus, PhantasmaKeys ownerKey, int seed, Logger logger = null)
        {
            this.Logger = logger != null ? logger : new DummyLogger();

            _owner = ownerKey;
            this.Nexus = nexus;

            CurrentTime = new DateTime(2018, 8, 26, 0, 0, 0, DateTimeKind.Utc);

            if (!Nexus.HasGenesis)
            {
                if (!Nexus.CreateGenesisBlock(_owner, CurrentTime, 5))
                {
                    throw new ChainException("Genesis block failure");
                }
            }
            else
            {
                var lastBlockHash = Nexus.RootChain.GetLastBlockHash();
                var lastBlock = Nexus.RootChain.GetBlockByHash(lastBlockHash);
                CurrentTime = new Timestamp(lastBlock.Timestamp.Value + 1);
                DateTime.SpecifyKind(CurrentTime, DateTimeKind.Utc);
            }

            _rnd = new Random(seed);
            _keys.Add(_owner);

            var oneFuel = UnitConversion.ToBigInteger(1, DomainSettings.FuelTokenDecimals);
            var token = Nexus.GetTokenInfo(Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
            var localBalance = Nexus.RootChain.GetTokenBalance(Nexus.RootStorage, token, _owner.Address);

            if (localBalance < oneFuel)
            {
                throw new Exception("Funds missing oops");
            }

            var neoPlatform = Pay.Chains.NeoWallet.NeoPlatform;
            var neoKeys = InteropUtils.GenerateInteropKeys(_owner, Nexus.GetGenesisHash(Nexus.RootStorage), neoPlatform);
            var neoText = Phantasma.Neo.Core.NeoKeys.FromWIF(neoKeys.ToWIF()).Address;
            var neoAddress = Phantasma.Pay.Chains.NeoWallet.EncodeAddress(neoText);

            var ethPlatform = Pay.Chains.EthereumWallet.EthereumPlatform;
            var ethKeys = InteropUtils.GenerateInteropKeys(_owner, Nexus.GetGenesisHash(Nexus.RootStorage), ethPlatform);
            var ethText = Phantasma.Ethereum.EthereumKey.FromWIF(ethKeys.ToWIF()).Address;
            var ethAddress = Phantasma.Pay.Chains.EthereumWallet.EncodeAddress(ethText);

            // only create all this stuff once
            if (!nexus.PlatformExists(nexus.RootStorage, neoPlatform))
            {
                /*BeginBlock();
                GenerateCustomTransaction(_owner, ProofOfWork.None, () =>
                {
                    return new ScriptBuilder().AllowGas(_owner.Address, Address.Null, 1, 99999).
                    CallContract(NativeContractKind.Governance, nameof(GovernanceContract.SetValue), Nexus.NexusProtocolVersionTag, 3).
                    SpendGas(_owner.Address).
                    EndScript();
                });
                EndBlock();*/

                BeginBlock();
                GenerateCustomTransaction(_owner, 0, () => new ScriptBuilder().AllowGas(_owner.Address, Address.Null, MinimumFee, 9999).
                    CallInterop("Nexus.CreatePlatform", _owner.Address, neoPlatform, neoText, neoAddress, "GAS").
                    CallInterop("Nexus.CreatePlatform", _owner.Address, ethPlatform, ethText, ethAddress, "ETH").
                SpendGas(_owner.Address).EndScript());

                var orgFunding = UnitConversion.ToBigInteger(1863626, DomainSettings.StakingTokenDecimals);
                var orgScript = new byte[0];
                var orgID = DomainSettings.PhantomForceOrganizationName;
                var orgAddress = Address.FromHash(orgID);

                GenerateCustomTransaction(_owner, ProofOfWork.None, () =>
                {
                    return new ScriptBuilder().AllowGas(_owner.Address, Address.Null, 1, 99999).
                    CallInterop("Nexus.CreateOrganization", _owner.Address, orgID, "Phantom Force", orgScript).
                    CallInterop("Organization.AddMember", _owner.Address, orgID, _owner.Address).
                    TransferTokens(DomainSettings.StakingTokenSymbol, _owner.Address, orgAddress, orgFunding).
                    CallContract(NativeContractKind.Swap, nameof(SwapContract.SwapFee), orgAddress, DomainSettings.StakingTokenSymbol, 500000).
                    CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), orgAddress, orgFunding - (5000)).
                    SpendGas(_owner.Address).
                    EndScript();
                });
                EndBlock();

                BeginBlock();
                var communitySupply = 100000;
                GenerateToken(_owner, "MKNI", "Mankini Token", UnitConversion.ToBigInteger(communitySupply, 0), 0, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite);
                MintTokens(_owner, _owner.Address, "MKNI", communitySupply);

                GenerateCustomTransaction(_owner, ProofOfWork.None, () =>
                {
                    return new ScriptBuilder().AllowGas(_owner.Address, Address.Null, 1, 99999).
                    CallContract(NativeContractKind.Sale, nameof(SaleContract.CreateSale), _owner.Address, "Mankini sale", SaleFlags.None, (Timestamp)(this.CurrentTime + TimeSpan.FromHours(5)), (Timestamp)(this.CurrentTime + TimeSpan.FromDays(5)), "MKNI", DomainSettings.StakingTokenSymbol, 7, 0, 1000, 1, 100).
                    SpendGas(_owner.Address).
                    EndScript();
                });

                EndBlock();

                //TODO add SOUL/KCAL on ethereum, removed for now because hash is not fixed yet
                //BeginBlock();
                //GenerateCustomTransaction(_owner, ProofOfWork.Minimal, () =>
                //{
                //    return new ScriptBuilder().AllowGas(_owner.Address, Address.Null, 1, 99999).
                //    CallInterop("Nexus.SetTokenPlatformHash", "SOUL", ethPlatform, Hash.FromUnpaddedHex("53d5bdb2c8797218f8a0e11e997c4ab84f0b40ce")). // eth ropsten testnet hash
                //    CallInterop("Nexus.SetTokenPlatformHash", "KCAL", ethPlatform, Hash.FromUnpaddedHex("67B132A32E7A3c4Ba7dEbedeFf6290351483008f")). // eth ropsten testnet hash
                //    SpendGas(_owner.Address).
                //    EndScript();
                //});
                //EndBlock();
            }

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

        private List<Transaction> transactions = new List<Transaction>();

        // there are more elegant ways of doing this...
        private Dictionary<Hash, Chain> txChainMap = new Dictionary<Hash, Chain>();
        private Dictionary<Hash, Transaction> txHashMap = new Dictionary<Hash, Transaction>();

        private HashSet<Address> pendingNames = new HashSet<Address>();

        private bool blockOpen = false;
        private PhantasmaKeys blockValidator;

        public void BeginBlock()
        {
            BeginBlock(_owner);
        }

        public void BeginBlock(PhantasmaKeys validator)
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
                var currentName = Nexus.RootChain.GetNameFromAddress(Nexus.RootStorage, address);
                if (currentName != ValidationUtils.ANONYMOUS_NAME)
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
            var protocol = (uint)Nexus.GetGovernanceValue(Nexus.RootStorage, Nexus.NexusProtocolVersionTag);

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

                        var lastBlockHash = chain.GetLastBlockHash();
                        var lastBlock = chain.GetBlockByHash(lastBlockHash);
                        BigInteger nextHeight = lastBlock != null ? lastBlock.Height + 1 : Chain.InitialHeight;
                        var prevHash = lastBlock != null ? lastBlock.Hash : Hash.Null;

                        var block = new Block(nextHeight, chain.Address, CurrentTime, hashes, prevHash, protocol, this.blockValidator.Address, System.Text.Encoding.UTF8.GetBytes("SIM"));

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
				                Transaction inflationTx = null;
                                var changeSet = chain.ProcessBlock(block, transactions, MinimumFee, out inflationTx, this.blockValidator);
				                if (inflationTx != null)
				                {
				                    transactions.Add(inflationTx);
				                }
                                block.Sign(this.blockValidator);
                                chain.AddBlock(block, transactions, MinimumFee, changeSet);
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

                            Logger.Message($"End block #{step} @ {chain.Name} chain: {block.Hash}");
                        }
                        else
                        {
                            throw new ChainException($"add block @ {chain.Name} failed, reason: {reason}");
                        }
                    }
                }

                return blocks;
            }

            return Enumerable.Empty<Block>();
        }

        private Transaction MakeTransaction(IEnumerable<IKeyPair> signees, ProofOfWork pow, Chain chain, byte[] script)
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

            foreach (var kp in signees)
            {
                tx.Sign(kp);
            }

            txChainMap[tx.Hash] = chain;
            txHashMap[tx.Hash] = tx;
            transactions.Add(tx);

            foreach (var signer in signees)
            {
                usedAddresses.Add(Address.FromKey(signer));
            }

            return tx;
        }

        private Transaction MakeTransaction(IKeyPair source, ProofOfWork pow, Chain chain, byte[] script)
        {
            return MakeTransaction(new IKeyPair[] { source }, pow, chain, script);
        }

        public Transaction GenerateCustomTransaction(IKeyPair owner, ProofOfWork pow, Func<byte[]> scriptGenerator)
        {
            return GenerateCustomTransaction(owner, pow, Nexus.RootChain, scriptGenerator);
        }

        public Transaction GenerateCustomTransaction(IKeyPair owner, ProofOfWork pow, Chain chain, Func<byte[]> scriptGenerator)
        {
            var script = scriptGenerator();

            var tx = MakeTransaction(owner, pow, chain, script);
            return tx;
        }

        public Transaction GenerateCustomTransaction(IEnumerable<PhantasmaKeys> owners, ProofOfWork pow, Func<byte[]> scriptGenerator)
        {
            return GenerateCustomTransaction(owners, pow, Nexus.RootChain, scriptGenerator);
        }

        public Transaction GenerateCustomTransaction(IEnumerable<PhantasmaKeys> owners, ProofOfWork pow, Chain chain, Func<byte[]> scriptGenerator)
        {
            var script = scriptGenerator();
            var tx = MakeTransaction(owners, pow, chain, script);
            return tx;
        }

        public Transaction GenerateToken(PhantasmaKeys owner, string symbol, string name, BigInteger totalSupply,
                int decimals, TokenFlags flags, byte[] tokenScript = null, Dictionary<string, int> labels = null, IEnumerable<ContractMethod> customMethods = null, uint seriesID = 0)
        {
            var version = Nexus.GetGovernanceValue(Nexus.RootStorage, Nexus.NexusProtocolVersionTag);
            if (labels == null)
            {
                labels = new Dictionary<string, int>();
            }

            if (tokenScript == null)
            {
                // small script that restricts minting of tokens to transactions where the owner is a witness
                var addressStr = Base16.Encode(owner.Address.ToByteArray());
                string[] scriptString;

                if (version >= 4)
                {
                    scriptString = new string[] {
                    $"alias r3, $result",
                    $"alias r4, $owner",
                    $"@{AccountTrigger.OnMint}: nop",
                    $"load $owner 0x{addressStr}",
                    "push $owner",
                    "extcall \"Address()\"",
                    "extcall \"Runtime.IsWitness\"",
                    "pop $result",
                    $"jmpif $result, @end",
                    $"load r0 \"invalid witness\"",
                    $"throw r0",

                    $"@end: ret"
                    };
                }
                else {
                    scriptString = new string[] {
                    $"alias r1, $triggerMint",
                    $"alias r2, $currentTrigger",
                    $"alias r3, $result",
                    $"alias r4, $owner",

                    $@"load $triggerMint, ""{AccountTrigger.OnMint}""",
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
                    $"load r0 \"invalid witness\"",
                    $"throw r0",

                    $"@end: ret"
                    };
                }
                DebugInfo debugInfo;
                tokenScript = AssemblerUtils.BuildScript(scriptString, "GenerateToken",  out debugInfo, out labels);
            }

            var sb = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, MinimumFee, 9999);

            if (version >= 4)
            {
                var triggerMap = new Dictionary<AccountTrigger, int>();

                var onMintLabel = AccountTrigger.OnMint.ToString();
                if (labels.ContainsKey(onMintLabel))
                {
                    triggerMap[AccountTrigger.OnMint] = labels[onMintLabel];
                }

                var methods = AccountContract.GetTriggersForABI(triggerMap);

                if (customMethods != null)
                {
                    methods = methods.Concat(customMethods);
                }

                var abi = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());
                var abiBytes = abi.ToByteArray();

                sb.CallInterop("Nexus.CreateToken", owner.Address, symbol, name, totalSupply, decimals, flags, tokenScript, abiBytes);
            }
            else
            {
                sb.CallInterop("Nexus.CreateToken", owner.Address, symbol, name, totalSupply, decimals, flags, tokenScript);
            }

            if (!flags.HasFlag(TokenFlags.Fungible))
            {
                ContractInterface nftABI;
                byte[] nftScript;
                NFTUtils.GenerateNFTDummyScript(symbol, name, name, "http://simulator/nft/*", "http://simulator/img/*", out nftScript, out nftABI);
                sb.CallInterop("Nexus.CreateTokenSeries", owner.Address, symbol, new BigInteger(seriesID), totalSupply, TokenSeriesMode.Unique, nftScript, nftABI.ToByteArray());
            }

            sb.SpendGas(owner.Address);
            
            var script = sb.EndScript();

            var tx = MakeTransaction(owner, ProofOfWork.Minimal, Nexus.RootChain, script);

            return tx;
        }

        public Transaction MintTokens(PhantasmaKeys owner, Address destination, string symbol, BigInteger amount)
        {
            var chain = Nexus.RootChain;

            var script = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, MinimumFee, 9999).
                MintTokens(symbol, owner.Address, destination, amount).
                SpendGas(owner.Address).
                EndScript();

            var tx = MakeTransaction(owner, ProofOfWork.None, chain, script);

            return tx;
        }

        public Transaction GenerateSideChainSend(PhantasmaKeys source, string tokenSymbol, Chain sourceChain, Address targetAddress, Chain targetChain, BigInteger amount, BigInteger fee)
        {
            Throw.IfNull(source, nameof(source));
            Throw.If(!Nexus.TokenExists(Nexus.RootStorage, tokenSymbol), "Token does not exist: "+ tokenSymbol);
            Throw.IfNull(sourceChain, nameof(sourceChain));
            Throw.IfNull(targetChain, nameof(targetChain));
            Throw.If(amount <= 0, "positive amount required");

            if (source.Address == targetAddress && tokenSymbol == DomainSettings.FuelTokenSymbol)
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
                sb.CallInterop("Runtime.SwapTokens", targetChain.Name, source.Address, targetAddress, DomainSettings.FuelTokenSymbol, fee);
            }

            var script =
                sb.CallInterop("Runtime.SwapTokens", targetChain.Name, source.Address, targetAddress, tokenSymbol, amount).
                SpendGas(source.Address).
                EndScript();

            var tx = MakeTransaction(source, ProofOfWork.None, sourceChain, script);

            return tx;
        }

        public Transaction GenerateSideChainSettlement(PhantasmaKeys source, Chain sourceChain, Chain destChain, Transaction transaction)
        {
            var script = ScriptUtils.
                BeginScript().
                CallContract(NativeContractKind.Block, nameof(BlockContract.SettleTransaction), sourceChain.Address, transaction.Hash).
                AllowGas(source.Address, Address.Null, MinimumFee, 800).
                SpendGas(source.Address).
                EndScript();
            var tx = MakeTransaction(source, ProofOfWork.None, destChain, script);
            return tx;
        }

        public Transaction GenerateAccountRegistration(PhantasmaKeys source, string name)
        {
            var sourceChain = this.Nexus.RootChain;
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, 9999).CallContract(NativeContractKind.Account, nameof(AccountContract.RegisterName), source.Address, name).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, ProofOfWork.Minimal, sourceChain, script);

            pendingNames.Add(source.Address);
            return tx;
        }

        public Transaction GenerateChain(PhantasmaKeys source, string organization, string parentchain, string name)
        {
            Throw.IfNull(parentchain, nameof(parentchain));

            var script = ScriptUtils.BeginScript().
                AllowGas(source.Address, Address.Null, MinimumFee, 9999).
                CallInterop("Nexus.CreateChain", source.Address, organization, name, parentchain).
                SpendGas(source.Address).EndScript();

            var tx = MakeTransaction(source, ProofOfWork.Minimal, Nexus.RootChain, script);
            return tx;
        }

        public Transaction DeployContracts(PhantasmaKeys source, Chain chain, params string[] contracts)
        {

            var sb = ScriptUtils.BeginScript().
                AllowGas(source.Address, Address.Null, MinimumFee, 999);

            foreach (var contractName in contracts)
            {
                sb.CallInterop("Runtime.DeployContract", source.Address, contractName);
            }

            var script = sb.SpendGas(source.Address).
                EndScript();

            var tx = MakeTransaction(source, ProofOfWork.Minimal, chain, script);
            return tx;
        }

        public Transaction GenerateTransfer(PhantasmaKeys source, Address dest, Chain chain, string tokenSymbol, BigInteger amount, List<PhantasmaKeys> signees = null)
        {
            signees = signees ?? new List<PhantasmaKeys>();
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
                TransferTokens(tokenSymbol, source.Address, dest, amount).
                SpendGas(source.Address).
                EndScript();

            var tx = MakeTransaction(signees, ProofOfWork.None, chain, script);
            return tx;
        }

        public Transaction GenerateSwap(PhantasmaKeys source, Chain chain, string fromSymbol, string toSymbol, BigInteger amount)
        {
            var script = ScriptUtils.BeginScript().
                CallContract(NativeContractKind.Swap, nameof(SwapContract.SwapTokens), source.Address, fromSymbol, toSymbol, amount).
                AllowGas(source.Address, Address.Null, MinimumFee, 9999).
                SpendGas(source.Address).
                EndScript();
            var tx = MakeTransaction(source, ProofOfWork.None, chain, script);
            return tx;
        }

        public Transaction GenerateNftTransfer(PhantasmaKeys source, Address dest, Chain chain, string tokenSymbol, BigInteger tokenId)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, 9999).CallInterop("Runtime.TransferToken", source.Address, dest, tokenSymbol, tokenId).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, ProofOfWork.None, chain, script);
            return tx;
        }

        public Transaction GenerateNftBurn(PhantasmaKeys source, Chain chain, string tokenSymbol, BigInteger tokenId)
        {
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, 9999).CallInterop("Runtime.BurnToken", source.Address, tokenSymbol, tokenId).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, ProofOfWork.None, chain, script);
            return tx;
        }

        public Transaction GenerateNftSale(PhantasmaKeys source, Chain chain, string tokenSymbol, BigInteger tokenId, BigInteger price)
        {
            Timestamp endDate = this.CurrentTime + TimeSpan.FromDays(5);
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, 9999).CallContract(NativeContractKind.Market, nameof(MarketContract.SellToken), source.Address, tokenSymbol, DomainSettings.FuelTokenSymbol, tokenId, price, endDate).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, ProofOfWork.None, chain, script);
            return tx;
        }

        public Transaction MintNonFungibleToken(PhantasmaKeys owner, Address destination, string tokenSymbol, byte[] rom, byte[] ram, BigInteger seriesID)
        {
            var chain = Nexus.RootChain;
            var script = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, MinimumFee, 9999).
                CallInterop("Runtime.MintToken", owner.Address, destination, tokenSymbol, rom, ram, seriesID).  
                SpendGas(owner.Address).
                EndScript();

            var tx = MakeTransaction(owner, ProofOfWork.None, chain, script);
            return tx;
        }

        public Transaction InfuseNonFungibleToken(PhantasmaKeys owner, string tokenSymbol, BigInteger tokenID, string infuseSymbol, BigInteger value)
        {
            var chain = Nexus.RootChain;
            var script = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, MinimumFee, 9999).
                CallInterop("Runtime.InfuseToken", owner.Address, tokenSymbol, tokenID, infuseSymbol, value).
                SpendGas(owner.Address).
                EndScript();

            var tx = MakeTransaction(owner, ProofOfWork.None, chain, script);
            return tx;
        }

        public Transaction GenerateSetTokenMetadata(PhantasmaKeys source, string tokenSymbol, string key, string value)
        {
            var chain = Nexus.RootChain;
            var script = ScriptUtils.BeginScript().AllowGas(source.Address, Address.Null, MinimumFee, 9999).CallInterop("Runtime.SetMetadata", source.Address, tokenSymbol, key, value).SpendGas(source.Address).EndScript();
            var tx = MakeTransaction(source, ProofOfWork.None, chain, script);

            return tx;
        }

        private int step;
        private HashSet<Address> usedAddresses = new HashSet<Address>();

        public void GenerateRandomBlock(Mempool mempool = null)
        {
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
                    case 1: tokenSymbol = DomainSettings.FiatTokenSymbol; break;
                    //case 2: token = Nexus.FuelTokenSymbol; break;
                    default: tokenSymbol = DomainSettings.StakingTokenSymbol; break;
                }

                switch (_rnd.Next() % 7)
                {
                    /*
                    // side-chain send
                    case 1:
                        {
                            var sourceChainList = Nexus.Chains.ToArray();
                            sourceChain = Nexus.GetChainByName( sourceChainList[_rnd.Next() % sourceChainList.Length]);

                            var targetChainList = Nexus.Chains.Select(x => Nexus.GetChainByName(x)).Where(x => Nexus.GetParentChainByName(x.Name) == sourceChain.Name || Nexus.GetParentChainByName(sourceChain.Name) == x.Name).ToArray();
                            var targetChain = targetChainList[_rnd.Next() % targetChainList.Length];

                            var total = UnitConversion.ToBigInteger(1 + _rnd.Next() % 100, DomainSettings.FuelTokenDecimals);

                            var tokenBalance = sourceChain.GetTokenBalance(sourceChain.Storage, tokenSymbol, source.Address);
                            var fuelBalance = sourceChain.GetTokenBalance(sourceChain.Storage, DomainSettings.FuelTokenSymbol, source.Address);

                            var expectedTotal = total;
                            if (tokenSymbol == DomainSettings.FuelTokenSymbol)
                            {
                                expectedTotal += fee;
                            }

                            var sideFee = 0;
                            if (tokenSymbol != DomainSettings.FuelTokenSymbol)
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

                                    var balance = pendingBlock.destChain.GetTokenBalance(pendingBlock.destChain.Storage, pendingBlock.tokenSymbol, source.Address);
                                    if (balance > 0)
                                    {
                                        Logger.Message($"...Settling {pendingBlock.sourceChain.Name}=>{pendingBlock.destChain.Name}: {pendingBlock.hash}");
                                        GenerateSideChainSettlement(source, pendingBlock.sourceChain, pendingBlock.destChain, pendingBlock.hash);
                                    }
                                }
                            }

                            break;
                        }
                        */
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
                            tokenSymbol = DomainSettings.FuelTokenSymbol;

                            var token = Nexus.GetTokenInfo(Nexus.RootStorage, tokenSymbol);

                            var balance = sourceChain.GetTokenBalance(sourceChain.Storage, token, source.Address);
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

                                var currentName = Nexus.RootChain.GetNameFromAddress(Nexus.RootStorage, source.Address);
                                if (currentName == ValidationUtils.ANONYMOUS_NAME)
                                {
                                    var lookup = Nexus.LookUpName(Nexus.RootStorage, randomName);
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
                                var key = PhantasmaKeys.Generate();
                                _keys.Add(key);
                                targetAddress = key.Address;
                            }
                            else
                            {
                                targetAddress = _keys[_rnd.Next() % _keys.Count].Address;
                            }

                            if (source.Address != targetAddress)
                            {
                                var total = UnitConversion.ToBigInteger(1 + _rnd.Next() % 100, DomainSettings.FuelTokenDecimals - 1);

                                var token = Nexus.GetTokenInfo(Nexus.RootStorage, tokenSymbol);
                                var tokenBalance = sourceChain.GetTokenBalance(sourceChain.Storage, token, source.Address);

                                var fuelToken = Nexus.GetTokenInfo(Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
                                var fuelBalance = sourceChain.GetTokenBalance(sourceChain.Storage, fuelToken, source.Address);

                                var expectedTotal = total;
                                if (tokenSymbol == DomainSettings.FuelTokenSymbol)
                                {
                                    expectedTotal += fee;
                                }

                                if (tokenBalance > expectedTotal && fuelBalance > fee)
                                {
                                    Logger.Message($"Rnd.Transfer: {total} {tokenSymbol} from {source.Address} to {targetAddress}");
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
        public void TimeSkipMinutes(int minutes)
        {
            CurrentTime = CurrentTime.AddMinutes(minutes);
            DateTime.SpecifyKind(CurrentTime, DateTimeKind.Utc);

            BeginBlock();
            var tx = GenerateCustomTransaction(_owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(_owner.Address, Address.Null, MinimumFee, 9999)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), _owner.Address).
                    SpendGas(_owner.Address).EndScript());
            EndBlock();

            var txCost = Nexus.RootChain.GetTransactionFee(tx);
        }
        public void TimeSkipHours(int hours)
        {
            CurrentTime = CurrentTime.AddHours(hours);
            DateTime.SpecifyKind(CurrentTime, DateTimeKind.Utc);

            BeginBlock();
            var tx = GenerateCustomTransaction(_owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(_owner.Address, Address.Null, MinimumFee, 9999)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), _owner.Address).
                    SpendGas(_owner.Address).EndScript());
            EndBlock();

            var txCost = Nexus.RootChain.GetTransactionFee(tx);
        }

        public void TimeSkipYears(int years)
        {
            CurrentTime = CurrentTime.AddYears(years);
            DateTime.SpecifyKind(CurrentTime, DateTimeKind.Utc);

            BeginBlock();
            var tx = GenerateCustomTransaction(_owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(_owner.Address, Address.Null, MinimumFee, 9999)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), _owner.Address).
                    SpendGas(_owner.Address).EndScript());
            EndBlock();

            var txCost = Nexus.RootChain.GetTransactionFee(tx);
        }

        public void TimeSkipToDate(DateTime date)
        {
            CurrentTime = date;
            DateTime.SpecifyKind(CurrentTime, DateTimeKind.Utc);

            BeginBlock();
            var tx = GenerateCustomTransaction(_owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(_owner.Address, Address.Null, MinimumFee, 9999)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), _owner.Address).
                    SpendGas(_owner.Address).EndScript());
            EndBlock();
        }

        public void TimeSkipDays(double days, bool roundUp = false, Action<Block> block = null)
        {
            CurrentTime = CurrentTime.AddDays(days);

            if (roundUp)
            {
                CurrentTime = CurrentTime.AddDays(1);
                CurrentTime = new DateTime(CurrentTime.Year, CurrentTime.Month, CurrentTime.Day, 0, 0, 0, DateTimeKind.Utc);

                var timestamp = (Timestamp) CurrentTime;
                var datetime = (DateTime) timestamp;
                if (datetime.Hour == 23)
                    datetime = datetime.AddHours(2);
                
                CurrentTime = new DateTime(datetime.Year, datetime.Month, datetime.Day, datetime.Hour, 0 , 0, DateTimeKind.Utc);   //to set the time of day component to 0
            }

            BeginBlock();
            var tx = GenerateCustomTransaction(_owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(_owner.Address, Address.Null, MinimumFee, 9999)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), _owner.Address).
                    SpendGas(_owner.Address).EndScript());
            
            var blocks = EndBlock();
            if (block != null)
            {
                block.Invoke(blocks.First());
            }

            var txCost = Nexus.RootChain.GetTransactionFee(tx);
            
        }
    }

}
