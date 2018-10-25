using Phantasma.Blockchain.Contracts.Native;
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
        private Chain nameChain;

        private static readonly string[] accountNames = {
            "Aberration", "Absence", "Ace", "Acid", "Alakazam", "Alien", "Alpha", "Angel", "Angler", "Anomaly", "Answer", "Ant", "Aqua", "Archangel",
            "Aspect", "Atom", "Avatar", "Azure", "Behemoth", "Beta", "Bishop", "Bite", "Blade", "Blank", "Blazer", "Bliss", "Boggle", "Bolt",
            "Bullet", "Bullseye", "Burn", "Chaos", "Charade", "Charm", "Chase", "Chief", "Chimera", "Chronicle", "Cipher", "Claw", "Cloud", "Combo",
            "Comet (C0M37)", "Complex (C0MPL3X)", "Conjurer (C0NJUR3R)", "Cowboy (C0WB0Y)", "Craze (CR4Z3)", "Crotchet (CR07CH37)", "Crow (CR0W)", "Crypto (CRYP70)", "Cryptonic (CRYP70N1C)", "Curse (CUR53)", "Dagger (D4663R)", "Dante (D4N73)", "Daydream (D4YDR34M)", "Dexter (D3X73R)", "Diablo (D14BL0)", "Doc (D0C)", "Doppelganger (D0PP3L64N63R)", "Drake (DR4K3)", "Dread (DR34D)", "Duckling (DUCKL1N6)", "Ecstasy (3C5745Y)", "Enigma (3N16M4)", "Epitome (3P170M3)", "Essence (3553NC3)", "Eternity (373RN17Y)", "Face (F4C3)", "Fata Morgana (F474 M0R64N4)", "Fetish (F3715H)", "Fiend (F13ND)", "Flash (FL45H)", "Flinch (FL1NCH)", "Fluke (FLUK3)", "Fragment (FR46M3N7)", "Freak (FR34K)", "Fury (FURY)", "Gem (63M)", "Ghoul (6H0UL)", "Gloom (6L00M)", "Gluttony (6LU770NY)", "Grace (6R4C3)", "Griffin (6R1FF1N)", "Grim (6R1M)", "Grimace (6R1M4C3)", "Grin (6R1N)", "Guru (6URU)", "Habit (H4B17)", "Habitat (H4B1747)", "Haze (H4Z3)", "Hex (H3X)", "Hoax (H04X)", "Hollow (H0LL0W)", "Hound (H0UND)", "Ibis (1B15)", "Idol (1D0L)", "Impossible (1MP0551BL3)", "Indie (1ND13)", "Infinity (1NF1N17Y)", "Jinx (J1NX)", "Juju (JUJU)", "Kid (K1D)", "Kiss (K155)", "Knight (KN16H7)", "Knot (KN07)", "Law (L4W)", "Legacy (L364CY)", "Lightning (L16H7N1N6)", "Limbo (L1MB0)", "Lynx (LYNX)", "Maestro (M4357R0)", "Mania (M4N14)", "Marshall (M4R5H4LL)", "Mascot (M45C07)", "Matriarch (M47R14RCH)", "Memento (M3M3N70)", "Memory (M3M0RY)", "Mermaid (M3RM41D)", "Mime (M1M3)", "Mongoose (M0N60053)", "Monkey (M0NK3Y)", "Moonlight (M00NL16H7)", "Moonshine (M00N5H1N3)", "Morgana (M0R64N4)", "Mothership (M07H3R5H1P)", "Myth (MY7H)", "Nemesis (N3M3515)", "Nemo (N3M0)", "Nest (N357)", "Neurosis (N3UR0515)", "Nighthawk (N16H7H4WK)", "Nightmare (N16H7M4R3)", "Nightowl (N16H70WL)", "Nix (N1X)", "Nobody (N0B0DY)", "Nova (N0V4)", "Oblivion (0BL1V10N)", "Obsidian (0B51D14N)", "Oddity (0DD17Y)", "Omega (0M364)", "Onyx (0NYX)", "Oracle (0R4CL3)", "Override (0V3RR1D3)", "Owl (0WL)", "Panther (P4N7H3R)", "Paradox (P4R4D0X)", "Paragon (P4R460N)", "Parody (P4R0DY)", "Particle (P4R71CL3)", "Patriarch (P47R14RCH)", "Perplex (P3RPL3X)", "Phantasm (PH4N745M)", "Phantom (PH4N70M)", "Phase (PH453)", "Phobia (PH0B14)", "Phoenix (PH03N1X)", "Pierce (P13RC3)", "Plague (PL46U3)", "Poltergeist (P0L73R63157)", "Prankster (PR4NK573R)", "Pride (PR1D3)", "Prima Donna (PR1M4 D0NN4)", "Prophecy (PR0PH3CY)", "Proto (PR070)", "Proxy (PR0XY)", "Quad (QU4D)", "Quake (QU4K3)", "Question (QU35710N)", "Quicksilver (QU1CK51LV3R)", "Quirk (QU1RK)", "Rapture (R4P7UR3)", "Ray (R4Y)", "Reaper (R34P3R)", "Rebus (R3BU5)", "Reverse (R3V3R53)", "Riddle (R1DDL3)", "Rider (R1D3R)", "Rogue (R06U3)", "Rune (RUN3)", "Saber (54B3R)", "Sabertooth (54B3R7007H)", "Sage (5463)", "Savant (54V4N7)", "Scepter (5C3P73R)", "Sentinel (53N71N3L)", "Serenity (53R3N17Y)", "Serpent (53RP3N7)", "Shade (5H4D3)", "Shadow (5H4D0W)", "Shark (5H4RK)", "Shell (5H3LL)", "Shepherd (5H3PH3RD)", "Shield (5H13LD)", "Sickle (51CKL3)", "Silver (51LV3R)", "Skipper (5K1PP3R)", "Sliver (5L1V3R)", "Sloth (5L07H)", "Smog (5M06)", "Specter (5P3C73R)", "Sphinx (5PH1NX)", "Spider (5P1D3R)", "Splinter (5PL1N73R)", "Spook (5P00K)", "Squirt (5QU1R7)", "Stalker (574LK3R)", "Storm (570RM)", "Streak (57R34K)", "Sunshine (5UN5H1N3)", "Surprise (5URPR153)", "Swan (5W4N)", "Talisman (74L15M4N)", "Tinge (71N63)", "Torpedo (70RP3D0)", "Trace (7R4C3)", "Tracer (7R4C3R)", "Trail (7R41L)", "Tremor (7R3M0R)", "Trinity (7R1N17Y)", "Trix (7R1X)", "Trixy (7R1XY)", "Trust (7RU57)", "Twist (7W157)", "Umbra (UMBR4)", "Umbrage (UMBR463)", "Vacuum (V4CUUM)", "Vagabond (V464B0ND)", "Veil (V31L)", "Vermin (V3RM1N)", "Vestige (V357163)", "Viper (V1P3R)", "Visage (V15463)", "Vision (V1510N)", "Void (V01D)", "Voodoo (V00D00)", "Voyage (V0Y463)", "Wasp (W45P)", "Web (W3B)", "Webster (W3B573R)", "Whiz (WH1Z)", "Witcher (W17CH3R)", "Wolf (W0LF)", "Wraith (WR417H)", "Wrath (WR47H)", "Wyvern (WYV3RN)", "Zero (Z3R0)", "Zigzag (Z16Z46)", "Zion (Z10N)"];


        private List<SideChainPendingTransaction> _pendingTxs = new List<SideChainPendingTransaction>();

        public ChainSimulator(KeyPair ownerKey, int seed)
        {
            _owner = ownerKey;
            this.Nexus = new Nexus(_owner);

            this.bankChain = Nexus.FindChainByName("bank");
            this.nameChain = Nexus.FindChainByName("names");

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


                switch (_rnd.Next() % 7)
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

                    // name register
                    case 5:
                        {
                            chain = nameChain;
                            token = Nexus.NativeToken;

                            var balance = chain.GetTokenBalance(token, source.Address);
                            if (balance >= NamesContract.RegistrationCost)
                            {
                                var randomName
                                var script = ScriptUtils.CallContractScript(chain, "Register", source.Address, randomName);
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
                        _currentTime += TimeSpan.FromMinutes(45);

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
