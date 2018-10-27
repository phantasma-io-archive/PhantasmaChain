using System.Text;
using System.Reflection;
using System.Collections.Generic;

using Phantasma.VM.Contracts;
using Phantasma.VM;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.IO;

namespace Phantasma.Blockchain.Contracts.Native
{
    public abstract class NativeContract : SmartContract
    {
        private Address _address;

        public override byte[] Script => null;

        private ContractInterface _ABI;
        public override ContractInterface ABI => _ABI;

        internal abstract ContractKind Kind { get; }

        private Dictionary<string, MethodInfo> _methodTable = new Dictionary<string, MethodInfo>();

        public NativeContract() : base()
        {
            var type = this.GetType();

            var bytes = Encoding.ASCII.GetBytes(type.Name);
            var hash = CryptoExtensions.Sha256(bytes);
            _address = new Address(hash);

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

            _ABI = new ContractInterface(methods);
        }

        public object CallMethod(string name, object[] args)
        {
            var method = _methodTable[name];
            return method.Invoke(this, args);
        }

        private HashSet<Hash> knownTransactions = new HashSet<Hash>();
        internal bool IsKnown(Hash hash)
        {
            return knownTransactions.Contains(hash);
        }

        protected void RegisterHashAsKnown(Hash hash)
        {
            knownTransactions.Add(hash);
        }

        #region SIDE CHAINS
        public bool IsChain(Address address)
        {
            return Runtime.Nexus.FindChainByAddress(address) != null;
        }

        public bool IsRootChain(Address address)
        {
            return (IsChain(address) && address == this.Runtime.Chain.Address);
        }

        public bool IsSideChain(Address address)
        {
            return (IsChain(address) && address != this.Runtime.Chain.Address);
        }

        public void SendTokens(Address targetChain, Address from, Address to, string symbol, BigInteger amount)
        {
            Expect(IsWitness(from));

            if (IsRootChain(this.Runtime.Chain.Address))
            {
                Expect(IsSideChain(targetChain));
            }
            else
            {
                Expect(IsRootChain(targetChain));
            }

            var otherChain = this.Runtime.Nexus.FindChainByAddress(targetChain);
            /*TODO
            var otherConsensus = (ConsensusContract)otherChain.FindContract(ContractKind.Consensus);
            Expect(otherConsensus.IsValidReceiver(from));*/

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            token.Burn(balances, from, amount);

            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { symbol = symbol, amount = amount, chainAddress = targetChain });
        }

        public void ReceiveTokens(Address sourceChain, Address to, Hash hash)
        {
            if (IsRootChain(this.Runtime.Chain.Address))
            {
                Expect(IsSideChain(sourceChain));
            }
            else
            {
                Expect(IsRootChain(sourceChain));
            }

            Expect(!IsKnown(hash));

            var otherChain = this.Runtime.Nexus.FindChainByAddress(sourceChain);

            var tx = otherChain.FindTransactionByHash(hash);
            Expect(tx != null);

            string symbol = null;
            BigInteger amount = 0;
            foreach (var evt in tx.Events)
            {
                if (evt.Kind == EventKind.TokenSend)
                {
                    var data = Serialization.Unserialize<TokenEventData>(evt.Data);
                    if (data.chainAddress == this.Runtime.Chain.Address)
                    {
                        symbol = data.symbol;
                        amount = data.amount;
                    }
                }
            }

            Expect(symbol != null);
            Expect(amount > 0);

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Runtime.Chain.GetTokenBalances(token);

            token.Mint(balances, to, amount);
            Runtime.Notify(EventKind.TokenReceive, to, new TokenEventData() { symbol = symbol, amount = amount, chainAddress = otherChain.Address });

            RegisterHashAsKnown(Runtime.Transaction.Hash);
        }
        #endregion
    }
}
