using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using Phantasma.VM;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using Phantasma.Core.Performance;
using Phantasma.Storage.Context;
using Phantasma.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Domain;

namespace Phantasma.Blockchain
{
    public class RuntimeVM : GasMachine, IRuntime
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
        public BigInteger PaidGas { get; private set; }
        public BigInteger MaxGas { get; private set; }
        public BigInteger GasPrice { get; private set; }

        private bool gasPaymentInProgress = false;

        public int TransactionIndex { get; private set; }
        public Address GasTarget { get; private set; }
        public bool DelayPayment { get; private set; }
        public readonly bool readOnlyMode;

        private bool randomized;
        private BigInteger seed;

        public BigInteger MinimumFee;

        private readonly StorageChangeSetContext changeSet;

        internal StorageContext RootStorage => this.IsRootChain() ? this.Storage : Nexus.RootStorage;

        public RuntimeVM(int index, byte[] script, Chain chain, Timestamp time, Transaction transaction, StorageChangeSetContext changeSet, OracleReader oracle, bool readOnlyMode, bool delayPayment = false) : base(script)
        {
            Core.Throw.IfNull(chain, nameof(chain));
            Core.Throw.IfNull(changeSet, nameof(changeSet));

            // NOTE: block and transaction can be null, required for Chain.InvokeContract
            //Throw.IfNull(block, nameof(block));
            //Throw.IfNull(transaction, nameof(transaction));

            this.TransactionIndex = index;
            this.MinimumFee = 1;
            this.GasPrice = 0;
            this.PaidGas = 0;
            this.GasTarget = chain.Address;
            this.MaxGas = 10000;  // a minimum amount required for allowing calls to Gas contract etc
            this.DelayPayment = delayPayment;

            this.Time = time;
            this.Chain = chain;
            this.Transaction = transaction;
            this.Oracle = oracle;
            this.changeSet = changeSet;
            this.readOnlyMode = readOnlyMode;

            this.randomized = false;

            this.FeeTargetAddress = Address.Null;

            if (this.Chain != null && !Chain.IsRoot)
            {
                var parentName = chain.Nexus.GetParentChainByName(chain.Name);
                this.ParentChain = chain.Nexus.GetChainByName(parentName);
            }
            else
            {
                this.ParentChain = null;
            }

            ExtCalls.RegisterWithRuntime(this);
        }

        public bool IsTrigger => DelayPayment;

        IChain IRuntime.Chain => this.Chain;

        ITransaction IRuntime.Transaction => this.Transaction;

        public StorageContext Storage => this.changeSet;

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
            BigInteger gasCost;

            // construtor
            if (method.EndsWith("()"))
            {
                gasCost = 10;
            }
            else
            {
                int dotPos = method.IndexOf('.');
                Expect(dotPos > 0, "extcall is missing namespace");

                var methodNamespace = method.Substring(0, dotPos);
                switch (methodNamespace)
                {
                    case "Runtime":
                    case "Data":
                    case "Map":
                    case "List":
                    case "Set":
                        gasCost = 50;
                        break;

                    case "Nexus":
                        gasCost = 1000;
                        break;

                    case "Organization":
                    case "Oracle":
                        gasCost = 200;
                        break;

                    case "Leaderboard":
                        gasCost = 100;
                        break;

                    default:
                        Expect(false, "invalid extcall namespace: " + methodNamespace);
                        gasCost = 0;
                        break;
                }
            }

            if (gasCost > 0)
            {
                ConsumeGas(gasCost);
            }

