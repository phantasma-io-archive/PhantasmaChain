using Phantasma.Blockchain.Storage;
using Phantasma.Core.Types;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class FriendContract : SmartContract
    {
        public override string Name => "friends";
        internal StorageMap _friendMap;

        #region FRIENDLIST
        public void AddFriend(Address target, Address friend)
        {
            Runtime.Expect(IsWitness(target), "invalid witness");

            Runtime.Expect(friend != Address.Null, "friend address must not be null");
            Runtime.Expect(friend != target, "friend must be different from target address");

            var friendList = _friendMap.Get<Address, StorageList>(target);
            Runtime.Expect(!friendList.Contains(friend), "already is friend");

            friendList.Add(friend);

            Runtime.Notify(EventKind.AddressAdd, target, friend);
        }

        public void RemoveFriend(Address target, Address friend)
        {
            Runtime.Expect(IsWitness(target), "invalid witness");

            Runtime.Expect(friend != Address.Null, "friend address must not be null");
            Runtime.Expect(friend != target, "friend must be different from target address");

            var friendList = _friendMap.Get<Address, StorageList>(target);

            Runtime.Expect(friendList.Contains(friend), "friend not found");
            friendList.Remove(friend);

            Runtime.Notify(EventKind.AddressRemove, target, friend);
        }

        public Address[] GetFriends(Address target)
        {
            var friendList = _friendMap.Get<Address, StorageList>(target);
            return friendList.All<Address>();
        }
        #endregion

    }
}
