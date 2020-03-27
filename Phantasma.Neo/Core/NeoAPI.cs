using LunarLabs.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Neo.Cryptography;
using System.Numerics;
using Phantasma.Neo.Utils;
using System.Threading;
using Phantasma.Neo.VM.Types;

namespace Phantasma.Neo.Core
{
    public class BlockIterator
    {
        public uint currentBlock;
        public uint currentTransaction;

        public BlockIterator(NeoAPI api)
        {
            this.currentBlock = api.GetBlockHeight();
            this.currentTransaction = 0;
        }

        public override string ToString()
        {
            return $"{currentBlock}/{currentTransaction}";
        }
    }

    public class NeoException : Exception
    {
        public NeoException(string msg) : base(msg)
        {

        }

        public NeoException(string msg, Exception cause) : base(msg, cause)
        {

        }
    }

    public enum VMType
    {
        Unknown,
        String,
        Boolean,
        Integer,
        Array,
        ByteArray
    }

    [Flags]
    public enum VMState : byte
    {
        NONE = 0,

        HALT = 1 << 0,
        FAULT = 1 << 1,
        BREAK = 1 << 2,
    }

    public class InvokeResult
    {
        public VMState state;
        public decimal gasSpent;
        public StackItem result;
        public Transaction transaction;
    }

    public abstract class NeoAPI
    {
        private static Dictionary<string, string> _systemAssets = null;
        public string LastError { get; protected set; }

        private Action<string> _logger;
        public Action<string> Logger
        {
            get
            {
                return _logger != null ? _logger : DummyLogger;
            }
        }

        public virtual void SetLogger(Action<string> logger = null)
        {
            this._logger = logger;
        }

        private void DummyLogger(string s)
        {

        }

        internal static Dictionary<string, string> GetAssetsInfo()
        {
            if (_systemAssets == null)
            {
                _systemAssets = new Dictionary<string, string>();
                AddAsset("NEO", "c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b");
                AddAsset("GAS", "602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7");
            }

            return _systemAssets;
        }

        private static void AddAsset(string symbol, string hash)
        {
            _systemAssets[symbol] = hash;
        }

        public static byte[] GetAssetID(string symbol)
        {
            var info = GetAssetsInfo();
            foreach (var entry in info)
            {
                if (entry.Key == symbol)
                {
                    return LuxUtils.ReverseHex(entry.Value).HexToBytes();
                }
            }

            return null;
        }

        public static IEnumerable<KeyValuePair<string, string>> Assets
        {
            get
            {
                var info = GetAssetsInfo();
                return info;
            }
        }

        public static string SymbolFromAssetID(byte[] assetID)
        {
            var str = assetID.ByteToHex();
            var result = SymbolFromAssetID(str);
            if (result == null)
            {
                result = SymbolFromAssetID(LuxUtils.ReverseHex(str));
            }

            return result;
        }

        public static string SymbolFromAssetID(string assetID)
        {
            if (assetID == null)
            {
                return null;
            }

            if (assetID.StartsWith("0x"))
            {
                assetID = assetID.Substring(2);
            }

            var info = GetAssetsInfo();
            foreach (var entry in info)
            {
                if (entry.Value == assetID)
                {
                    return entry.Key;
                }
            }

            return null;
        }


        // TODO NEP5 should be refactored to be a data object without the embedded api

        public struct TokenInfo
        {
            public string symbol;
            public string hash;
            public string name;
            public int decimals;
        }

        private static Dictionary<string, TokenInfo> _tokenScripts = null;

