using System;
using System.Collections.Generic;
using Phantasma.VM;
using Phantasma.Cryptography;
using Phantasma.Core;
using Phantasma.Numerics;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Core.Types;
using Phantasma.Storage.Context;
using Phantasma.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Domain;

namespace Phantasma.Blockchain.Contracts
{
    public class RuntimeVM : VirtualMachine, IRuntime
    {
        public Timestamp Time { get; private set; }
        public Transaction Transaction { get; private set; }
        public Chain Chain { get; private set; }
        public Chain ParentChain { get; private set; }
        public OracleReader Oracle { get; private set; }
        public Nexus Nexus => Chain.Nexus;

        private List<Event> _events = new List<Event>();
        public IEnumerable<Event> Events => _events;

        public Address FeeTargetAddress { get; private set; }

        public StorageChangeSetContext ChangeSet { get; private set; }

        public BigInteger UsedGas { get; private set; }
        public BigInteger PaidGas { get; private set; }
        public BigInteger MaxGas { get; private set; }
        public BigInteger GasPrice { get; private set; }
        public Address GasTarget { get; private set; }
        public bool DelayPayment { get; private set; }
        public readonly bool readOnlyMode;

        private bool isBlockOperation;

        private bool randomized;
        private BigInteger seed;

        public BigInteger MinimumFee;

        public RuntimeVM(byte[] script, Chain chain, Timestamp time, Transaction transaction, StorageChangeSetContext changeSet, OracleReader oracle, bool readOnlyMode, bool delayPayment = false) : base(script)
        {
            Core.Throw.IfNull(chain, nameof(chain));
            Core.Throw.IfNull(changeSet, nameof(changeSet));

            // NOTE: block and transaction can be null, required for Chain.InvokeContract
            //Throw.IfNull(block, nameof(block));
            //Throw.IfNull(transaction, nameof(transaction));

            this.MinimumFee = 1;
            this.GasPrice = 0;
            this.UsedGas = 0;
            this.PaidGas = 0;
            this.GasTarget = chain.Address;
            this.MaxGas = 10000;  // a minimum amount required for allowing calls to Gas contract etc
            this.DelayPayment = delayPayment;

            this.Time = time;
            this.Chain = chain;
            this.Transaction = transaction;
            this.Oracle = oracle;
            this.ChangeSet = changeSet;
            this.readOnlyMode = readOnlyMode;

            this.isBlockOperation = false;
            this.randomized = false;

            this.FeeTargetAddress = Address.Null;

            if (this.Chain != null && !Chain.IsRoot)
            {
                var parentName = chain.Nexus.GetParentChainByName(chain.Name);
                this.ParentChain = chain.Nexus.FindChainByName(parentName);
            }
            else
            {
                this.ParentChain = null;
            }

            Chain.RegisterExtCalls(this);
        }

        public bool IsTrigger => DelayPayment;

        public override string ToString()
        {
            return $"Runtime.Context={CurrentContext}";
        }

        internal void RegisterMethod(string name, Func<RuntimeVM, ExecutionState> handler)
        {
            handlers[name] = handler;
        }

        private Dictionary<string, Func<RuntimeVM, ExecutionState>> handlers = new Dictionary<string, Func<RuntimeVM, ExecutionState>>();

        public override ExecutionState ExecuteInterop(string method)
        {
            Expect(!isBlockOperation, "no interops available in block operations");

            if (handlers.ContainsKey(method))
            {
                return handlers[method](this);
            }

            return ExecutionState.Fault;
        }

        public override ExecutionState Execute()
        {
            var result = base.Execute();

            if (result == ExecutionState.Halt)
            {
                if (readOnlyMode)
                {
                    if (ChangeSet.Any())
                    {
#if DEBUG
                        throw new VMDebugException(this, "VM changeset modified in read-only mode");
#else
                        result = ExecutionState.Fault;
#endif
                    }
                }
                else
                if (PaidGas < UsedGas && Nexus.HasGenesis && !DelayPayment)
                {
#if DEBUG
                    throw new VMDebugException(this, "VM unpaid gas");
#else
                                        result = ExecutionState.Fault;
#endif
                }
            }

            return result;
        }

        public override ExecutionContext LoadContext(string contextName)
        {
            if (isBlockOperation && Nexus.HasGenesis && contextName != Nexus.TokenContractName)
            {
                throw new ChainException($"{contextName} context not available in block operations");
            }

            var contract = this.Nexus.AllocContract(contextName);
            if (contract != null)
            {
                return Chain.GetContractContext(contract);
            }

            return null;
        }

        public VMObject CallContext(string contextName, string methodName, params object[] args)
        {
            var previousContext = CurrentContext;

            var context = LoadContext(contextName);
            Expect(context != null, "could not call context: " + contextName);

            for (int i= args.Length - 1; i>=0; i--)
            {
                var obj = VMObject.FromObject(args[i]);
                this.Stack.Push(obj);
            }

            this.Stack.Push(VMObject.FromObject(methodName));

            CurrentContext = context;
            var temp = context.Execute(this.CurrentFrame, this.Stack);
            Expect(temp == ExecutionState.Halt, "expected call success");
            CurrentContext = previousContext;

            if (this.Stack.Count > 0)
            {
                var result = this.Stack.Pop();
                return result;
            }
            else
            {
                return new VMObject();
            }
        }