            if (handlers.ContainsKey(method))
            {
                using (var m = new ProfileMarker(method))
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
                    if (changeSet.Any())
                    {
                        throw new VMException(this, "VM changeset modified in read-only mode");
                    }
                }
                else
                if (PaidGas < UsedGas && Nexus.HasGenesis && !DelayPayment)
                {
                    throw new VMException(this, "VM unpaid gas");
                }
            }

            return result;
        }

        public override ExecutionContext LoadContext(string contextName)
        {
            var contract = this.Chain.GetContractByName(this.Storage, contextName);
            if (contract != null)
            {
                return Chain.GetContractContext(this.changeSet, contract);
            }

            return null;
        }

        public VMObject CallContext(string contextName, string methodName, params object[] args)
        {
            var previousContext = CurrentContext;
            var previousCaller = this.EntryAddress;

            var context = LoadContext(contextName);
            Expect(context != null, "could not call context: " + contextName);

            for (int i = args.Length - 1; i >= 0; i--)
            {
                var obj = VMObject.FromObject(args[i]);
                this.Stack.Push(obj);
            }

            this.Stack.Push(VMObject.FromObject(methodName));

            BigInteger savedGas = this.UsedGas;

            this.EntryAddress = CurrentContext.Address;
            CurrentContext = context;

            _activeAddresses.Push(context.Address);

            var temp = context.Execute(this.CurrentFrame, this.Stack);
            Expect(temp == ExecutionState.Halt, "expected call success");

            CurrentContext = previousContext;
            this.EntryAddress = previousCaller;

            var temp2 = _activeAddresses.Pop();
            if (temp2 != context.Address)
            {
                throw new VMException(this, "runtimeVM implementation bug detected: address stack");
            }

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

                        if (address.IsNull)
                        {
                            Expect(gasPaymentInProgress, $"gas payment not in progress");
                            this.gasPaymentInProgress = false;
                            return; // this event should be ignored, it is mostly an hack
                        }
                        else
                        {
                            Expect(!gasPaymentInProgress, $"gas payment already in progress");
                            this.gasPaymentInProgress = true;

                            var gasInfo = Serialization.Unserialize<GasEventData>(bytes);
                            this.PaidGas += gasInfo.amount;

                            if (address != this.Chain.Address)
                            {
                                this.FeeTargetAddress = address;
                            }
                        }
                        break;
                    }

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
                    Expect(this.IsRootChain(), $"event kind only in root chain");
                    break;

                case EventKind.FileCreate:
                case EventKind.FileDelete:
                    Expect(contract == Nexus.StorageContractName, $"event kind only in {Nexus.StorageContractName } contract");
                    break;

                case EventKind.ValidatorPropose:
                case EventKind.ValidatorElect:
                case EventKind.ValidatorRemove:
                    Expect(contract == Nexus.ValidatorContractName, $"event kind only in {Nexus.ValidatorContractName} contract");
                    break;

                case EventKind.ValueCreate:
                case EventKind.ValueUpdate:
                    Expect(contract == Nexus.GovernanceContractName, $"event kind only in {Nexus.GovernanceContractName} contract");
                    break;

                case EventKind.Inflation:

                    var inflationSymbol = Serialization.Unserialize<string>(bytes);

                    if (inflationSymbol == DomainSettings.StakingTokenSymbol)
                    {
                        Expect(contract == Nexus.GasContractName, $"event kind only in {Nexus.GasContractName} contract");
                    }
                    else
                    {
                        Expect(inflationSymbol != DomainSettings.FuelTokenSymbol, $"{inflationSymbol} cannot have inflation event");
                    }

                    break;
            }

            var evt = new Event(kind, address, contract, bytes);
            _events.Add(evt);
        }

        public void Expect(bool condition, string description)
        {
            if (condition)
            {
                return;
            }

            var callingFrame = new StackFrame(1);
            var method = callingFrame.GetMethod();

#if DEBUG
            if (this.Transaction != null)
                description += $", tx: {this.Transaction.Hash}";
            else
#endif
            description = $"{description} @ {method.Name}";

            throw new VMException(this, description);
        }

        #region GAS
        public override ExecutionState ValidateOpcode(Opcode opcode)
        {
            // required for allowing transactions to occur pre-minting of native token
            if (readOnlyMode || !Nexus.HasGenesis)
            {
                return ExecutionState.Running;
            }

            return base.ValidateOpcode(opcode);
        }

        public override ExecutionState ConsumeGas(BigInteger gasCost)
        {
            if (gasCost == 0 || gasPaymentInProgress)
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

            var result = base.ConsumeGas(gasCost);

            if (UsedGas > MaxGas && !DelayPayment)
            {
                throw new VMException(this, $"VM gas limit exceeded ({MaxGas})/({UsedGas})");
            }

            return result;
        }
        #endregion

        #region ORACLES
        // returns value in FIAT token
        public BigInteger GetTokenPrice(string symbol)
        {
            if (symbol == DomainSettings.FiatTokenSymbol)
            {
                return UnitConversion.GetUnitValue(DomainSettings.FiatTokenDecimals);
            }

            Core.Throw.If(!Nexus.TokenExists(RootStorage, symbol), "cannot read price for invalid token");
            var token = GetToken(symbol);

            Core.Throw.If(Oracle == null, "cannot read price from null oracle");
            var bytes = Oracle.Read<byte[]>(this.Time, "price://" + symbol);
            var value = BigInteger.FromUnsignedArray(bytes, true);

            Expect(value > 0, "token price not available for " + symbol);

            return value;
        }
        #endregion

        #region RANDOM NUMBERS
        public static readonly uint RND_A = 16807;
        public static readonly uint RND_M = 2147483647;

        // returns a next random number
        public BigInteger GenerateRandomNumber()
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
            var value = Nexus.GetGovernanceValue(this.RootStorage, name);
            return value;
        }

        #region TRIGGERS
        public bool InvokeTriggerOnAccount(Address address, AccountTrigger trigger, params object[] args)
        {
            if (address.IsNull)
            {
                return false;
            }

            if (address.IsUser)
            {

                //var accountScript = Nexus.LookUpAddressScript(RootStorage, address);
                var accountScript = OptimizedAddressScriptLookup(address);

                //Expect(accountScript.SequenceEqual(accountScript2), "different account scripts");
                return InvokeTrigger(accountScript, trigger.ToString(), args);
            }

            if (address.IsSystem)
            {
                var contract = Nexus.GetContractByAddress(address);
                if (contract != null)
                {
                    var triggerName = trigger.ToString();
                    if (contract.HasInternalMethod(triggerName))
                    {
                        CallContext(contract.Name, triggerName, args);
                    }
                }

                return true;
            }

            return true;
        }

        private byte[] OptimizedAddressScriptLookup(Address target)
        {
            var scriptMapKey = Encoding.UTF8.GetBytes($".{Nexus.AccountContractName}._scriptMap");

            var scriptMap = new StorageMap(scriptMapKey, RootStorage);

            if (scriptMap.ContainsKey(target))
                return scriptMap.Get<Address, byte[]>(target);
            else
                return new byte[0];

        }

        public bool InvokeTriggerOnToken(TokenInfo token, TokenTrigger trigger, params object[] args)
        {
            return InvokeTrigger(token.Script, trigger.ToString(), args);
        }

        public bool InvokeTrigger(byte[] script, string triggerName, params object[] args)
        {
            if (script == null || script.Length == 0)
            {
                return true;
            }

            var leftOverGas = (uint)(this.MaxGas - this.UsedGas);
            var runtime = new RuntimeVM(-1, script, this.Chain, this.Time, this.Transaction, this.changeSet, this.Oracle, false, true);
            runtime.ThrowOnFault = true;

            for (int i = args.Length - 1; i >= 0; i--)
            {
                var obj = VMObject.FromObject(args[i]);
                runtime.Stack.Push(obj);
            }
            runtime.Stack.Push(VMObject.FromObject(triggerName));

            var state = runtime.Execute();
            // TODO catch VM exceptions?

            // propagate gas consumption
            // TODO this should happen not here but in real time during previous execution, to prevent gas attacks
            this.ConsumeGas(runtime.UsedGas);

            if (state == ExecutionState.Halt)
            {
                // propagate events to the other runtime
                foreach (var evt in runtime.Events)
                {
                    this.Notify(evt.Kind, evt.Address, evt.Data);
                }

                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion

        private HashSet<Address> validatedWitnesses = new HashSet<Address>();

        public bool IsWitness(Address address)
        {
            if (address.IsInterop)
            {
                return false;
            }

            if (address == Address.Null)
            {
                return false;
            }

            if (address == this.Chain.Address /*|| address == this.Address*/)
            {
                return false;
            }

            using (var m = new ProfileMarker("validatedWitnesses.Contains"))
            if (validatedWitnesses.Contains(address))
            {
                return true;
            }

            if (address.IsSystem)
            {
                foreach (var activeAddress in this.ActiveAddresses)
                {
                    if (activeAddress == address)
                    {
                        return true;
                    }
                }

                var org = Nexus.GetOrganizationByAddress(RootStorage, address);
                if (org != null)
                {
                    this.ConsumeGas(10000);
                    var result = org.IsWitness(Transaction);

                    if (result)
                    {
                        validatedWitnesses.Add(address);
                    }

                    return result;
                }
                else
                {
                    return address == CurrentContext.Address;
                }
            }

            if (this.Transaction == null)
            {
                return false;
            }

            bool accountResult;

            if (address.IsUser && Nexus.HasGenesis && OptimizedHasAddressScript(RootStorage, address))
            {
                using (var m = new ProfileMarker("InvokeTriggerOnAccount"))
                    accountResult = InvokeTriggerOnAccount(address, AccountTrigger.OnWitness, address);
            }
            else
            {
                using (var m = new ProfileMarker("Transaction.IsSignedBy"))
                    accountResult = this.Transaction.IsSignedBy(address);
            }

            // workaround for supporting txs done in older nodes
            if (!accountResult && this.IsRootChain() && this.Chain.Height < 50000)
            {
                accountResult = this.Transaction.IsSignedBy(this.GenesisAddress);
            }

            if (accountResult)
            {
                validatedWitnesses.Add(address);
            }

            return accountResult;
        }

        bool OptimizedHasAddressScript(StorageContext context, Address address)
        {
            var scriptMapKey = Encoding.UTF8.GetBytes($".{Nexus.AccountContractName}._scriptMap");

            var scriptMap = new StorageMap(scriptMapKey, context);

            if (address.IsUser)
            {
                return scriptMap.ContainsKey(address);
            }

            return false;

        }

        public IBlock GetBlockByHash(Hash hash)
        {
            return this.Chain.GetBlockByHash(hash);
        }

        public IBlock GetBlockByHeight(BigInteger height)
        {
            var hash = this.Chain.GetBlockHashAtHeight(height);
            return GetBlockByHash(hash);
        }

        public ITransaction GetTransaction(Hash hash)
        {
            return this.Chain.GetTransactionByHash(hash);
        }

        public IContract GetContract(string name)
        {
            throw new NotImplementedException();
        }

        public bool TokenExists(string symbol)
        {
            return Nexus.TokenExists(RootStorage, symbol);
        }

        public bool TokenExists(string symbol, string platform)
        {
            return Nexus.TokenExistsOnPlatform(symbol, platform, RootStorage);
        }

        public bool FeedExists(string name)
        {
            return Nexus.FeedExists(RootStorage, name);
        }

        public bool PlatformExists(string name)
        {
            return Nexus.PlatformExists(RootStorage, name);
        }

        public bool ContractExists(string name)
        {
            return Nexus.ContractExists(RootStorage, name);
        }

        public bool ContractDeployed(string name)
        {
            return Chain.IsContractDeployed(this.Storage, name);
        }

        public bool ArchiveExists(Hash hash)
        {
            return Nexus.ArchiveExists(hash);
        }

        public IArchive GetArchive(Hash hash)
        {
            return Nexus.GetArchive(hash);
        }

        public bool DeleteArchive(Hash hash)
        {
            var archive = Nexus.GetArchive(hash);
            if (archive == null)
            {
                return false;
            }
            return Nexus.DeleteArchive(archive);
        }

        public bool ChainExists(string name)
        {
            return Nexus.ChainExists(this.RootStorage, name);
        }

        public int GetIndexOfChain(string name)
        {
            return Nexus.GetIndexOfChain(name);
        }

        public IChain GetChainParent(string name)
        {
            var parentName = Nexus.GetParentChainByName(name);
            return this.GetChainByName(parentName);
        }

        public Address LookUpName(string name)
        {
            return Nexus.LookUpName(this.RootStorage, name);
        }

        public bool HasAddressScript(Address from)
        {
            return Nexus.HasAddressScript(this.RootStorage, from);
        }

        public byte[] GetAddressScript(Address from)
        {
            return Nexus.LookUpAddressScript(this.RootStorage, from);
        }

        public string GetAddressName(Address from)
        {
            return Nexus.LookUpAddressName(this.RootStorage, from);
        }

        public Event[] GetTransactionEvents(Hash transactionHash)
        {
            var blockHash = Chain.GetBlockHashOfTransaction(transactionHash);
            var block = Chain.GetBlockByHash(blockHash);
            Expect(block != null, "block not found for this transaction");
            return block.GetEventsForTransaction(transactionHash);
        }

        public Hash[] GetTransactionHashesForAddress(Address address)
        {
            return Chain.GetTransactionHashesForAddress(address);
        }

        public ValidatorEntry GetValidatorByIndex(int index)
        {
            return Nexus.GetValidatorByIndex(index);
        }

        public ValidatorEntry[] GetValidators()
        {
            return Nexus.GetValidators();
        }

        public bool IsPrimaryValidator(Address address)
        {
            return Nexus.IsPrimaryValidator(address);
        }

        public bool IsSecondaryValidator(Address address)
        {
            return Nexus.IsSecondaryValidator(address);
        }

        public int GetPrimaryValidatorCount()
        {
            return Nexus.GetPrimaryValidatorCount();
        }

        public int GetSecondaryValidatorCount()
        {
            return Nexus.GetSecondaryValidatorCount();
        }

        public bool IsKnownValidator(Address address)
        {
            return Nexus.IsKnownValidator(address);
        }

        public bool IsStakeMaster(Address address)
        {
            return Nexus.IsStakeMaster(this.RootStorage, address);
        }

        public BigInteger GetStake(Address address)
        {
            return Nexus.GetStakeFromAddress(this.RootStorage, address);
        }

        private static readonly byte[] uuidKey = System.Text.Encoding.UTF8.GetBytes(".uuid");

        public BigInteger GenerateUID()
        {
            BigInteger uuid;
            if (Storage.Has(uuidKey))
            {
                uuid = Storage.Get<BigInteger>(uuidKey);
                uuid += 1;
            }
            else
            {
                uuid = 1;
            }

            Storage.Put(uuidKey, uuid);
            return uuid;
        }

        public BigInteger GetBalance(string symbol, Address address)
        {
            Expect(TokenExists(symbol), $"Token does not exist ({symbol})");
            var token = GetToken(symbol);
            return Chain.GetTokenBalance(this.Storage, token, address);
        }

        public BigInteger[] GetOwnerships(string symbol, Address address)
        {
            Expect(TokenExists(symbol), $"Token does not exist ({symbol})");
            return Chain.GetOwnedTokens(this.Storage, symbol, address);
        }

        public BigInteger GetTokenSupply(string symbol)
        {
            Expect(TokenExists(symbol), $"Token does not exist ({symbol})");
            return Chain.GetTokenSupply(this.Storage, symbol);
        }

        public void SetTokenPlatformHash(string symbol, string platform, Hash hash)
        {
            var Runtime = this;
            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            Runtime.Expect(Runtime.IsWitness(Runtime.GenesisAddress), "invalid witness, must be genesis");

            Runtime.Expect(platform != DomainSettings.PlatformName, "external token chain required");
            Runtime.Expect(hash != Hash.Null, "hash cannot be null");

            var pow = Runtime.Transaction.Hash.GetDifficulty();
            Runtime.Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            Runtime.Expect(Runtime.PlatformExists(platform), "platform not found");

            Runtime.Expect(!string.IsNullOrEmpty(symbol), "token symbol required");
            Runtime.Expect(ValidationUtils.IsValidTicker(symbol), "invalid symbol");
            //Runtime.Expect(!Runtime.TokenExists(symbol, platform), $"token {symbol}/{platform} already exists");

            Runtime.Expect(!string.IsNullOrEmpty(platform), "chain name required");

            Nexus.SetTokenPlatformHash(symbol, platform, hash, this.RootStorage);
        }

        public void CreateToken(Address from, string symbol, string name, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script)
        {
            var Runtime = this;
            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            var pow = Runtime.Transaction.Hash.GetDifficulty();
            Runtime.Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            Runtime.Expect(!string.IsNullOrEmpty(symbol), "token symbol required");
            Runtime.Expect(!string.IsNullOrEmpty(name), "token name required");

            Runtime.Expect(ValidationUtils.IsValidTicker(symbol), "invalid symbol");
            Runtime.Expect(!Runtime.TokenExists(symbol), "token already exists");

            Runtime.Expect(maxSupply >= 0, "token supply cant be negative");
            Runtime.Expect(decimals >= 0, "token decimals cant be negative");
            Runtime.Expect(decimals <= DomainSettings.MAX_TOKEN_DECIMALS, $"token decimals cant exceed {DomainSettings.MAX_TOKEN_DECIMALS}");

            if (symbol == DomainSettings.FuelTokenSymbol)
            {
                Runtime.Expect(flags.HasFlag(TokenFlags.Fuel), "token should be native");
            }
            else
            {
                Runtime.Expect(!flags.HasFlag(TokenFlags.Fuel), "token can't be native");
            }

            if (symbol == DomainSettings.StakingTokenSymbol)
            {
                Runtime.Expect(flags.HasFlag(TokenFlags.Stakable), "token should be stakable");
            }

            if (symbol == DomainSettings.FiatTokenSymbol)
            {
                Runtime.Expect(flags.HasFlag(TokenFlags.Fiat), "token should be fiat");
            }

            if (!flags.HasFlag(TokenFlags.Fungible))
            {
                Runtime.Expect(!flags.HasFlag(TokenFlags.Divisible), "non-fungible token must be indivisible");
            }

            if (flags.HasFlag(TokenFlags.Divisible))
            {
                Runtime.Expect(decimals > 0, "divisible token must have decimals");
            }
            else
            {
                Runtime.Expect(decimals == 0, "indivisible token can't have decimals");
            }

            Runtime.Expect(from.IsUser, "owner address must be user address");
            Runtime.Expect(Runtime.IsStakeMaster(from), "needs to be master");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Nexus.CreateToken(RootStorage, symbol, name, maxSupply, decimals, flags, script);
            Runtime.Notify(EventKind.TokenCreate, from, symbol);
        }

        public void CreateChain(Address creator, string organization, string name, string parentName)
        {
            var Runtime = this;
            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            var pow = Runtime.Transaction.Hash.GetDifficulty();
            Runtime.Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            Runtime.Expect(!string.IsNullOrEmpty(name), "name required");
            Runtime.Expect(!string.IsNullOrEmpty(parentName), "parent chain required");

            Runtime.Expect(Runtime.OrganizationExists(organization), "invalid organization");
            var org = Runtime.GetOrganization(organization);
            Runtime.Expect(org.IsMember(creator), "creator does not belong to organization");

            Runtime.Expect(creator.IsUser, "owner address must be user address");
            Runtime.Expect(Runtime.IsStakeMaster(creator), "needs to be master");
            Runtime.Expect(Runtime.IsWitness(creator), "invalid witness");

            name = name.ToLowerInvariant();
            Runtime.Expect(!name.Equals(parentName, StringComparison.OrdinalIgnoreCase), "same name as parent");

            Nexus.CreateChain(RootStorage, organization, name, parentName);
            Runtime.Notify(EventKind.ChainCreate, creator, name);
        }

        public void CreateFeed(Address owner, string name, FeedMode mode)
        {
            var Runtime = this;
            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            var pow = Runtime.Transaction.Hash.GetDifficulty();
            Runtime.Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            Runtime.Expect(!string.IsNullOrEmpty(name), "name required");

            Runtime.Expect(owner.IsUser, "owner address must be user address");
            Runtime.Expect(Runtime.IsStakeMaster(owner), "needs to be master");
            Runtime.Expect(Runtime.IsWitness(owner), "invalid witness");

            Runtime.Expect(Nexus.CreateFeed(RootStorage, owner, name, mode), "feed creation failed");

            Runtime.Notify(EventKind.FeedCreate, owner, name);
        }

        public BigInteger CreatePlatform(Address from, string name, string externalAddress, Address interopAddress, string fuelSymbol)
        {
            var Runtime = this;
            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            Runtime.Expect(from == Runtime.GenesisAddress, "(CreatePlatform) must be genesis");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(ValidationUtils.IsValidIdentifier(name), "invalid platform name");

            var platformID = Nexus.CreatePlatform(RootStorage, externalAddress, interopAddress, name, fuelSymbol);
            Runtime.Expect(platformID > 0, $"creation of platform with id {platformID} failed");

            Runtime.Notify(EventKind.PlatformCreate, from, name);
            return platformID;
        }

        public void CreateOrganization(Address from, string ID, string name, byte[] script)
        {
            var Runtime = this;
            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            Runtime.Expect(from == Runtime.GenesisAddress, $"(CreateOrganization) must be genesis from: {from} genesis: {Runtime.GenesisAddress}");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(ValidationUtils.IsValidIdentifier(ID), "invalid organization name");

            Runtime.Expect(!Nexus.OrganizationExists(RootStorage, ID), "organization already exists");

            Nexus.CreateOrganization(RootStorage, ID, name, script);

            Runtime.Notify(EventKind.OrganizationCreate, from, ID);
        }

        public IArchive CreateArchive(Address creator, Address owner, MerkleTree merkleTree, BigInteger size, ArchiveFlags flags, byte[] key)
        {
            // TODO validation
            return Nexus.CreateArchive(this.RootStorage, merkleTree, size, flags, key);
        }

        public bool WriteArchive(IArchive archive, int blockIndex, byte[] content)
        {
            if (archive == null)
            {
                return false;
            }

            var blockCount = (int)archive.GetBlockCount();
            if (blockIndex < 0 || blockIndex >= blockCount)
            {
                return false; 
            }

            Nexus.WriteArchiveBlock((Archive)archive, blockIndex, content);
            return true;
        }

        public bool IsAddressOfParentChain(Address address)
        {
            if (this.IsRootChain())
            {
                return false;
            }

            var parentName = Nexus.GetParentChainByName(Chain.Name);
            var target = Nexus.GetChainByAddress(address);
            return target.Name == parentName;
        }

        public bool IsAddressOfChildChain(Address address)
        {
            var parentName = Nexus.GetParentChainByAddress(address);
            return Chain.Name == parentName;
        }

        public bool IsNameOfParentChain(string name)
        {
            if (this.IsRootChain())
            {
                return false;
            }

            var parentName = Nexus.GetParentChainByName(Chain.Name);
            return name == parentName;
        }

        public bool IsNameOfChildChain(string name)
        {
            var parentName = Nexus.GetParentChainByName(name);
            return Chain.Name == parentName;
        }

        public void MintTokens(string symbol, Address from, Address target, BigInteger amount)
        {
            var Runtime = this;

            // TODO should not be necessary, verified by trigger
            //Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");
            IToken token;
            using (var m = new ProfileMarker("Runtime.GetToken"))
                token = Runtime.GetToken(symbol);
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fiat), "token can't be fiat");

            using (var m = new ProfileMarker("Nexus.MintTokens"))
                Nexus.MintTokens(this, token, from, target, Chain.Name, amount);
        }

        public BigInteger MintToken(string symbol, Address from, Address target, byte[] rom, byte[] ram)
        {
            var Runtime = this;
            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");
            IToken token;
            using (var m = new ProfileMarker("Runtime.GetToken"))
                token = Runtime.GetToken(symbol);
            Runtime.Expect(!token.IsFungible(), "token must be non-fungible");
            // TODO should not be necessary, verified by trigger
            //Runtime.Expect(IsWitness(target), "invalid witness");

            Runtime.Expect(Runtime.IsRootChain(), "can only mint nft in root chain");

            Runtime.Expect(rom.Length <= TokenContent.MaxROMSize, "ROM size exceeds maximum allowed");
            Runtime.Expect(ram.Length <= TokenContent.MaxRAMSize, "RAM size exceeds maximum allowed");

            BigInteger tokenID;
            using (var m = new ProfileMarker("Nexus.CreateNFT"))
                tokenID = Nexus.CreateNFT(this, symbol, Runtime.Chain.Name, target, rom, ram);
            Runtime.Expect(tokenID > 0, "invalid tokenID");

            using (var m = new ProfileMarker("Nexus.MintToken"))
                Nexus.MintToken(this, token, from, target, Chain.Name, tokenID, rom, ram);

            return tokenID;
        }

        public void BurnTokens(string symbol, Address target, BigInteger amount)
        {
            var Runtime = this;
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(IsWitness(target), "invalid witness");

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");
            var token = Runtime.GetToken(symbol);
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(token.IsBurnable(), "token must be burnable");
            Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fiat), "token can't be fiat");

            Nexus.BurnTokens(this, token, target, target, Chain.Name, amount);
        }

        public void BurnToken(string symbol, Address target, BigInteger tokenID)
        {
            var Runtime = this;
            Runtime.Expect(IsWitness(target), "invalid witness");

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");
            var token = Runtime.GetToken(symbol);
            Runtime.Expect(!token.IsFungible(), "token must be non-fungible");
            Runtime.Expect(token.IsBurnable(), "token must be burnable");

            Nexus.BurnToken(this, token, target, target, Chain.Name, tokenID);
        }

        public void TransferTokens(string symbol, Address source, Address destination, BigInteger amount)
        {
            var Runtime = this;
            if (ProtocolVersion >= 3)
            {
                Runtime.Expect(!source.IsNull, "invalid source");
            }

            if (source == destination || amount == 0)
            {
                return;
            }

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");
            var token = Runtime.GetToken(symbol);

            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(IsWitness(source), "invalid witness");
            Runtime.Expect(!Runtime.IsTrigger, "not allowed inside a trigger");

            if (destination.IsInterop)
            {
                Runtime.Expect(Runtime.Chain.IsRoot, "interop transfers only allowed in main chain");
                Runtime.CallContext("interop", "WithdrawTokens", source, destination, symbol, amount);
                return;
            }

            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");

            Runtime.Nexus.TransferTokens(Runtime, token, source, destination, amount);
        }

        public void TransferToken(string symbol, Address source, Address destination, BigInteger tokenID)
        {
            var Runtime = this;
            Runtime.Expect(IsWitness(source), "invalid witness");

            Runtime.Expect(source != destination, "source and destination must be different");

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");

            var token = Runtime.GetToken(symbol);
            Runtime.Expect(!token.IsFungible(), "token must be non-fungible");

            Nexus.TransferToken(this, token, source, destination, tokenID);
        }

        public void SwapTokens(string sourceChain, Address from, string targetChain, Address to, string symbol, BigInteger value, byte[] rom, byte[] ram)
        {
            var Runtime = this;

            Runtime.Expect(sourceChain != targetChain, "source chain and target chain must be different");
            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");

            var token = Runtime.GetToken(symbol);
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "must be transferable token");

            if (token.IsFungible())
            {
                Runtime.Expect(rom == null, "fungible tokens cant have rom");
                Runtime.Expect(ram == null, "fungible tokens cant have ram");
            }
            else
            {
                Runtime.Expect(rom != null & rom.Length > 0, "nft rom is missing");
                Runtime.Expect(ram != null, "nft ram is missing");
            }

            if (PlatformExists(sourceChain))
            {
                Runtime.Expect(sourceChain != DomainSettings.PlatformName, "invalid platform as source chain");

                if (token.IsFungible())
                {
                    Nexus.MintTokens(this, token, from, to, sourceChain, value);
                }
                else
                {
                    Nexus.MintToken(this, token, from, to, sourceChain, value, rom, ram);
                }
            }
            else
            if (PlatformExists(targetChain))
            {
                Runtime.Expect(targetChain != DomainSettings.PlatformName, "invalid platform as target chain");
                Nexus.BurnTokens(this, token, from, to, targetChain, value);

                var swap = new ChainSwap(DomainSettings.PlatformName, sourceChain, Transaction.Hash, targetChain, targetChain, Hash.Null);
                this.Chain.RegisterSwap(this.Storage, to, swap);
            }
            else
            if (sourceChain == this.Chain.Name)
            {
                Runtime.Expect(IsNameOfParentChain(targetChain) || IsNameOfChildChain(targetChain), "target must be parent or child chain");
                Runtime.Expect(to.IsUser, "destination must be user address");
                Runtime.Expect(IsWitness(from), "invalid witness");

                /*if (tokenInfo.IsCapped())
                {
                    var sourceSupplies = new SupplySheet(symbol, this.Runtime.Chain, Runtime.Nexus);
                    var targetSupplies = new SupplySheet(symbol, targetChain, Runtime.Nexus);

                    if (IsAddressOfParentChain(targetChainAddress))
                    {
                        Runtime.Expect(sourceSupplies.MoveToParent(this.Storage, amount), "source supply check failed");
                    }
                    else // child chain
                    {
                        Runtime.Expect(sourceSupplies.MoveToChild(this.Storage, targetChain.Name, amount), "source supply check failed");
                    }
                }*/

                if (token.IsFungible())
                {
                    Nexus.BurnTokens(this, token, from, to, targetChain, value);
                }
                else
                {
                    Nexus.BurnToken(this, token, from, to, targetChain, value);
                }

                var swap = new ChainSwap(DomainSettings.PlatformName, sourceChain, Transaction.Hash, DomainSettings.PlatformName, targetChain, Hash.Null);
                this.Chain.RegisterSwap(this.Storage, to, swap);
            }
            else
            if (targetChain == this.Chain.Name)
            {
                Runtime.Expect(IsNameOfParentChain(sourceChain) || IsNameOfChildChain(sourceChain), "source must be parent or child chain");
                Runtime.Expect(!to.IsInterop, "destination cannot be interop address");
                Runtime.Expect(IsWitness(to), "invalid witness");

                if (token.IsFungible())
                {
                    Nexus.MintTokens(this, token, from, to, sourceChain, value);
                }
                else
                {
                    Nexus.MintToken(this, token, from, to, sourceChain, value, rom, ram);
                }
            }
            else
            {
                throw new ChainException("invalid swap chain source and destinations");
            }
        }

        public void WriteToken(string tokenSymbol, BigInteger tokenID, byte[] ram)
        {
            var nft = ReadToken(tokenSymbol, tokenID);
            Nexus.WriteNFT(this, tokenSymbol, tokenID, nft.CurrentChain, nft.CurrentOwner, nft.ROM, ram, true);
        }

        public TokenContent ReadToken(string tokenSymbol, BigInteger tokenID)
        {
            return Nexus.ReadNFT(this, tokenSymbol, tokenID);
        }

        public bool IsPlatformAddress(Address address)
        {
            return Nexus.IsPlatformAddress(RootStorage, address);
        }

        public void RegisterPlatformAddress(string platform, Address localAddress, string externalAddress)
        {
            Expect(this.Chain.Name == DomainSettings.RootChainName, "must be in root chain");
            Nexus.RegisterPlatformAddress(RootStorage, platform, localAddress, externalAddress);
        }

        public byte[] ReadOracle(string URL)
        {
            return this.Oracle.Read<byte[]>(this.Time, URL);
        }

        public Hash GetTokenPlatformHash(string symbol, IPlatform platform)
        {
            if (platform == null)
            {
                return Hash.Null;
            }

            return this.Nexus.GetTokenPlatformHash(symbol, platform.Name, this.RootStorage);
        }

        public IToken GetToken(string symbol)
        {
            return Nexus.GetTokenInfo(RootStorage, symbol);
        }

        public IFeed GetFeed(string name)
        {
            return Nexus.GetFeedInfo(RootStorage, name);
        }

        public IPlatform GetPlatformByName(string name)
        {
            return Nexus.GetPlatformInfo(RootStorage, name);
        }

        public IPlatform GetPlatformByIndex(int index)
        {
            index--;
            var platforms = this.GetPlatforms();
            if (index<0 || index >= platforms.Length)
            {
                return null;
            }

            var name = platforms[index];
            return GetPlatformByName(name);
        }

        public IChain GetChainByAddress(Address address)
        {
            return Nexus.GetChainByAddress(address);
        }

        public IChain GetChainByName(string name)
        {
            return Nexus.GetChainByName(name);
        }

        public string[] GetTokens()
        {
            return Nexus.GetTokens(this.RootStorage);
        }

        public string[] GetContracts()
        {
            return Nexus.GetContracts(this.RootStorage);
        }

        public string[] GetChains()
        {
            return Nexus.GetChains(this.RootStorage);
        }

        public string[] GetPlatforms()
        {
            return Nexus.GetPlatforms(this.RootStorage);
        }

        public string[] GetFeeds()
        {
            return Nexus.GetFeeds(this.RootStorage);
        }

        public string[] GetOrganizations()
        {
            return Nexus.GetOrganizations(this.RootStorage);
        }

        public void Log(string description)
        {
            var Runtime = this;
            Runtime.Expect(NexusName != "mainnet", "logs not allowed on this nexus");
            Runtime.Expect(!string.IsNullOrEmpty(description), "invalid log string");
            Runtime.Expect(description.Length <= 256, "log string too large");
            Runtime.ConsumeGas(1000);
            Runtime.Notify(EventKind.Log, this.EntryAddress, description);
        }

        public void Throw(string description)
        {
            throw new VMException(this, description);
        }

        public override string GetDumpFileName()
        {
            if (this.Transaction != null)
            {
                return this.Transaction.Hash.ToString()+".txt";
            }

            return base.GetDumpFileName();
        }

        public override void DumpData(List<string> lines)
        {
            lines.Add(VMException.Header("RUNTIME"));
            lines.Add("Time: " + Time.Value);
            lines.Add("Nexus: " + NexusName);
            lines.Add("Chain: " + Chain.Name);
            lines.Add("TxHash: " + (Transaction != null ? Transaction.Hash.ToString() : "None"));
            if (Transaction != null)
            {
                lines.Add("Payload: " + (Transaction.Payload != null && Transaction.Payload.Length > 0 ? Base16.Encode(Transaction.Payload) : "None"));
                var bytes = Transaction.ToByteArray(true);
                lines.Add(VMException.Header("RAWTX"));
                lines.Add(Base16.Encode(bytes));
            }
        }

        public bool OrganizationExists(string name)
        {
            return Nexus.OrganizationExists(RootStorage, name);
        }

        public IOrganization GetOrganization(string name)
        {
            return Nexus.GetOrganizationByName(RootStorage, name);
        }

        public bool AddMember(string organization, Address admin, Address target)
        {
            var org = Nexus.GetOrganizationByName(RootStorage, organization);
            return org.AddMember(this, admin, target);
        }

        public bool RemoveMember(string organization, Address admin, Address target)
        {
            var org = Nexus.GetOrganizationByName(RootStorage, organization);
            return org.RemoveMember(this, admin, target);
        }

        public void MigrateMember(string organization, Address admin, Address source, Address destination)
        {
            var org = Nexus.GetOrganizationByName(RootStorage, organization);
            org.Migrate(this, admin, source, destination);
        }

        public Address GetValidator(Timestamp time)
        {
            return this.Chain.GetValidator(this.RootStorage, time);
        }


        public Timestamp GetGenesisTime()
        {
            if (HasGenesis)
            {
                var genesisBlock = Nexus.RootChain.GetBlockByHash(GenesisHash);
                return genesisBlock.Timestamp;
            }

            return this.Time;
        }

        public bool HasGenesis => Nexus.HasGenesis;
        public string NexusName => Nexus.Name;
        public BigInteger ProtocolVersion => Nexus.GetGovernanceValue(Nexus.RootStorage, Nexus.NexusProtocolVersionTag);
        public Address GenesisAddress => Nexus.GetGenesisAddress(RootStorage);
        public Hash GenesisHash => Nexus.GetGenesisHash(RootStorage);
    }
}