        internal static Dictionary<string, TokenInfo> GetTokenInfo()
        {
            if (_tokenScripts == null)
            {
                _tokenScripts = new Dictionary<string, TokenInfo>();
                AddToken("RPX", "ecc6b20d3ccac1ee9ef109af5a7cdb85706b1df9", "RedPulse", 8);
                AddToken("DBC", "b951ecbbc5fe37a9c280a76cb0ce0014827294cf", "DeepBrain", 8);
                AddToken("QLC", "0d821bd7b6d53f5c2b40e217c6defc8bbe896cf5", "Qlink", 8);
                AddToken("APH", "a0777c3ce2b169d4a23bcba4565e3225a0122d95", "Aphelion", 8);
                AddToken("ZPT", "ac116d4b8d4ca55e6b6d4ecce2192039b51cccc5", "Zeepin", 8);
                AddToken("TKY", "132947096727c84c7f9e076c90f08fec3bc17f18", "TheKey", 8);
                AddToken("TNC", "08e8c4400f1af2c20c28e0018f29535eb85d15b6", "Trinity", 8);
                AddToken("CPX", "45d493a6f73fa5f404244a5fb8472fc014ca5885", "APEX", 8);
                AddToken("ACAT", "7f86d61ff377f1b12e589a5907152b57e2ad9a7a", "ACAT", 8);
                AddToken("NRVE", "a721d5893480260bd28ca1f395f2c465d0b5b1c2", "Narrative", 8);
                AddToken("THOR", "67a5086bac196b67d5fd20745b0dc9db4d2930ed", "Thor", 8);
                AddToken("RHT", "2328008e6f6c7bd157a342e789389eb034d9cbc4", "HashPuppy", 0);
                AddToken("IAM", "891daf0e1750a1031ebe23030828ad7781d874d6", "BridgeProtocol", 8);
                AddToken("SWTH", "ab38352559b8b203bde5fddfa0b07d8b2525e132", "Switcheo", 8);
                AddToken("OBT", "0e86a40588f715fcaf7acd1812d50af478e6e917", "Orbis", 8);
                AddToken("ONT", "ceab719b8baa2310f232ee0d277c061704541cfb", "Ontology", 8);
                AddToken("SOUL", "ed07cffad18f1308db51920d99a2af60ac66a7b3", "Phantasma", 8); //OLD 4b4f63919b9ecfd2483f0c72ff46ed31b5bbb7a4
                AddToken("AVA", "de2ed49b691e76754c20fe619d891b78ef58e537", "Travala", 8);
                AddToken("EFX", "acbc532904b6b51b5ea6d19b803d78af70e7e6f9", "Effect.AI", 8);
                AddToken("MCT", "a87cc2a513f5d8b4a42432343687c2127c60bc3f", "Master Contract", 8);
                AddToken("GDM", "d1e37547d88bc9607ff9d73116ebd9381c156f79", "Guardium", 8);
                AddToken("PKC", "af7c7328eee5a275a3bcaee2bf0cf662b5e739be", "Pikcio", 8);
                AddToken("ASA", "a58b56b30425d3d1f8902034996fcac4168ef71d", "Asura", 8);
                AddToken("LRN", "06fa8be9b6609d963e8fc63977b9f8dc5f10895f", "Loopring", 8);
                AddToken("TKY", "132947096727c84c7f9e076c90f08fec3bc17f18", "THEKEY", 8);
                AddToken("NKN", "c36aee199dbba6c3f439983657558cfb67629599", "NKN", 8);
                AddToken("XQT", "6eca2c4bd2b3ed97b2aa41b26128a40ce2bd8d1a", "Quarteria", 8);
                AddToken("EDS", "81c089ab996fc89c468a26c0a88d23ae2f34b5c0", "Endorsit Shares", 8);
            }

            return _tokenScripts;
        }

        private static void AddToken(string symbol, string hash, string name, int decimals)
        {
            _tokenScripts[symbol] = new TokenInfo { symbol = symbol, hash = hash, name = name, decimals = decimals };
        }

        public static IEnumerable<string> TokenSymbols
        {
            get
            {
                var info = GetTokenInfo();
                return info.Keys;
            }
        }

        public static byte[] GetScriptHashFromString(string hash)
        {
            hash = hash.ToLower();
            if (hash.StartsWith("0x"))
            {
                hash = hash.Substring(2);
            }

            return hash.HexToBytes().Reverse().ToArray();
        }

        public static byte[] GetScriptHashFromSymbol(string symbol)
        {
            GetTokenInfo();
            foreach (var entry in _tokenScripts)
            {
                if (entry.Key == symbol)
                {
                    return GetScriptHashFromString(entry.Value.hash);
                }
            }

            return null;
        }

        public static string GetStringFromScriptHash(byte[] hash)
        {
            return LuxUtils.ReverseHex(hash.ToHexString());
        }

        protected static StackItem ParseStack(DataNode stack)
        {
            if (stack != null)
            {
                //var items = new List<StackItem>();

                if (stack.Children.Count() > 0 && stack.Name == "stack")
                {
                    foreach (var child in stack.Children)
                    {
                        var item = ParseStackItems(child);
                        return item;
                        //items.Add(item);
                    }
                }

                return null;
                //return items.ToArray();
            }

            return null;
        }

