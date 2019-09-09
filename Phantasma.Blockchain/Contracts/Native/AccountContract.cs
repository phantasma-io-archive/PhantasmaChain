using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts.Native
{
    /*
     * Account script triggers
     * OnMint(symbol, amount)
     * OnBurn(symbol, amount)
     * OnSend(symbol, amount)
     * OnReceive(symbol, amount)
    */
    public sealed class AccountContract : SmartContract
    {
        public override string Name => "account";

        public static readonly string TriggerMint = "OnMint";
        public static readonly string TriggerBurn = "OnBurn";
        public static readonly string TriggerSend = "OnSend";
        public static readonly string TriggerReceive = "OnReceive";

        public static readonly string ANONYMOUS = "anonymous";
        public static readonly string GENESIS = "genesis";

        internal StorageMap _addressMap; //<Address, string> 
        internal StorageMap _nameMap; //<string, Address> 
        internal StorageMap _scriptMap; //<Address, byte[]> 
        internal StorageMap _metadata;

        public static readonly BigInteger RegistrationCost = UnitConversion.ToBigInteger(0.1m, Nexus.FuelTokenDecimals);

        public AccountContract() : base()
        {
        }

        public void RegisterName(Address target, string name)
        {
            Runtime.Expect(target != Address.Null, "address must not be null");
            Runtime.Expect(target != Runtime.Nexus.GenesisAddress, "address must not be genesis");
            Runtime.Expect(IsWitness(target), "invalid witness");
            Runtime.Expect(ValidateName(name), "invalid name");

            Runtime.Expect(!_addressMap.ContainsKey(target), "address already has a name");
            Runtime.Expect(!_nameMap.ContainsKey(name), "name already used");

            _addressMap.Set(target, name);
            _nameMap.Set(name, target);

            Runtime.Notify(EventKind.AddressRegister, target, name);
        }

        public void RegisterScript(Address target, byte[] script)
        {
            Runtime.Expect(target != Address.Null, "address must not be null");
            Runtime.Expect(target != Runtime.Nexus.GenesisAddress, "address must not be genesis");
            Runtime.Expect(IsWitness(target), "invalid witness");

            Runtime.Expect(script.Length < 1024, "invalid script length");

            Runtime.Expect(!_scriptMap.ContainsKey(target), "address already has a script");

            _scriptMap.Set(target, script);

            // TODO? Runtime.Notify(EventKind.AddressRegister, target, script);
        }

        public void SetMetadata(Address target, string key, string value)
        {
            Runtime.Expect(IsWitness(target), "invalid witness");

            var metadataEntries = _metadata.Get<Address, StorageList>(target);

            int index = -1;

            var count = metadataEntries.Count();
            for (int i = 0; i < count; i++)
            {
                var temp = metadataEntries.Get<Metadata>(i);
                if (temp.key == key)
                {
                    index = i;
                    break;
                }
            }

            var metadata = new Metadata() { key = key, value = value };
            if (index >= 0)
            {
                metadataEntries.Replace<Metadata>(index, metadata);
            }
            else
            {
                metadataEntries.Add<Metadata>(metadata);
            }

            Runtime.Notify(EventKind.Metadata, target, new MetadataEventData() { type = "account", metadata = metadata });
        }

        public string GetMetadata(Address address, string key)
        {
            var metadataEntries = _metadata.Get<Address, StorageList>(address);

            var count = metadataEntries.Count();
            for (int i = 0; i < count; i++)
            {
                var temp = metadataEntries.Get<Metadata>(i);
                if (temp.key == key)
                {
                    return temp.value;
                }
            }

            return null;
        }

        public Metadata[] GetMetadataList(Address address)
        {
            var metadataEntries = _metadata.Get<Address, StorageList>(address);
            return metadataEntries.All<Metadata>();
        }

        public string LookUpAddress(Address target)
        {
            if (target == Runtime.Nexus.GenesisAddress)
            {
                return GENESIS;
            }

            if (_addressMap.ContainsKey(target))
            {
                return _addressMap.Get<Address, string>(target);
            }

            return ANONYMOUS;
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
            if (name == ANONYMOUS)
            {
                return Address.Null;
            }

            if (name == GENESIS)
            {
                return Runtime.Nexus.GenesisAddress;
            }

            if (_nameMap.ContainsKey(name))
            {
                return _nameMap.Get<string, Address>(name);
            }

            return Address.Null;
        }

        public static bool ValidateName(string name)
        {
            if (name == null)
            {
                return false;
            }

            if (name.Length < 4 || name.Length > 15)
            {
                return false;
            }

            if (name == ANONYMOUS)
            {
                return false;
            }

            int index = 0;
            while (index < name.Length)
            {
                var c = name[index];
                index++;

                if (c >= 97 && c <= 122) continue; // lowercase allowed
                if (c == 95) continue; // underscore allowed
                if (c >= 48 && c <= 57) continue; // numbers allowed

                return false;
            }

            return true;
        }
    }
}
