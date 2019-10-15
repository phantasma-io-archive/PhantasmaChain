using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Contracts.Native
{
    public sealed class AccountContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Account;

        internal StorageMap _addressMap; //<Address, string> 
        internal StorageMap _nameMap; //<string, Address> 
        internal StorageMap _scriptMap; //<Address, byte[]> 
        internal StorageMap _metadata;

        public static readonly BigInteger RegistrationCost = UnitConversion.ToBigInteger(0.1m, DomainSettings.FuelTokenDecimals);

        public AccountContract() : base()
        {
        }

        private string[] reservedNames = new string[] {
            "phantasma", "neo", "ethereum", "bitcoin", "litecoin", "eos", "ripple", "tether", "tron",
            "monero", "dash", "tezos", "cosmos", "maker", "ontology", "dogecoin", "zcash", "vechain",
            "qtum", "omise",  "holo", "nano", "augur", "waves", "icon" , "dai", "bitshares",
            "siacoin", "komodo", "zilliqa", "steem", "enjin", "aelf", "nash", "stratis",
            "decentraland", "elastos", "loopring", "grin", "nuls", "loom", "enigma", "wax",
            "bancor", "ark", "nos", "bluzelle", "satoshi", "gwei", "nacho", "chainchanged",
            "oracle", "oracles", "dex", "exchange", "ico", "ieo", "wallet", "account", "address",
            "airdrop", "giveaway", "casino", "free", "mail", "dapp", "donation", "charity",
            "coin", "token", "nexus", "deposit", "phantom", "phantomforce", "cityofzion", "coz",
            "huobi", "binance", "kraken", "kucoin", "coinbase", "switcheo", "bittrex","bitstamp",
            "bithumb", "okex", "hotbit", "bitmart", "bilaxy", "vitalik", "nakamoto", "sergio",
            "windows", "osx", "ios","android", "google", "yahoo", "facebook", "alibaba", "ebay",
            "apple", "amazon", "microsoft", "samsung", "verizon", "walmart", "ibm", "disney",
            "netflix", "alibaba", "tencent", "baidu", "visa", "mastercard", "instagram", "paypal",
            "adobe", "huawei", "vodafone", "dell", "uber", "youtube", "whatsapp", "snapchat", "pinterest",
            "22", "goati", "gamecenter", "pixgamecenter", "seal", "crosschain", "blacat",
            "bitladon", "bitcoinmeester"};

    
        public void RegisterName(Address target, string name)
        {
            Runtime.Expect(target.IsUser, "must be user address");
            Runtime.Expect(target != Runtime.Nexus.GenesisAddress, "address must not be genesis");
            Runtime.Expect(Runtime.IsWitness(target), "invalid witness");
            Runtime.Expect(ValidationUtils.IsValidIdentifier(name), "invalid name");

            var stake = Runtime.GetStake(target);
            Runtime.Expect(stake >= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals), "must have something staked");

            Runtime.Expect(!_addressMap.ContainsKey(target), "address already has a name");
            Runtime.Expect(!_nameMap.ContainsKey(name), "name already used for other account");

            Runtime.Expect(name != Runtime.Nexus.Name, "name already used for nexus");
            Runtime.Expect(!Runtime.ChainExists(name), "name already used for a chain");
            Runtime.Expect(!Runtime.PlatformExists(name), "name already used for a platform");
            Runtime.Expect(!Runtime.ContractExists(name), "name already used for a contract");
            Runtime.Expect(!Runtime.FeedExists(name), "name already used for a feed");
            Runtime.Expect(!Runtime.OrganizationExists(name), "name already used for a organization");
            Runtime.Expect(!Runtime.TokenExists(name.ToUpper()), "name already used for a token");

            bool isReserved = false;
            for (int i = 0; i < reservedNames.Length; i++)
            {
                if (name.StartsWith(reservedNames[i]))
                {
                    isReserved = true;
                    break;
                }
            }

            if (isReserved && Runtime.IsWitness(Runtime.Nexus.GenesisAddress))
            {
                isReserved = false;
            }

            Runtime.Expect(!isReserved, "name reserved by system");

            _addressMap.Set(target, name);
            _nameMap.Set(name, target);

            Runtime.Notify(EventKind.AddressRegister, target, name);
        }

        public void UnregisterName(Address target)
        {
            Runtime.Expect(target.IsUser, "must be user address");
            Runtime.Expect(target != Runtime.Nexus.GenesisAddress, "address must not be genesis");
            Runtime.Expect(Runtime.IsWitness(target), "invalid witness");
          
            Runtime.Expect(_addressMap.ContainsKey(target), "address doest not have a name yet");

            var name = _addressMap.Get<Address, string>(target);
            _addressMap.Remove(target);
            _nameMap.Remove(name);

            Runtime.Notify(EventKind.AddressUnregister, target, name);
        }

        public void RegisterScript(Address target, byte[] script)
        {
            Runtime.Expect(target.IsUser, "must be user address");
            Runtime.Expect(target != Runtime.Nexus.GenesisAddress, "address must not be genesis");
            Runtime.Expect(Runtime.IsWitness(target), "invalid witness");

            var stake = Runtime.GetStake(target);
            Runtime.Expect(stake >= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals), "must have something staked");

            Runtime.Expect(script.Length < 1024, "invalid script length");

            Runtime.Expect(!_scriptMap.ContainsKey(target), "address already has a script");

            var witnessCheck = Runtime.InvokeTrigger(script, AccountTrigger.OnWitness.ToString(), Address.Null);
            Runtime.Expect(!witnessCheck, "script does not handle OnWitness correctly");

            _scriptMap.Set(target, script);

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
            if (target == Runtime.Nexus.GenesisAddress)
            {
                return ValidationUtils.GENESIS;
            }

            if (_addressMap.ContainsKey(target))
            {
                return _addressMap.Get<Address, string>(target);
            }

            return ValidationUtils.ANONYMOUS;
        }

        public byte[] LookUpScript(Address target)
        {
            if (_scriptMap.ContainsKey(target))
            {
                return _scriptMap.Get<Address, byte[]>(target);
            }

            return new byte[0];
        }

        public Address LookUpName(string name)
        {
            if (name == ValidationUtils.ANONYMOUS)
            {
                return Address.Null;
            }

            if (name == ValidationUtils.GENESIS)
            {
                return Runtime.Nexus.GenesisAddress;
            }

            if (_nameMap.ContainsKey(name))
            {
                return _nameMap.Get<string, Address>(name);
            }

            return Address.Null;
        }
    }
}