        protected static StackItem ParseStackItems(DataNode stackItem)
        {
            var type = stackItem.GetString("type");
            var value = stackItem.GetString("value");

            switch (type)
            {
                case "ByteArray":
                    {
                        return new VM.Types.ByteArray(value.HexToBytes());
                    }

                case "Boolean":
                    {
                        return new VM.Types.Boolean(value.ToLower() == "true");
                    }

                case "Integer":
                    {
                        BigInteger intVal;
                        BigInteger.TryParse(value, out intVal);
                        return new VM.Types.Integer(intVal);
                    }
                case "Array": // Type
                    {
                        var items = new List<StackItem>();
                        foreach (var child in stackItem.Children)
                        {
                            var item = ParseStackItems(child);
                            items.Add(item);
                        }
                        return new VM.Types.Array(items);
                    }
                default:
                    {
                        //Console.WriteLine("ParseStack:unknown DataNode stack type: '" + type + "'");
                        break;
                    }
            }

            return null;
        }

        public abstract InvokeResult InvokeScript(byte[] script);

        public InvokeResult InvokeScript(UInt160 scriptHash, string operation, object[] args)
        {
            return InvokeScript(scriptHash, new object[] { operation, args });
        }

        public InvokeResult InvokeScript(UInt160 scriptHash, object[] args)
        {
            var script = GenerateScript(scriptHash, args);

            return InvokeScript(script);
        }

        public static void EmitObject(ScriptBuilder sb, object item)
        {
            if (item is IEnumerable<byte>)
            {
                var arr = ((IEnumerable<byte>)item).ToArray();

                sb.EmitPush(arr);
            }
            else
            if (item is IEnumerable<object>)
            {
                var arr = ((IEnumerable<object>)item).ToArray();

                for (int index = arr.Length - 1; index >= 0; index--)
                {
                    EmitObject(sb, arr[index]);
                }

                sb.EmitPush(arr.Length);
                sb.Emit(OpCode.PACK);
            }
            else
            if (item == null)
            {
                sb.EmitPush("");
            }
            else
            if (item is string)
            {
                sb.EmitPush((string)item);
            }
            else
            if (item is bool)
            {
                sb.EmitPush((bool)item);
            }
            else
            if (item is BigInteger)
            {
                sb.EmitPush((BigInteger)item);
            }
            else
            if (item is UInt160)
            {
                sb.EmitPush(((UInt160)item).ToArray());
            }
            else
            if (item is UInt256)
            {
                sb.EmitPush(((UInt256)item).ToArray());
            }
            else
            if (item is int || item is sbyte || item is short)
            {
                var n = (int)item;
                sb.EmitPush((BigInteger)n);
            }
            else
            if (item is uint || item is byte || item is ushort)
            {
                var n = (uint)item;
                sb.EmitPush((BigInteger)n);
            }
            else
            {
                throw new NeoException("Unsupported contract parameter: " + item.ToString());
            }
        }

        public static byte[] GenerateScript(UInt160 scriptHash, object[] args, bool addNonce = true)
        {
            using (var sb = new ScriptBuilder())
            {
                var items = new Stack<object>();

                if (args != null)
                {
                    foreach (var item in args)
                    {
                        items.Push(item);
                    }
                }

                while (items.Count > 0)
                {
                    var item = items.Pop();
                    EmitObject(sb, item);
                }

                sb.EmitAppCall(scriptHash, false);

                if (addNonce)
                {
                    var timestamp = DateTime.UtcNow.ToTimestamp();
                    var nonce = BitConverter.GetBytes(timestamp);

                    //sb.Emit(OpCode.THROWIFNOT);
                    sb.Emit(OpCode.RET);
                    sb.EmitPush(nonce);
                }

                var bytes = sb.ToArray();

                string hex = bytes.ByteToHex();
                //System.IO.File.WriteAllBytes(@"D:\code\Crypto\neo-debugger-tools\ICO-Template\bin\Debug\inputs.avm", bytes);

                return bytes;
            }
        }

        private Dictionary<string, Transaction> lastTransactions = new Dictionary<string, Transaction>();

