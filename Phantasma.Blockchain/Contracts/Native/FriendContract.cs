using Phantasma.Blockchain.Storage;
using Phantasma.Core.Types;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class FriendContract : SmartContract
    {
        public override string Name => "friends";
        internal StorageList _friends;

        #region FRIENDLIST
        public void AddFriend(Address target, Address friend)
        {
            Runtime.Expect(IsWitness(target), "invalid witness");

            Runtime.Expect(friend != Address.Null, "friend address must not be null");
            Runtime.Expect(friend != target, "friend must be different from target address");

            _friends.Add(friend);

            Runtime.Notify(EventKind.AddressAdd, target, friend);
        }

        public void RemoveFriend(Address target, Address friend)
        {
            Runtime.Expect(IsWitness(target), "invalid witness");

            Runtime.Expect(friend != Address.Null, "friend address must not be null");
            Runtime.Expect(friend != target, "friend must be different from target address");

            Runtime.Expect(_friends.Contains(friend), "friend not found");
            _friends.Remove(friend);

            Runtime.Notify(EventKind.AddressRemove, target, friend);
        }

        public Address[] GetFriends(Address target)
        {
            return _friends.All<Address>();
        }
        #endregion

    }
}