        public void Notify<T>(Enum kind, Address address, T content)
        {
            var intVal = (int)(object)kind;
            Notify<T>((EventKind)(EventKind.Custom + intVal), address, content);
        }

        public void Notify<T>(EventKind kind, Address address, T content)
        {
            var bytes = content == null ? new byte[0] : Serialization.Serialize(content);
            Notify(kind, address, bytes);
        }

        public void Notify(EventKind kind, Address address, byte[] bytes)
        {
            var contract = CurrentContext.Name;

            switch (kind)
            {
                case EventKind.GasEscrow:
                    {
                        Expect(contract == Nexus.GasContractName, $"event kind only in {Nexus.GasContractName} contract");

                        var gasInfo = Serialization.Unserialize<GasEventData>(bytes);
                        Expect(gasInfo.price >= this.MinimumFee, "gas fee is too low");
                        this.MaxGas = gasInfo.amount;
                        this.GasPrice = gasInfo.price;
                        this.GasTarget = address;
                        break;
                    }

                case EventKind.GasPayment:
                    {
                        Expect(contract == Nexus.GasContractName, $"event kind only in {Nexus.GasContractName} contract");

                        var gasInfo = Serialization.Unserialize<GasEventData>(bytes);
                        this.PaidGas += gasInfo.amount;

                        if (address != this.Chain.Address)
                        {
                            this.FeeTargetAddress = address;
                        }

                        break;
                    }

                case EventKind.GasLoan:
                    Expect(contract == Nexus.GasContractName, $"event kind only in {Nexus.GasContractName} contract");
                    break;

                case EventKind.BlockCreate:
                case EventKind.BlockClose:
                    Expect(contract == Nexus.BlockContractName, $"event kind only in {Nexus.BlockContractName} contract");

                    isBlockOperation = true;
                    UsedGas = 0;
                    break;

                case EventKind.ValidatorSwitch:
                    Expect(contract == Nexus.BlockContractName, $"event kind only in {Nexus.BlockContractName} contract");
                    break;

                case EventKind.PollCreated:
                case EventKind.PollClosed:
                case EventKind.PollVote:
                    Expect(contract == Nexus.ConsensusContractName, $"event kind only in {Nexus.ConsensusContractName} contract");
                    break;

                case EventKind.ChainCreate:
                case EventKind.TokenCreate:
                case EventKind.FeedCreate:
                    Expect(contract == Nexus.NexusContractName, $"event kind only in {Nexus.NexusContractName} contract");
                    break;

                case EventKind.FileCreate:
                case EventKind.FileDelete:
                    Expect(contract == Nexus.StorageContractName, $"event kind only in {Nexus.StorageContractName } contract");
                    break;

                case EventKind.ValidatorAdd:
                case EventKind.ValidatorRemove:
                    Expect(contract == Nexus.ValidatorContractName, $"event kind only in {Nexus.ValidatorContractName} contract");
                    break;

                case EventKind.BrokerRequest:
                    Expect(contract == Nexus.InteropContractName, $"event kind only in {Nexus.InteropContractName} contract");
                    break;

                case EventKind.ValueCreate:
                case EventKind.ValueUpdate:
                    Expect(contract == Nexus.GovernanceContractName, $"event kind only in {Nexus.GovernanceContractName} contract");
                    break;
            }

            var evt = new Event(kind, address, contract, bytes);
            _events.Add(evt);
        }

        public void Expect(bool condition, string description)
        {
#if DEBUG
            if (!condition)
            {
                throw new VMDebugException(this, description);
            }
#endif

            Core.Throw.If(!condition, $"contract assertion failed: {description}");
        }

        #region GAS
        public override ExecutionState ValidateOpcode(Opcode opcode)
        {
            // required for allowing transactions to occur pre-minting of native token
            if (readOnlyMode || !Nexus.HasGenesis)
            {
                return ExecutionState.Running;
            }

            var gasCost = GetGasCostForOpcode(opcode);
            return ConsumeGas(gasCost);
        }

        public ExecutionState ConsumeGas(BigInteger gasCost)
        {
            if (gasCost == 0 || isBlockOperation)
            {
                return ExecutionState.Running;
            }

            if (gasCost < 0)
            {
                Core.Throw.If(gasCost < 0, "invalid gas amount");
            }

            // required for allowing transactions to occur pre-minting of native token
            if (readOnlyMode || !Nexus.HasGenesis)
            {
                return ExecutionState.Running;
            }

            UsedGas += gasCost;

            if (UsedGas > MaxGas && !DelayPayment)
            {
#if DEBUG
                throw new VMDebugException(this, "VM gas limit exceeded");
#else
                                return ExecutionState.Fault;
#endif
            }

            return ExecutionState.Running;
        }