        public void GenerateInputsOutputs(NeoKeys key, string symbol, IEnumerable<Transaction.Output> targets, out List<Transaction.Input> inputs, out List<Transaction.Output> outputs, decimal system_fee = 0)
        {
            var from_script_hash = new UInt160(key.signatureHash.ToArray());
            var info = GetAssetsInfo();
            var targetAssetID = LuxUtils.ReverseHex(info[symbol]).HexToBytes();
            if (targets != null)
                foreach (var t in targets)
                    if (t.assetID == null)
                        t.assetID = targetAssetID;
            //else Console.WriteLine("ASSETID target already existed: " + symbol);
            GenerateInputsOutputs(from_script_hash, targets, out inputs, out outputs, system_fee);
        }

        public void GenerateInputsOutputs(UInt160 key, string symbol, IEnumerable<Transaction.Output> targets, out List<Transaction.Input> inputs, out List<Transaction.Output> outputs, decimal system_fee = 0)
        {
            var info = GetAssetsInfo();
            var targetAssetID = LuxUtils.ReverseHex(info[symbol]).HexToBytes();
            if (targets != null)
                foreach (var t in targets)
                    if (t.assetID == null)
                        t.assetID = targetAssetID;
            // else  Console.WriteLine("ASSETID target already existed: " + symbol);
            GenerateInputsOutputs(key, targets, out inputs, out outputs, system_fee);
        }

        public void GenerateInputsOutputs(NeoKeys key, IEnumerable<Transaction.Output> targets, out List<Transaction.Input> inputs, out List<Transaction.Output> outputs, decimal system_fee = 0)
        {
            var from_script_hash = new UInt160(key.signatureHash.ToArray());
            GenerateInputsOutputs(from_script_hash, targets, out inputs, out outputs, system_fee);
        }

        public void GenerateInputsOutputs(UInt160 from_script_hash, IEnumerable<Transaction.Output> targets, out List<Transaction.Input> inputs, out List<Transaction.Output> outputs, decimal system_fee = 0)
        {
            var unspent = GetUnspent(from_script_hash);
            // filter any asset lists with zero unspent inputs
            unspent = unspent.Where(pair => pair.Value.Count > 0).ToDictionary(pair => pair.Key, pair => pair.Value);

            inputs = new List<Transaction.Input>();
            outputs = new List<Transaction.Output>();

            var from_address = from_script_hash.ToAddress();
            var info = GetAssetsInfo();

            // dummy tx to self
            if (targets == null)
            {
                string assetName = "GAS";
                string assetID = info[assetName];
                var targetAssetID = LuxUtils.ReverseHex(assetID).HexToBytes();
                if (!unspent.ContainsKey(assetName))
                    throw new NeoException($"Not enough {assetName} in address {from_address}");

                var src = unspent[assetName][0];
                decimal selected = src.value;
                // Console.WriteLine("SENDING " + selected + " GAS to source");

                inputs.Add(new Transaction.Input()
                {
                    prevHash = src.hash,
                    prevIndex = src.index,
                });

                outputs.Add(new Transaction.Output()
                {
                    assetID = targetAssetID,
                    scriptHash = from_script_hash,
                    value = selected
                });
                return;
            }

            foreach (var target in targets)
                if (target.scriptHash.Equals(from_script_hash))
                    throw new NeoException("Target can't be same as input");

            bool done_fee = false;
            foreach (var asset in info)
            {
                string assetName = asset.Key;
                string assetID = asset.Value;

                if (!unspent.ContainsKey(assetName))
                    continue;

                var targetAssetID = LuxUtils.ReverseHex(assetID).HexToBytes();

                var thistargets = targets.Where(o => o.assetID.SequenceEqual(targetAssetID));

                decimal cost = -1;
                foreach (var target in thistargets)
                    if (target.assetID.SequenceEqual(targetAssetID))
                    {
                        if (cost < 0)
                            cost = 0;
                        cost += target.value;
                    }

                // incorporate fee in GAS utxo, if sending GAS
                bool sendfee = false;
                if (system_fee > 0 && assetName == "GAS")
                {
                    done_fee = true;
                    sendfee = true;
                    if (cost < 0)
                        cost = 0;
                    cost += system_fee;
                }

                if (cost == -1)
                    continue;

                var sources = unspent[assetName].OrderBy(src => src.value);
                decimal selected = 0;

                // >= cost ou > cost??
                foreach (var src in sources)
                {
                    if (selected >= cost && inputs.Count > 0)
                        break;

                    selected += src.value;
                    inputs.Add(new Transaction.Input()
                    {
                        prevHash = src.hash,
                        prevIndex = src.index,
                    });
                    // Console.WriteLine("ADD inp " + src.ToString());
                }

                if (selected < cost)
                    throw new NeoException($"Not enough {assetName} in address {from_address}");

                if (cost > 0)
                    foreach (var target in thistargets)
                        outputs.Add(target);

                if (selected > cost || cost == 0 || sendfee)  /// is sendfee needed? yes if selected == cost
                    outputs.Add(new Transaction.Output()
                    {
                        assetID = targetAssetID,
                        scriptHash = from_script_hash,
                        value = selected - cost
                    });
            }
            /*
                        if (system_fee > 0 && !done_fee && false)
                        {
                            var gasID = LuxUtils.ReverseHex(info["GAS"]).HexToBytes();
                            var gassources = unspent["GAS"];
                            // foreach (var src in gassources)
                            //     Console.WriteLine("SRC: " + src.ToString());
                            decimal feeselected = 0;
                            foreach (var src in gassources)
                                if (feeselected <= system_fee)
                                {
                                    inputs.Add(new Transaction.Input()
                                    {
                                        prevHash = src.hash,
                                        prevIndex = src.index,
                                    });
                                    feeselected += src.value;
                                    Console.WriteLine("add input  " + feeselected);
                                    break;
                                }
                            outputs.Add(new Transaction.Output()
                            {
                                assetID = gasID,
                                scriptHash = from_script_hash,
                                value = feeselected - system_fee
                            });
                        }

                        foreach (var i in inputs)
                            Console.WriteLine("INPUT " + i);
                        foreach (var i in output)
                            Console.WriteLine("OUTPUT " + i);
                        // chaining
                        if (lastTransactions.ContainsKey(from_address))
                        {
                            var lastTx = lastTransactions[from_address];
                            uint index = 0;
                            if (lastTx.outputs != null)
                                foreach (var output in lastTx.outputs)
                                {
                                    if (output.assetID.SequenceEqual(targetAssetID) && output.scriptHash.Equals(from_script_hash))
                                    {
                                        selected += output.value;
                                        var input = new Transaction.Input()
                                        {
                                            prevHash = lastTx.Hash,
                                            prevIndex = index,
                                        };
                                        inputs.Add(input);
                                        break;
                                    }
                                    index++;
                                }
                        }
            */

        }

