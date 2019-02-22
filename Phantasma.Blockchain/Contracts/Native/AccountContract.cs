using Phantasma.Blockchain.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class AccountContract : SmartContract
    {
        public override string Name => "account";

        public static readonly string ANONYMOUS = "anonymous";
        public static readonly string GENESIS = "genesis";

        internal StorageMap _addressMap; //<Address, string> 
        internal StorageMap _nameMap; //<string, Address> 

        public static readonly BigInteger RegistrationCost = UnitConversion.ToBigInteger(0.1m, Nexus.FuelTokenDecimals);

        public AccountContract() : base()
        {
        }

        public void Register(Address target, string name)
        {
            Runtime.Expect(target != Address.Null, "address must not be null");
            Runtime.Expect(target != Runtime.Nexus.GenesisAddress, "address must not be genesis");
            Runtime.Expect(IsWitness(target), "invalid witness");
            Runtime.Expect(ValidateAddressName(name), "invalid name");

            Runtime.Expect(!_addressMap.ContainsKey(target), "address already has a name");

            var token = Runtime.Nexus.FuelToken;
            var balances = Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Transfer(this.Storage, balances, target, Runtime.Chain.Address, RegistrationCost), "fee failed");

            _addressMap.Set(target, name);
            _nameMap.Set(name, target);

            Runtime.Notify(EventKind.AddressRegister, target, name);
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

        public static bool ValidateAddressName(string name)
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
