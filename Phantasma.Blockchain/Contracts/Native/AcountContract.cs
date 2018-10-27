using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct AddressMessage
    {
        public Address from;
        public Timestamp timestamp;
        public byte[] content;
    }

    public sealed class AcountContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Account;

        public static readonly string ANONYMOUS = "anonymous";

        public const int MIN_MESSAGE_LENGTH = 1024 * 64;
        public const int MAX_MESSAGE_LENGTH = 16;

        private static readonly string MESSAGE_ID = "_msg";
        private static readonly string FRIEND_ID = "_frd";
        private const string NAME_MAP = "_names";
        private const string ADDRESS_MAP = "_addrs";

        public static readonly BigInteger RegistrationCost = TokenUtils.ToBigInteger(0.1m, Nexus.NativeTokenDecimals);

        public AcountContract() : base()
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

        public void SendMessage(Address from, Address to, byte[] content)
        {
            Expect(IsWitness(from));

            Expect(content.Length >= MIN_MESSAGE_LENGTH);
            Expect(content.Length <= MAX_MESSAGE_LENGTH);

            var msg = new AddressMessage()
            {
                from = from,
                timestamp = Runtime.Block.Timestamp,
                content = content
            };

            var list = Storage.FindCollectionForAddress<AddressMessage>(MESSAGE_ID, from);
            list.Add(msg);
        }

        public AddressMessage[] GetMessages(Address target)
        {
            var list = Storage.FindCollectionForAddress<AddressMessage>(MESSAGE_ID, target);
            return list.All();
        }

        #region FRIENDLIST
        public void AddFriend(Address target, Address friend)
        {
            Expect(IsWitness(target));

            Expect(friend != target);
            Expect(friend != Address.Null);

            var list = Storage.FindCollectionForAddress<Address>(FRIEND_ID, target);
            list.Add(friend);

            Runtime.Notify(EventKind.FriendAdd, target, friend);
        }

        public void RemoveFriend(Address target, Address friend)
        {
            Expect(IsWitness(target));

            Expect(friend != target);
            Expect(friend != Address.Null);

            var list = Storage.FindCollectionForAddress<Address>(FRIEND_ID, target);
            Expect(list.Contains(friend));
            list.Remove(friend);

            Runtime.Notify(EventKind.FriendRemove, target, friend);
        }

        public Address[] GetFriends(Address target)
        {
            var list = Storage.FindCollectionForAddress<Address>(FRIEND_ID, target);
            return list.All();
        }
        #endregion

    }
}