        public Transaction CallContract(NeoKeys key, UInt160 scriptHash, object[] args, string attachSymbol = null, IEnumerable<Transaction.Output> attachTargets = null)
        {
            var bytes = GenerateScript(scriptHash, args);
            return CallContract(key, scriptHash, bytes, attachSymbol, attachTargets);
        }

        public Transaction CallContract(NeoKeys key, UInt160 scriptHash, string operation, object[] args, string attachSymbol = null, IEnumerable<Transaction.Output> attachTargets = null)
        {
            return CallContract(key, scriptHash, new object[] { operation, args }, attachSymbol, attachTargets);
        }

        public Transaction CallContract(NeoKeys key, UInt160 scriptHash, byte[] bytes, string attachSymbol = null, IEnumerable<Transaction.Output> attachTargets = null)
        {
            List<Transaction.Input> inputs = null;
            List<Transaction.Output> outputs = null;

            if (attachSymbol == null)
            {
                attachSymbol = "GAS";
            }   
            
            if (!string.IsNullOrEmpty(attachSymbol))
            {
                GenerateInputsOutputs(key, attachSymbol, attachTargets, out inputs, out outputs);

                if (inputs.Count == 0)
                {
                    throw new NeoException($"Not enough inputs for transaction");
                }
            }

            var transaction = new Transaction()
            {
                type = TransactionType.InvocationTransaction,
                version = 0,
                script = bytes,
                gas = 0,
                inputs = inputs != null ? inputs.ToArray() : null,
                outputs = outputs != null ? outputs.ToArray() : null,
                attributes = inputs == null ? (new TransactionAttribute[] { new TransactionAttribute(TransactionAttributeUsage.Script, key.Address.AddressToScriptHash()) } ) : null
            };

            transaction.Sign(key);

            if (SendTransaction(key, transaction))
            {
                return transaction;
            }

            return null;
        }

        protected abstract bool SendTransaction(Transaction tx);

        public bool SendTransaction(NeoKeys keys, Transaction tx)
        {
            return SendTransaction(tx);
        }

