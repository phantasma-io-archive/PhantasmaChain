using System;
using System.Reflection;
using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.VM.Contracts;
using Phantasma.Core.Utils;
using Phantasma.Blockchain.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.IO;
using Phantasma.VM;
using System.Linq;

namespace Phantasma.Blockchain.Contracts
{
    public abstract class SmartContract : IContract
    {
        public BigInteger Order { get; internal set; }

        public ContractInterface ABI { get; private set; }

        private Dictionary<byte[], byte[]> _storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public RuntimeVM Runtime { get; private set; }
        public StorageContext Storage => Runtime.ChangeSet;

        public abstract ContractKind Kind { get; }

        private Dictionary<string, MethodInfo> _methodTable = new Dictionary<string, MethodInfo>();

        public SmartContract()
        {
            this.Order = 0;

            BuildMethodTable();
        }

        internal void SetRuntimeData(RuntimeVM VM)
        {
            this.Runtime = VM;
        }

        public int GetSize()
        {
            return 0; // TODO
        }

        public bool IsWitness(Address address)
        {
            if (address == this.Runtime.Chain.Address) // TODO this is not right...
            {
                return true;
            }

            return Runtime.Transaction.IsSignedBy(address);
        }

        #region METHOD TABLE
        private void BuildMethodTable()
        {
            var type = this.GetType();

            var srcMethods = type.GetMethods(BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Instance);
            var methods = new List<ContractMethod>();

            var ignore = new HashSet<string>(new string[] { "ToString", "GetType", "Equals", "GetHashCode", "CallMethod", "SetTransaction" });

            foreach (var srcMethod in srcMethods)
            {
                var parameters = new List<VM.VMType>();
                var srcParams = srcMethod.GetParameters();

                var methodName = srcMethod.Name;
                if (methodName.StartsWith("get_"))
                {
                    methodName = methodName.Substring(4);
                }

                if (ignore.Contains(methodName))
                {
                    continue;
                }

                var isVoid = srcMethod.ReturnType == typeof(void);
                var returnType = isVoid ? VMType.None : VMObject.GetVMType(srcMethod.ReturnType);

                bool isValid = isVoid || returnType != VMType.None;
                if (!isValid)
                {
                    continue;
                }

                foreach (var srcParam in srcParams)
                {
                    var paramType = srcParam.ParameterType;
                    var vmtype = VMObject.GetVMType(paramType);

                    if (vmtype != VMType.None)
                    {
                        parameters.Add(vmtype);
                    }
                    else
                    {
                        isValid = false;
                        break;
                    }
                }

                if (isValid)
                {
                    _methodTable[methodName] = srcMethod;
                    var method = new ContractMethod(methodName, returnType, parameters.ToArray());
                    methods.Add(method);
                }
            }

            this.ABI = new ContractInterface(methods);
        }

        internal bool HasInternalMethod(string methodName)
        {
            return _methodTable.ContainsKey(methodName);
        }

        internal object CallInternalMethod(string name, object[] args)
        {
            Throw.If(!_methodTable.ContainsKey(name), "unknowm internal method");

            var method = _methodTable[name];
            Throw.IfNull(method, nameof(method));

            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.ParameterType.IsEnum)
                {
                    var receivedType = args[i].GetType();
                    if (!receivedType.IsEnum)
                    {
                        var val = Enum.Parse(p.ParameterType, args[i].ToString());
                        args[i] = val;
                    }
                }
            }

