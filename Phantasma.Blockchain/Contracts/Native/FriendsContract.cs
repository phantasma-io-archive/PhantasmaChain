using Phantasma.Core.Types;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class FriendsContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Friends;

        private static readonly string FRIEND_ID = "_frd";

        public FriendsContract() : base()
        {
        }

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
    }
}