        public abstract byte[] GetStorage(string scriptHash, byte[] key);

        public abstract Transaction GetTransaction(UInt256 hash);

        public Transaction GetTransaction(string hash)
        {
            var val = new UInt256(LuxUtils.ReverseHex(hash).HexToBytes());
            return GetTransaction(val);
        }

        public Transaction SendAsset(NeoKeys fromKey, string toAddress, string symbol, decimal amount)
        {
            if (String.Equals(fromKey.Address, toAddress, StringComparison.OrdinalIgnoreCase))
            {
                throw new NeoException("Source and dest addresses are the same");
            }

            var toScriptHash = toAddress.GetScriptHashFromAddress();
            var target = new Transaction.Output() { scriptHash = new UInt160(toScriptHash), value = amount };
            var targets = new List<Transaction.Output>() { target };
            return SendAsset(fromKey, symbol, targets);
        }

        public Transaction SendAsset(NeoKeys fromKey, string symbol, IEnumerable<Transaction.Output> targets)
        {
            List<Transaction.Input> inputs;
            List<Transaction.Output> outputs;

            GenerateInputsOutputs(fromKey, symbol, targets, out inputs, out outputs);

            Transaction tx = new Transaction()
            {
                type = TransactionType.ContractTransaction,
                version = 0,
                script = null,
                gas = -1,
                inputs = inputs.ToArray(),
                outputs = outputs.ToArray()
            };

            tx.Sign(fromKey);

            var ok = SendTransaction(tx);
            return ok ? tx : null;
        }

        public Transaction WithdrawAsset(NeoKeys toKey, string fromAddress, string symbol, decimal amount, byte[] verificationScript)
        {
            var fromScriptHash = new UInt160(fromAddress.GetScriptHashFromAddress());
            var target = new Transaction.Output() { scriptHash = new UInt160(toKey.Address.GetScriptHashFromAddress()), value = amount };
            var targets = new List<Transaction.Output>() { target };
            return WithdrawAsset(toKey, fromScriptHash, symbol, targets, verificationScript);
        }

        public Transaction WithdrawAsset(NeoKeys toKey, UInt160 fromScripthash, string symbol, IEnumerable<Transaction.Output> targets, byte[] verificationScript)
        {

            var check = verificationScript.ToScriptHash();
            if (check != fromScripthash)
            {
                throw new ArgumentException("Invalid verification script");
            }

            List<Transaction.Input> inputs;
            List<Transaction.Output> outputs;
            GenerateInputsOutputs(fromScripthash, symbol, targets, out inputs, out outputs);


            List<Transaction.Input> gas_inputs;
            List<Transaction.Output> gas_outputs;
            GenerateInputsOutputs(toKey, "GAS", null, out gas_inputs, out gas_outputs);

            foreach (var entry in gas_inputs)
            {
                inputs.Add(entry);
            }

            foreach (var entry in gas_outputs)
            {
                outputs.Add(entry);
            }

            Transaction tx = new Transaction()
            {
                type = TransactionType.ContractTransaction,
                version = 0,
                script = null,
                gas = -1,
                inputs = inputs.ToArray(),
                outputs = outputs.ToArray()
            };

            var witness = new Witness { invocationScript = ("0014" + toKey.Address.AddressToScriptHash().ByteToHex()).HexToBytes(), verificationScript = verificationScript };
            tx.Sign(toKey, new Witness[] { witness });

            var ok = SendTransaction(tx);
            return ok ? tx : null;
        }

        public Transaction ClaimGas(NeoKeys ownerKey)
        {
            var targetScriptHash = new UInt160(ownerKey.Address.AddressToScriptHash());

            decimal amount;
            var claimable = GetClaimable(targetScriptHash, out amount);

            var references = new List<Transaction.Input>();
            foreach (var entry in claimable)
            {
                references.Add(new Transaction.Input() { prevHash = entry.hash, prevIndex = entry.index });
            }

            if (amount <= 0)
            {
                throw new ArgumentException("No GAS to claim at this address");
            }

            List<Transaction.Input> inputs;
            List<Transaction.Output> outputs;
            GenerateInputsOutputs(ownerKey, "GAS", null, out inputs, out outputs);

            outputs.Add(
            new Transaction.Output()
            {
                scriptHash = targetScriptHash,
                assetID = NeoAPI.GetAssetID("GAS"),
                value = amount
            });

            Transaction tx = new Transaction()
            {
                type = TransactionType.ClaimTransaction,
                version = 0,
                script = null,
                gas = -1,
                claimReferences = references.ToArray(),
                inputs = inputs.ToArray(),
                outputs = outputs.ToArray(),
            };

            tx.Sign(ownerKey);

            var ok = SendTransaction(tx);
            return ok ? tx : null;
        }

