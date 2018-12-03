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

namespace Phantasma.Blockchain.Contracts
{
    public abstract class SmartContract : IContract
    {
        public ContractInterface ABI { get; private set; }
        public abstract string Name { get; }

        public BigInteger Order { get; internal set; } // TODO remove this?

        private Dictionary<byte[], byte[]> _storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer()); // TODO remove this?

        public RuntimeVM Runtime { get; private set; }
        public StorageContext Storage => Runtime.ChangeSet;

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
        private HashSet<Hash> settledTransactions = new HashSet<Hash>();

        public bool IsSettled(Hash hash)
        {
            return settledTransactions.Contains(hash);
        }

        protected void RegisterHashAsKnown(Hash hash)
        {
            settledTransactions.Add(hash);
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

        public void SettleBlock(Address sourceChain, Hash hash)
        {
            Runtime.Expect(IsParentChain(sourceChain) || IsChildChain(sourceChain), "source must be parent or child chain");

            Runtime.Expect(!IsSettled(hash), "hash already settled");

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
                    if (evt.Kind == EventKind.TokenEscrow)
                    {
                        var data = Serialization.Unserialize<TokenEventData>(evt.Data);
                        if (data.chainAddress == this.Runtime.Chain.Address)
                        {
                            symbol = data.symbol;
                            value = data.value;
                            targetAddress = evt.Address;
                            // TODO what about multiple escrow events in the same tx, seems not supported yet?
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
    }
}
