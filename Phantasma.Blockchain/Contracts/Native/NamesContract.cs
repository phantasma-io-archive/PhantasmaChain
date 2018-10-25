using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class NamesContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Names;

        public static readonly string ANONYMOUS = "anonymous";

        private const string NAME_MAP = "_names";
        private const string ADDRESS_MAP = "_addrs";

        public static readonly BigInteger RegistrationCost = TokenUtils.ToBigInteger(0.1m);

        public NamesContract() : base()
        {
        }

        public void Register(Address target, string name)
        {
            Expect(target != Address.Null);
            Expect(IsWitness(target));
            Expect(ValidateAddressName(name));

            var addressMap = Storage.FindMapForContract<Address, string>(ADDRESS_MAP);
            Expect(!addressMap.ContainsKey(target));

            var token = Runtime.Nexus.NativeToken;
            var balances = Runtime.Chain.GetTokenBalances(token);
            Expect(token.Transfer(balances, target, Runtime.Chain.Address, RegistrationCost));

            addressMap.Set(target, name);

            var nameMap = Storage.FindMapForContract<string, Address>(NAME_MAP);
            nameMap.Set(name, target);

            Runtime.Notify(EventKind.AddressRegister, target, name);
        }

        public string LookUpAddress(Address target)
        {
            var map = Storage.FindMapForContract<Address, string>(ADDRESS_MAP);

            if (map.ContainsKey(target))
            {
                return map.Get(target);
            }

            return ANONYMOUS;
        }

        public Address LookUpName(string name)
        {
            var map = Storage.FindMapForContract<string, Address>(NAME_MAP);

            if (map.ContainsKey(name))
            {
                return map.Get(name);
            }

            return Address.Null;
        }

        private static bool ValidateAddressName(string name)
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