        public Transaction ClaimGas(NeoKeys ownerKey, string fromAddress, byte[] verificationScript)
        {
            var fromScriptHash = new UInt160(fromAddress.GetScriptHashFromAddress());
            return ClaimGas(ownerKey, fromScriptHash, verificationScript);
        }

        // claim from contract, without having private key
        public Transaction ClaimGas(NeoKeys ownerKey, UInt160 fromScripthash, byte[] verificationScript)
        {

            var check = verificationScript.ToScriptHash();
            if (check != fromScripthash)
            {
                throw new ArgumentException("Invalid verification script");
            }

            decimal amount;
            var claimable = GetClaimable(fromScripthash, out amount);

            var references = new List<Transaction.Input>();
            foreach (var entry in claimable)
            {
                references.Add(new Transaction.Input() { prevHash = entry.hash, prevIndex = entry.index });
            }

            if (amount <= 0)
            {
                throw new ArgumentException("No GAS to claim at this address");
            }

            List<Transaction.Input> inputs;
            List<Transaction.Output> outputs;
            GenerateInputsOutputs(ownerKey, "GAS", null, out inputs, out outputs);

            outputs.Add(
            new Transaction.Output()
            {
                scriptHash = fromScripthash,
                assetID = NeoAPI.GetAssetID("GAS"),
                value = amount
            });

            Transaction tx = new Transaction()
            {
                type = TransactionType.ClaimTransaction,
                version = 0,
                script = null,
                gas = -1,
                claimReferences = references.ToArray(),
                inputs = inputs.ToArray(),
                outputs = outputs.ToArray(),
            };

            var witness = new Witness { invocationScript = ("0014" + ownerKey.Address.AddressToScriptHash().ByteToHex()).HexToBytes(), verificationScript = verificationScript };
            tx.Sign(ownerKey, new Witness[] { witness });

            var ok = SendTransaction(tx);
            return ok ? tx : null;
        }

        public Dictionary<string, decimal> GetBalancesOf(NeoKeys key)
        {
            return GetBalancesOf(key.Address);
        }

        public Dictionary<string, decimal> GetBalancesOf(string address)
        {
            var assets = GetAssetBalancesOf(address);
            var tokens = GetTokenBalancesOf(address);

            var result = new Dictionary<string, decimal>();

            foreach (var entry in assets)
            {
                result[entry.Key] = entry.Value;
            }

            foreach (var entry in tokens)
            {
                result[entry.Key] = entry.Value;
            }

            return result;
        }

        public Dictionary<string, decimal> GetTokenBalancesOf(NeoKeys key)
        {
            return GetTokenBalancesOf(key.Address);
        }

        public Dictionary<string, decimal> GetTokenBalancesOf(string address)
        {
            var result = new Dictionary<string, decimal>();
            foreach (var symbol in TokenSymbols)
            {
                var token = GetToken(symbol);
                try
                {
                    var amount = token.BalanceOf(address);
                    if (amount > 0)
                    {
                        result[symbol] = amount;
                    }
                }
                catch
                {
                    continue;
                }
            }
            return result;
        }

        public abstract bool HasPlugin(string hash);

        public abstract string GetNep5Transfers(UInt160 hash, DateTime timestamp);

        public string GetNep5Transfers(NeoKeys key, DateTime timestamp)
        {
            return GetNep5Transfers(key.Address, timestamp);
        }

        public string GetNep5Transfers(string address, DateTime timestamp)
        {
            var hash = new UInt160(address.AddressToScriptHash());
            return GetNep5Transfers(hash, timestamp);
        }


        public abstract Dictionary<string, decimal> GetAssetBalancesOf(UInt160 hash);

        public Dictionary<string, decimal> GetAssetBalancesOf(NeoKeys key)
        {
            return GetAssetBalancesOf(key.Address);
        }

        public Dictionary<string, decimal> GetAssetBalancesOf(string address)
        {
            var hash = new UInt160(address.AddressToScriptHash());
            return GetAssetBalancesOf(hash);
        }