        public static BigInteger GetGasCostForOpcode(Opcode opcode)
        {
            switch (opcode)
            {
                case Opcode.GET:
                case Opcode.PUT:
                case Opcode.CALL:
                case Opcode.LOAD:
                    return 2;

                case Opcode.EXTCALL:
                    return 3;

                case Opcode.CTX:
                    return 5;

                case Opcode.SWITCH:
                    return 10;

                case Opcode.NOP:
                case Opcode.RET:
                    return 0;

                default: return 1;
            }
        }
        #endregion

        #region ORACLES
        // returns value in FIAT token
        public BigInteger GetTokenPrice(string symbol)
        {
            if (symbol == Nexus.FiatTokenSymbol)
            {
                return UnitConversion.GetUnitValue(Nexus.FiatTokenDecimals);
            }

            if (symbol == Nexus.FuelTokenSymbol)
            {
                var result = GetTokenPrice(Nexus.StakingTokenSymbol);
                result /= 5;
                return result;
            }

            Core.Throw.If(Oracle == null, "cannot read price from null oracle");

            Core.Throw.If(!Nexus.TokenExists(symbol), "cannot read price for invalid token");

            var bytes = Oracle.Read("price://" + symbol);
            var value = BigInteger.FromUnsignedArray(bytes, true);
            return value;
        }

        public BigInteger GetTokenQuote(string baseSymbol, string quoteSymbol, BigInteger amount)
        {
            if (baseSymbol == quoteSymbol)
                return amount;

            var basePrice = GetTokenPrice(baseSymbol);
            var quotePrice = GetTokenPrice(quoteSymbol);

            BigInteger result;

            var baseToken = Nexus.GetTokenInfo(baseSymbol);
            var quoteToken = Nexus.GetTokenInfo(quoteSymbol);

            result = basePrice * amount;
            result = UnitConversion.ConvertDecimals(result, baseToken.Decimals, Nexus.FiatTokenDecimals);

            result /= quotePrice;

            result = UnitConversion.ConvertDecimals(result, Nexus.FiatTokenDecimals, quoteToken.Decimals);

            return result;
        }
        #endregion

        #region RANDOM NUMBERS
        public static readonly uint RND_A = 16807;
        public static readonly uint RND_M = 2147483647;

        // returns a next random number
        public BigInteger GetRandomNumber()
        {
            if (!randomized)
            {
                // calculates first initial pseudo random number seed
                byte[] bytes = Transaction != null ? Transaction.Hash.ToByteArray() : new byte[32];

                for (int i = 0; i < this.entryScript.Length; i++)
                {
                    var index = i % bytes.Length;
                    bytes[index] ^= entryScript[i];
                }

                var time = System.BitConverter.GetBytes(Time.Value);

                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] ^= time[i % time.Length];
                }

                seed = BigInteger.FromUnsignedArray(bytes, true);
                randomized = true;
            }
            else
            {
                seed = ((RND_A * seed) % RND_M);
            }

            return seed;
        }
        #endregion

        // fetches a chain-governed value
        public BigInteger GetGovernanceValue(string name)
        {
            var value = Nexus.RootChain.InvokeContract(Nexus.GovernanceContractName, "GetValue", name).AsNumber();
            return value;
        }

        public BigInteger GetBalance(string tokenSymbol, Address address)
        {
            var tokenInfo = Nexus.GetTokenInfo(tokenSymbol);
            if (tokenInfo.Flags.HasFlag(TokenFlags.Fungible))
            {
                var balances = new BalanceSheet(tokenSymbol);
                return balances.Get(this.ChangeSet, address);
            }
            else
            {
                var ownerships = new OwnershipSheet(tokenSymbol);
                var items = ownerships.Get(this.ChangeSet, address);
                return items.Length;
            }
        }

        public IBlock GetBlockByHash(IChain chain, Hash hash)
        {
            throw new NotImplementedException();
        }

        public IBlock GetBlockByHeight(IChain chain, BigInteger height)
        {
            throw new NotImplementedException();
        }

        public ITransaction GetTransaction(IChain chain, Hash hash)
        {
            throw new NotImplementedException();
        }

        public IToken GetToken(string symbol)
        {
            throw new NotImplementedException();
        }

        public IFeed GetFeed(string name)
        {
            throw new NotImplementedException();
        }

        public IPlatform GetPlatform(string name)
        {
            throw new NotImplementedException();
        }

        public IChain GetChainByAddress(Address address)
        {
            throw new NotImplementedException();
        }

        public IChain GetChainByName(string name)
        {
            throw new NotImplementedException();
        }

        public void Log(string description)
        {
            throw new NotImplementedException();
        }

        public void Throw(string description)
        {
            throw new NotImplementedException();
        }

        public void Notify(EventKind kind, Address address, VMObject content)
        {
            throw new NotImplementedException();
        }

        public IEvent GetTransactionEvents(ITransaction transaction)
        {
            throw new NotImplementedException();
        }

        public BigInteger GetBalance(IChain chain, IToken token, Address address)
        {
            throw new NotImplementedException();
        }
    }
}
