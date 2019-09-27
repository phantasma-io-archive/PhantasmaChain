using System;
using System.Collections.Generic;
using Phantasma.VM;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using Phantasma.Storage.Context;
using Phantasma.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Domain;
using Phantasma.Contracts;

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

        private readonly StorageChangeSetContext changeSet;

        private StorageContext RootStorage => this.IsRootChain() ? this.Storage : Nexus.RootStorage;

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
            this.changeSet = changeSet;
            this.readOnlyMode = readOnlyMode;

            this.isBlockOperation = false;
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

        INexus IRuntime.Nexus => this.Nexus;

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
            // TODO review this better
            //Expect(!isBlockOperation, "no interops available in block operations");

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
                    if (changeSet.Any())
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
            if (isBlockOperation && Nexus.HasGenesis)
            {
                throw new ChainException($"{contextName} context not available in block operations");
            }

            var contract = this.Nexus.GetContractByName(contextName);
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

            for (int i= args.Length - 1; i>=0; i--)
            {
                var obj = VMObject.FromObject(args[i]);
                this.Stack.Push(obj);
            }

            this.Stack.Push(VMObject.FromObject(methodName));

            BigInteger savedGas = this.UsedGas;

            this.EntryAddress = SmartContract.GetAddressForName(CurrentContext.Name);
            CurrentContext = context;
            var temp = context.Execute(this.CurrentFrame, this.Stack);
            Expect(temp == ExecutionState.Halt, "expected call success");

            CurrentContext = previousContext;
            this.EntryAddress = previousCaller;

            if (contextName == Nexus.BombContractName)
            {
                this.UsedGas = savedGas;
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

                case EventKind.ValidatorPropose:
                case EventKind.ValidatorElect:
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
            if (symbol == DomainSettings.FiatTokenSymbol)
            {
                return UnitConversion.GetUnitValue(DomainSettings.FiatTokenDecimals);
            }

            Core.Throw.If(!Nexus.TokenExists(symbol), "cannot read price for invalid token");
            var token = GetToken(symbol);

            if (!token.Flags.HasFlag(TokenFlags.External))
            {
                var result = CallContext("exchange", "GetTokenPrice", symbol).AsNumber();
                return result;
            }

            Core.Throw.If(Oracle == null, "cannot read price from null oracle");
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
            result = UnitConversion.ConvertDecimals(result, baseToken.Decimals, DomainSettings.FiatTokenDecimals);

            result /= quotePrice;

            result = UnitConversion.ConvertDecimals(result, DomainSettings.FiatTokenDecimals, quoteToken.Decimals);

            return result;
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
                var accountScript = Nexus.LookUpAddressScript(this.changeSet, address);
                return InvokeTrigger(accountScript, trigger.ToString(), args);
            }

            if (address.IsSystem)
            {
                var contract = Nexus.GetContractByAddress(address);
                if (contract != null)
                {
                    var triggerName = trigger.ToString();
                    BigInteger gasCost;
                    if (contract.HasInternalMethod(triggerName, out gasCost))
                    {
                        CallContext(contract.Name, triggerName, args);
                    }
                }

                return true;
            }

            return true;
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
            var runtime = new RuntimeVM(script, this.Chain, this.Time, this.Transaction, this.changeSet, this.Oracle, false, true);
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

        public bool IsWitness(Address address)
        {
            if (address == this.Chain.Address /*|| address == this.Address*/)
            {
                return false;
            }

            if (address == this.EntryAddress)
            {
                return true;
            }

            if (address.IsSystem)
            {
                var contextAddress = SmartContract.GetAddressForName(this.CurrentContext.Name);
                return contextAddress == address;
            }

            if (address.IsInterop)
            {
                return false;
            }

            if (this.Transaction == null)
            {
                return false;
            }

            if (address.IsUser && Nexus.HasGenesis && this.Nexus.HasAddressScript(changeSet, address))
            {
                return InvokeTriggerOnAccount(address, AccountTrigger.OnWitness, address);
            }

            return this.Transaction.IsSignedBy(address);
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
            return Nexus.TokenExists(symbol);
        }

        public bool FeedExists(string name)
        {
            return Nexus.FeedExists(name);
        }

        public bool PlatformExists(string name)
        {
            return Nexus.PlatformExists(name);
        }

        public bool ContractExists(string name)
        {
            return Nexus.PlatformExists(name);
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

        public Address GetValidatorForBlock(Hash blockHash)
        {
            return Chain.GetValidatorForBlock(blockHash);
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

        private static readonly string uuidKey = ".uuid";

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
            Expect(Nexus.TokenExists(symbol), $"Token does not exist ({symbol})");
            return Chain.GetTokenBalance(this.Storage, symbol, address);
        }

        public BigInteger[] GetOwnerships(string symbol, Address address)
        {
            Expect(Nexus.TokenExists(symbol), $"Token does not exist ({symbol})");
            return Chain.GetOwnedTokens(this.Storage, symbol, address);
        }

        public BigInteger GetTokenSupply(string symbol)
        {
            Expect(Nexus.TokenExists(symbol), $"Token does not exist ({symbol})");
            return Chain.GetTokenSupply(this.Storage, symbol);
        }

        public bool CreateToken(string symbol, string name, string platform, Hash hash, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script)
        {
            return Nexus.CreateToken(symbol, name, platform, hash, maxSupply, decimals, flags, script);
        }

        public bool CreateChain(Address owner, string name, string parentChain)
        {
            return Nexus.CreateChain(this.RootStorage, owner, name, parentChain);
        }

        public bool CreateFeed(Address owner, string name, FeedMode mode)
        {
            return Nexus.CreateFeed(this.RootStorage, owner, name, mode);
        }

        public bool CreatePlatform(Address address, string name, string fuelSymbol)
        {
            return Nexus.CreatePlatform(this.RootStorage, address, name, fuelSymbol);
        }

        public bool CreateArchive(MerkleTree merkleTree, BigInteger size, ArchiveFlags flags, byte[] key)
        {
            return Nexus.CreateArchive(this.RootStorage, merkleTree, size, flags, key);
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

        public bool MintTokens(string symbol, Address from, Address target, BigInteger amount)
        {
            var Runtime = this;

            // TODO should not be necessary, verified by trigger
            //Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");
            var tokenInfo = Runtime.GetToken(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(!tokenInfo.Flags.HasFlag(TokenFlags.Fiat), "token can't be fiat");

            return Nexus.MintTokens(this, symbol, from, target, amount, false);
        }

        public BigInteger MintToken(string symbol, Address from, Address target, byte[] rom, byte[] ram)
        {
            var Runtime = this;
            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");
            var tokenInfo = Runtime.GetToken(symbol);
            Runtime.Expect(!tokenInfo.IsFungible(), "token must be non-fungible");
            // TODO should not be necessary, verified by trigger
            //Runtime.Expect(IsWitness(target), "invalid witness");

            Runtime.Expect(Runtime.IsRootChain(), "can only mint nft in root chain");

            Runtime.Expect(rom.Length <= TokenContent.MaxROMSize, "ROM size exceeds maximum allowed");
            Runtime.Expect(ram.Length <= TokenContent.MaxRAMSize, "RAM size exceeds maximum allowed");

            var tokenID = Nexus.CreateNFT(symbol, Runtime.Chain.Name, target, rom, ram);
            Runtime.Expect(tokenID > 0, "invalid tokenID");

            Runtime.Expect(Nexus.MintToken(this, symbol, from, target, tokenID, false), "minting failed");

            return tokenID;
        }

        public bool BurnTokens(string symbol, Address target, BigInteger amount)
        {
            var Runtime = this;
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(IsWitness(target), "invalid witness");

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");
            var tokenInfo = Runtime.GetToken(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(tokenInfo.IsBurnable(), "token must be burnable");
            Runtime.Expect(!tokenInfo.Flags.HasFlag(TokenFlags.Fiat), "token can't be fiat");

            return Nexus.BurnTokens(this, symbol, target, amount, false);
        }

        public bool BurnToken(string symbol, Address target, BigInteger tokenID)
        {
            var Runtime = this;
            Runtime.Expect(IsWitness(target), "invalid witness");

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");
            var tokenInfo = Runtime.GetToken(symbol);
            Runtime.Expect(!tokenInfo.IsFungible(), "token must be non-fungible");
            Runtime.Expect(tokenInfo.IsBurnable(), "token must be burnable");

            var nft = Runtime.ReadToken(symbol, tokenID);

            return Nexus.BurnToken(this, symbol, target, tokenID, false);
        }

        public bool TransferTokens(string symbol, Address source, Address destination, BigInteger amount)
        {
            var Runtime = this;

            if (source == destination)
            {
                return true;
            }

            if (IsPlatformAddress(source))
            {
                return Nexus.MintTokens(this, symbol, this.EntryAddress, destination, amount, true);
            }

            if (IsPlatformAddress(destination))
            {
                return Nexus.BurnTokens(this, symbol, source, amount, true);
            }

            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(IsWitness(source), "invalid witness");
            Runtime.Expect(!Runtime.IsTrigger, "not allowed inside a trigger");

            if (destination.IsInterop)
            {
                Runtime.Expect(Runtime.Chain.IsRoot, "interop transfers only allowed in main chain");
                Runtime.CallContext("interop", "WithdrawTokens", source, destination, symbol, amount);
                return true;
            }

            Runtime.Expect(Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");

            return Runtime.Nexus.TransferTokens(Runtime, symbol, source, destination, amount);
        }

        public bool TransferToken(string symbol, Address source, Address destination, BigInteger tokenID)
        {
            var Runtime = this;
            Runtime.Expect(IsWitness(source), "invalid witness");

            Runtime.Expect(source != destination, "source and destination must be different");

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");
            var tokenInfo = Runtime.GetToken(symbol);
            Runtime.Expect(!tokenInfo.IsFungible(), "token must be non-fungible");

            return Nexus.TransferToken(this, symbol, source, destination, tokenID);
        }

        public bool SendTokens(Address targetChainAddress, Address from, Address to, string symbol, BigInteger amount)
        {
            throw new NotImplementedException();
        }

        public bool SendToken(Address targetChainAddress, Address from, Address to, string symbol, BigInteger tokenID)
        {
            throw new NotImplementedException();
        }

        public bool WriteToken(string tokenSymbol, BigInteger tokenID, byte[] ram)
        {
            throw new NotImplementedException();
        }

        public TokenContent ReadToken(string tokenSymbol, BigInteger tokenID)
        {
            throw new NotImplementedException();
        }

        public bool IsPlatformAddress(Address address)
        {
            return Nexus.IsPlatformAddress(address);
        }

        public byte[] ReadOracle(string URL)
        {
            return this.Oracle.Read(URL);
        }

        public IToken GetToken(string symbol)
        {
            return Nexus.GetTokenInfo(symbol);
        }

        public IFeed GetFeed(string name)
        {
            return Nexus.GetFeedInfo(name);
        }

        public IPlatform GetPlatform(string name)
        {
            return Nexus.GetPlatformInfo(name);
        }

        public IChain GetChainByAddress(Address address)
        {
            return Nexus.GetChainByAddress(address);
        }

        public IChain GetChainByName(string name)
        {
            return Nexus.GetChainByName(name);
        }

        public void Log(string description)
        {
            throw new NotImplementedException();
        }

        public void Throw(string description)
        {
#if DEBUG
            throw new VMDebugException(this, description);
#else
            throw new ChainException(description);
#endif
        }
    }
}