        public bool IsAsset(string symbol)
        {
            var info = GetAssetsInfo();
            return info.ContainsKey(symbol);
        }

        public bool IsToken(string symbol)
        {
            var info = GetTokenInfo();
            return info.ContainsKey(symbol);
        }

        public NEP5 GetToken(string symbol)
        {
            var info = GetTokenInfo();
            if (info.ContainsKey(symbol))
            {
                var token = info[symbol];
                return new NEP5(this, token.hash, token.name, new BigInteger(token.decimals));
            }

            throw new NeoException("Invalid token symbol");
        }

        public struct UnspentEntry
        {
            public UInt256 hash;
            public uint index;
            public decimal value;
        }

        public abstract List<UnspentEntry> GetClaimable(UInt160 hash, out decimal amount);

        public abstract Dictionary<string, List<UnspentEntry>> GetUnspent(UInt160 scripthash);

        public Dictionary<string, List<UnspentEntry>> GetUnspent(string address)
        {
            return GetUnspent(new UInt160(address.AddressToScriptHash()));
        }

        #region BLOCKS
        public abstract uint GetBlockHeight();
        public abstract Block GetBlock(UInt256 hash);
        public abstract Block GetBlock(uint height);

        #endregion

        public Transaction DeployContract(NeoKeys keys, byte[] script, byte[] parameter_list, byte return_type, ContractProperties properties, string name, string version, string author, string email, string description)
        {
            if (script.Length > 1024 * 1024) return null;

            byte[] gen_script;
            using (var sb = new ScriptBuilder())
            {
                sb.EmitPush(description);
                sb.EmitPush(email);
                sb.EmitPush(author);
                sb.EmitPush(version);
                sb.EmitPush(name);
                sb.EmitPush((byte)properties);
                sb.EmitPush(return_type);
                sb.EmitPush(parameter_list);
                sb.EmitPush(script);

                sb.EmitSysCall("Neo.Contract.Create");

                gen_script = sb.ToArray();

                //string hex = bytes.ByteToHex();
                //System.IO.File.WriteAllBytes(@"D:\code\Crypto\neo-debugger-tools\ICO-Template\bin\Debug\inputs.avm", bytes);                
            }

            decimal fee = 100;

            if (properties.HasFlag(ContractProperties.HasStorage))
            {
                fee += 400;
            }

            if (properties.HasFlag(ContractProperties.HasDynamicInvoke))
            {
                fee += 500;
            }

            fee -= 10; // first 10 GAS is free

            List<Transaction.Input> inputs;
            List<Transaction.Output> outputs;

            GenerateInputsOutputs(keys, "GAS", null, out inputs, out outputs, fee);

            Transaction tx = new Transaction()
            {
                type = TransactionType.InvocationTransaction,
                version = 0,
                script = gen_script,
                gas = fee,
                inputs = inputs.ToArray(),
                outputs = outputs.ToArray()
            };

            tx.Sign(keys);

            var ok = SendTransaction(tx);
            return ok ? tx : null;
        }

        public Transaction WaitForTransaction(BlockIterator iterator, Func<Transaction, bool> filter, int maxBlocksToWait = 9999)
        {
            uint newBlock;

            while (true)
            {
                newBlock = GetBlockHeight();

                if (iterator.currentBlock > newBlock)
                {
                    return null;
                }

                while (iterator.currentBlock <= newBlock)
                {
                    var block = GetBlock(iterator.currentBlock);

                    if (block != null)
                    {
                        for (uint i = iterator.currentTransaction; i < block.transactions.Length; i++)
                        {
                            var tx = block.transactions[i];
                            tx.block = block;

                            iterator.currentTransaction++;

                            if (filter(tx))
                            {
                                return tx;
                            }
                        }

                        iterator.currentBlock++;
                        iterator.currentTransaction = 0;
                    }
                }

                if (maxBlocksToWait == 0)
                {
                    return null;
                }
                else
                {
                    maxBlocksToWait--;
                    Thread.Sleep(5000);
                }
            }
        }

        public void WaitForTransaction(BlockIterator iterator, NeoKeys keys, Transaction tx, int maxBlocksToWait = 9999)
        {
            if (tx == null)
            {
                throw new ArgumentNullException();
            }

            WaitForTransaction(iterator, x => x.Hash == tx.Hash, maxBlocksToWait);
            lastTransactions[keys.Address] = tx;
        }


    }

}
