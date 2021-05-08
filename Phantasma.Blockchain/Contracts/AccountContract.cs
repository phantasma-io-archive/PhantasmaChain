using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class AccountContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Account;

        internal StorageMap _addressMap; //<Address, string> 
        internal StorageMap _nameMap; //<string, Address> 
        internal StorageMap _scriptMap; //<Address, byte[]> 
        internal StorageMap _abiMap; //<Address, byte[]> 

        public static readonly BigInteger RegistrationCost = UnitConversion.ToBigInteger(0.1m, DomainSettings.FuelTokenDecimals);

        public AccountContract() : base()
        {
        }

        public void RegisterName(Address target, string name)
        {
            Runtime.Expect(target.IsUser, "must be user address");
            Runtime.Expect(target != Runtime.GenesisAddress, "address must not be genesis");
            Runtime.Expect(Runtime.IsWitness(target), "invalid witness");
            Runtime.Expect(ValidationUtils.IsValidIdentifier(name), "invalid name");

            var stake = Runtime.GetStake(target);
            Runtime.Expect(stake >= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals), "must have something staked");

            Runtime.Expect(name != Runtime.NexusName, "name already used for nexus");
            Runtime.Expect(!Runtime.ChainExists(name), "name already used for a chain");
            Runtime.Expect(!Runtime.PlatformExists(name), "name already used for a platform");
            Runtime.Expect(!Runtime.ContractExists(name), "name already used for a contract");
            Runtime.Expect(!Runtime.FeedExists(name), "name already used for a feed");
            Runtime.Expect(!Runtime.OrganizationExists(name), "name already used for a organization");
            Runtime.Expect(!Runtime.TokenExists(name.ToUpper()), "name already used for a token");

            Runtime.Expect(!_addressMap.ContainsKey(target), "address already has a name");
            Runtime.Expect(!_nameMap.ContainsKey(name), "name already used for other account");

            var isReserved = ValidationUtils.IsReservedIdentifier(name);

            if (isReserved && Runtime.IsWitness(Runtime.GenesisAddress))
            {
                isReserved = false;
            }

            Runtime.Expect(!isReserved, $"name '{name}' reserved by system");

            _addressMap.Set(target, name);
            _nameMap.Set(name, target);

            Runtime.Notify(EventKind.AddressRegister, target, name);
        }

        public void UnregisterName(Address target)
        {
            Runtime.Expect(target.IsUser, "must be user address");
            Runtime.Expect(target != Runtime.GenesisAddress, "address must not be genesis");
            Runtime.Expect(Runtime.IsWitness(target), "invalid witness");

            Runtime.Expect(_addressMap.ContainsKey(target), "address doest not have a name yet");

            var name = _addressMap.Get<Address, string>(target);
            _addressMap.Remove(target);
            _nameMap.Remove(name);

            Runtime.Notify(EventKind.AddressUnregister, target, name);
        }

        public void RegisterScript(Address target, byte[] script, byte[] abiBytes)
        {
            Runtime.Expect(target.IsUser, "must be user address");
            Runtime.Expect(target != Runtime.GenesisAddress, "address must not be genesis");
            Runtime.Expect(Runtime.IsWitness(target), "invalid witness");

            var stake = Runtime.GetStake(target);
            Runtime.Expect(stake >= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals), "must have something staked");

            Runtime.Expect(script.Length < 1024, "invalid script length");

            Runtime.Expect(!_scriptMap.ContainsKey(target), "address already has a script");

            var abi = ContractInterface.FromBytes(abiBytes);
            Runtime.Expect(abi.MethodCount > 0, "unexpected empty contract abi");

            var witnessTriggerName = AccountTrigger.OnWitness.ToString();
            if (abi.HasMethod(witnessTriggerName))
            {
                var witnessCheck = Runtime.InvokeTrigger(false, script, NativeContractKind.Account, abi, witnessTriggerName, Address.Null) != TriggerResult.Failure;
                Runtime.Expect(!witnessCheck, "script does not handle OnWitness correctly, case #1");

                witnessCheck = Runtime.InvokeTrigger(false, script, NativeContractKind.Account, abi, witnessTriggerName, target) != TriggerResult.Failure;
                Runtime.Expect(witnessCheck, "script does not handle OnWitness correctly, case #2");
            }

            _scriptMap.Set(target, script);
            _abiMap.Set(target, abiBytes);

            var constructor = abi.FindMethod(SmartContract.ConstructorName);

            if (constructor != null)
            {
                Runtime.CallContext(target.Text, constructor, target);
            }

            // TODO? Runtime.Notify(EventKind.AddressRegister, target, script);
        }

        public bool HasScript(Address address)
        {
            if (address.IsUser)
            {
                return _scriptMap.ContainsKey(address);
            }

            return false;
        }

        public string LookUpAddress(Address target)
        {
            if (target == Runtime.GenesisAddress)
            {
                return ValidationUtils.GENESIS_NAME;
            }

            if (_addressMap.ContainsKey(target))
            {
                return _addressMap.Get<Address, string>(target);
            }

            return ValidationUtils.ANONYMOUS_NAME;
        }

        public byte[] LookUpScript(Address target)
        {
            if (_scriptMap.ContainsKey(target))
            {
                return _scriptMap.Get<Address, byte[]>(target);
            }

            return new byte[0];
        }

        public byte[] LookUpABI(Address target)
        {
            if (_abiMap.ContainsKey(target))
            {
                return _abiMap.Get<Address, byte[]>(target);
            }

            return null;
        }

        public Address LookUpName(string name)
        {
            if (name == ValidationUtils.ANONYMOUS_NAME || name == ValidationUtils.NULL_NAME)
            {
                return Address.Null;
            }

            if (name == ValidationUtils.GENESIS_NAME)
            {
                return Runtime.GenesisAddress;
            }

            if (_nameMap.ContainsKey(name))
            {
                return _nameMap.Get<string, Address>(name);
            }

            return Address.Null;
        }


        public void Migrate(Address from, Address target)
        {
            Runtime.Expect(target != from, "addresses must be different");
            Runtime.Expect(target.IsUser, "must be user address");

            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            bool isSeller = Runtime.CallNativeContext(NativeContractKind.Market, nameof(MarketContract.IsSeller), from).AsBool();
            Runtime.Expect(!isSeller, "sale pending on market");

            isSeller = Runtime.CallNativeContext(NativeContractKind.Sale, nameof(SaleContract.IsSeller), from).AsBool();
            Runtime.Expect(!isSeller, "crowdsale pending");

            var relayBalance = Runtime.CallNativeContext(NativeContractKind.Relay, nameof(RelayContract.GetBalance), from).AsNumber();
            Runtime.Expect(relayBalance == 0, "relay channel can't be open");

            var unclaimed = Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), from).AsNumber();
            if (unclaimed > 0)
            {
                Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Claim), from, from);
            }

            var symbols = Runtime.GetTokens();
            foreach (var symbol in symbols)
            {
                var balance = Runtime.GetBalance(symbol, from);
                if (balance > 0)
                {
                    var info = Runtime.GetToken(symbol);
                    if (info.IsFungible())
                    {
                        Runtime.TransferTokens(symbol, from, target, balance);
                    }
                    else
                    {
                        var tokenIDs = Runtime.GetOwnerships(symbol, from);
                        foreach (var tokenID in tokenIDs)
                        {
                            Runtime.TransferToken(symbol, from, target, tokenID);
                        }
                    }
                }
            }

            if (_addressMap.ContainsKey(from))
            {
                var currentName = _addressMap.Get<Address, string>(from);
                _addressMap.Migrate<Address, string>(from, target);
                _nameMap.Set(currentName, target);

                var tmp = LookUpAddress(target);
                Runtime.Expect(tmp == currentName, "migration of name failed");
            }

            _scriptMap.Migrate<Address, byte[]>(from, target);
            _abiMap.Migrate<Address, byte[]>(from, target);

            var stake = Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.GetStake), from).AsNumber();
            if (stake > 0)
            {
                Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Migrate), from, target);
            }

            if (Runtime.IsKnownValidator(from))
            {
                Runtime.CallNativeContext(NativeContractKind.Validator, nameof(ValidatorContract.Migrate), from, target);
            }

            var usedSpace = Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.GetUsedSpace), from).AsNumber();
            if (usedSpace > 0)
            {
                Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.Migrate), from, target);
            }

            Runtime.CallNativeContext(NativeContractKind.Consensus, nameof(ConsensusContract.Migrate), from, target);

            // TODO exchange, friend

            var orgs = Runtime.GetOrganizations();
            foreach (var orgID in orgs)
            {
                Runtime.MigrateMember(orgID, from, from, target);
            }

            var migrateMethod = new ContractMethod("OnMigrate", VM.VMType.None, -1, new ContractParameter[] { new ContractParameter("from", VM.VMType.Object), new ContractParameter("to", VM.VMType.Object) });

            var contracts = Runtime.GetContracts();
            foreach (var contract in contracts)
            {
                var abi = contract.ABI;

                if (abi.Implements(migrateMethod))
                {
                    var method = abi.FindMethod(migrateMethod.name);
                    Runtime.CallContext(contract.Name, method, from, target);
                }
            }

            foreach (var symbol in symbols)
            {
                var token = Runtime.GetToken(symbol);

                var abi = token.ABI;

                if (abi.Implements(migrateMethod))
                {
                    var method = abi.FindMethod(migrateMethod.name);
                    Runtime.CallContext(symbol, method, from, target);
                }
            }

            Runtime.CallInterop("Nexus.MigrateToken", from, target);
        }


        public static ContractMethod GetTriggerForABI(AccountTrigger trigger)
        {
            return GetTriggersForABI(new[] { trigger }).First();
        }

        public static IEnumerable<ContractMethod> GetTriggersForABI(IEnumerable<AccountTrigger> triggers)
        {
            var entries = new Dictionary<AccountTrigger, int>();
            foreach (var trigger in triggers)
            {
                entries[trigger] = 0;
            }

            return GetTriggersForABI(entries);
        }

        public static IEnumerable<ContractMethod> GetTriggersForABI(Dictionary<AccountTrigger, int> triggers)
        {
            var result = new List<ContractMethod>();

            foreach (var entry in triggers)
            {
                var trigger = entry.Key;
                var offset = entry.Value;

                switch (trigger)
                {
                    case AccountTrigger.OnWitness:
                    case AccountTrigger.OnUpgrade:
                        result.Add(new ContractMethod(trigger.ToString(), VM.VMType.None, offset, new[] { new ContractParameter("from", VM.VMType.Object) }));
                        break;

                    case AccountTrigger.OnBurn:
                    case AccountTrigger.OnMint:
                    case AccountTrigger.OnReceive:
                    case AccountTrigger.OnSend:
                        result.Add(new ContractMethod(trigger.ToString(), VM.VMType.None, offset, new[] {
                            new ContractParameter("from", VM.VMType.Object),
                            new ContractParameter("to", VM.VMType.Object),
                            new ContractParameter("symbol", VM.VMType.String),
                            new ContractParameter("amount", VM.VMType.Number)
                        }));
                        break;

                    default:
                        throw new System.Exception("AddTriggerToABI: Unsupported trigger: " + trigger);
                }
            }

            return result;
        }
    }
}