            return method.Invoke(this, args);
        }
        #endregion

        #region SIDE CHAINS
        // NOTE we should later prevent contracts from manipulating those
        private HashSet<Hash> knownTransactions = new HashSet<Hash>();
        internal bool IsKnown(Hash hash)
        {
            return knownTransactions.Contains(hash);
        }

        protected void RegisterHashAsKnown(Hash hash)
        {
            knownTransactions.Add(hash);
        }

        public bool IsChain(Address address)
        {
            return Runtime.Nexus.FindChainByAddress(address) != null;
        }

        public bool IsRootChain(Address address)
        {
            var chain = Runtime.Nexus.FindChainByAddress(address);
            if (chain == null)
            {
                return false;
            }

            return chain.IsRoot;
        }

        public bool IsSideChain(Address address)
        {
            var chain = Runtime.Nexus.FindChainByAddress(address);
            if (chain == null)
            {
                return false;
            }

            return !chain.IsRoot;
        }

        public bool IsParentChain(Address address)
        {
            if (Runtime.Chain.ParentChain == null)
            {
                return false;
            }
            return address == this.Runtime.Chain.ParentChain.Address;
        }

        public bool IsChildChain(Address address)
        {
            var chain = Runtime.Nexus.FindChainByAddress(address);
            if (chain == null)
            {
                return false;
            }

            return chain.ParentChain == this.Runtime.Chain;
        }

        public void SendTokens(Address targetChain, Address from, Address to, string symbol, BigInteger amount)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(IsParentChain(targetChain) || IsChildChain(targetChain), "target must be parent or child chain");

            var otherChain = this.Runtime.Nexus.FindChainByAddress(targetChain);
            /*TODO
            var otherConsensus = (ConsensusContract)otherChain.FindContract(ContractKind.Consensus);
            Runtime.Expect(otherConsensus.IsValidReceiver(from));*/

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "must be fungible token");

            if (token.IsCapped)
            {
                var sourceSupplies = this.Runtime.Chain.GetTokenSupplies(token);
                var targetSupplies = otherChain.GetTokenSupplies(token);

                if (IsParentChain(targetChain))
                {
                    Runtime.Expect(sourceSupplies.MoveToParent(amount), "source supply check failed");
                    Runtime.Expect(targetSupplies.MoveFromChild(this.Runtime.Chain, amount), "target supply check failed");
                }
                else // child chain
                {
                    Runtime.Expect(sourceSupplies.MoveToChild(this.Runtime.Chain, amount), "source supply check failed");
                    Runtime.Expect(targetSupplies.MoveFromParent(amount), "target supply check failed");
                }
            }

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Burn(balances, from, amount), "burn failed");

            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { symbol = symbol, value = amount, chainAddress = targetChain });
        }

        public void SendToken(Address targetChain, Address from, Address to, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(IsParentChain(targetChain) || IsChildChain(targetChain), "source must be parent or child chain");

            var otherChain = this.Runtime.Nexus.FindChainByAddress(targetChain);

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), "must be non-fungible token");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Runtime.Expect(ownerships.Take(from, tokenID), "take token failed");

            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { symbol = symbol, value = tokenID, chainAddress = targetChain });
        }

        public void SettleBlock(Address sourceChain, Hash hash)
        {
            Runtime.Expect(IsParentChain(sourceChain) || IsChildChain(sourceChain), "source must be parent or child chain");

            Runtime.Expect(!IsKnown(hash), "hash already settled");

            var otherChain = this.Runtime.Nexus.FindChainByAddress(sourceChain);

            var block = otherChain.FindBlockByHash(hash);
            Runtime.Expect(block != null, "invalid block");

            int settlements = 0;

            foreach (var txHash in block.TransactionHashes)
            {
                string symbol = null;
                BigInteger value = 0;
                Address targetAddress = Address.Null;

                var evts = block.GetEventsForTransaction(txHash);

                foreach (var evt in evts)
                {
                    if (evt.Kind == EventKind.TokenSend)
                    {
                        var data = Serialization.Unserialize<TokenEventData>(evt.Data);
                        if (data.chainAddress == this.Runtime.Chain.Address)
                        {
                            symbol = data.symbol;
                            value = data.value;
                            targetAddress = evt.Address;
                            // TODO what about multiple send events in the same tx, seems not supported yet?
                        }
                    }
                }

                if (symbol != null)
                {
                    settlements++;
                    Runtime.Expect(value > 0, "value must be greater than zero");
                    Runtime.Expect(targetAddress != Address.Null, "target must not be null");

                    var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
                    Runtime.Expect(token != null, "invalid token");

                    if (token.Flags.HasFlag(TokenFlags.Fungible))
                    {
                        if (token.IsCapped)
                        {
                            var sourceSupplies = otherChain.GetTokenSupplies(token);
                            var targetSupplies = this.Runtime.Chain.GetTokenSupplies(token);

                            if (IsParentChain(sourceChain))
                            {
                                Runtime.Expect(sourceSupplies.MoveToChild(this.Runtime.Chain, value), "source supply check failed");
                                Runtime.Expect(targetSupplies.MoveFromParent(value), "target supply check failed");
                            }
                            else // child chain
                            {
                                Runtime.Expect(sourceSupplies.MoveToParent(value), "source supply check failed");
                                Runtime.Expect(targetSupplies.MoveFromChild(this.Runtime.Chain, value), "target supply check failed");
                            }
                        }

                        var balances = this.Runtime.Chain.GetTokenBalances(token);
                        Runtime.Expect(token.Mint(balances, targetAddress, value), "mint failed");
                    }
                    else
                    {
                        var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
                        Runtime.Expect(ownerships.Give(targetAddress, value), "give token failed");
                    }

                    Runtime.Notify(EventKind.TokenReceive, targetAddress, new TokenEventData() { symbol = symbol, value = value, chainAddress = otherChain.Address });
                }
            }

            Runtime.Expect(settlements > 0, "no settlements in the block");
            RegisterHashAsKnown(hash);
        }
        #endregion

        #region FUNGIBLE TOKENS
        public void MintTokens(Address target, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            Runtime.Expect(IsWitness(token.Owner), "invalid witness");

            if (token.IsCapped)
            {
                var supplies = this.Runtime.Chain.GetTokenSupplies(token);
                Runtime.Expect(supplies.Mint(amount), "minting failed");
            }

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Mint(balances, target, amount), "minting failed");

            Runtime.Notify(EventKind.TokenMint, target, new TokenEventData() { symbol = symbol, value = amount, chainAddress = this.Runtime.Chain.Address });
        }

        public void BurnTokens(Address from, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            if (token.IsCapped)
            {
                var supplies = this.Runtime.Chain.GetTokenSupplies(token);
                Runtime.Expect(supplies.Burn(amount), "minting failed");
            }

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Burn(balances, from, amount), "burning failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = amount });
        }

        public void TransferTokens(Address source, Address destination, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(source != destination, "source and destination must be different");
            Runtime.Expect(IsWitness(source), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Transfer(balances, source, destination, amount), "transfer failed");

            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
        }

        public BigInteger GetBalance(Address address, string symbol)
        {
            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            return balances.Get(address);
        }
        #endregion

        #region NON FUNGIBLE TOKENS
        public BigInteger[] GetTokens(Address address, string symbol)
        {
            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            return ownerships.Get(address).ToArray();
        }

        public BigInteger MintToken(Address from, string symbol, byte[] data)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            var tokenID = this.Runtime.Chain.CreateNFT(token, data);
            Runtime.Expect(tokenID > 0, "invalid tokenID");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Runtime.Expect(ownerships.Give(from, tokenID), "give token failed");

            Runtime.Notify(EventKind.TokenMint, from, new TokenEventData() { symbol = symbol, value = tokenID });
            return tokenID;
        }

        public void BurnToken(Address from, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Runtime.Expect(ownerships.Take(from, tokenID), "take token failed");

            Runtime.Expect(this.Runtime.Chain.DestroyNFT(token, tokenID), "destroy token failed");

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, value = tokenID });
        }

        public void TransferToken(Address source, Address destination, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(source), "invalid witness");

            Runtime.Expect(source != destination, "source and destination must be different");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            var ownerships = this.Runtime.Chain.GetTokenOwnerships(token);
            Runtime.Expect(ownerships.Take(source, tokenID), "take token failed");
            Runtime.Expect(ownerships.Give(destination, tokenID), "give token failed");

            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = tokenID, symbol = symbol });
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = tokenID, symbol = symbol });
        }

        public void SetTokenViewer(Address source, string symbol, string url)
        {
            Runtime.Expect(IsWitness(source), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(!token.IsFungible, "token must be non-fungible");

            Runtime.Expect(token.Owner == source, "owner expected");

            token.SetViewer(url);

            Runtime.Notify(EventKind.TokenInfo, source, url);
        }

        #endregion

    }
}
