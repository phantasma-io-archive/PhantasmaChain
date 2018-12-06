using Phantasma.Blockchain.Storage;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct AddressMessage
    {
        public Address from;
        public Timestamp timestamp;
        public byte[] content;
    }

    public sealed class MessagingContract : SmartContract
    {
        public override string Name => "messages";

        public const int MIN_MESSAGE_LENGTH = 1024 * 64;
        public const int MAX_MESSAGE_LENGTH = 16;

        private static readonly string MESSAGE_ID = "_msg";
        private static readonly string FRIEND_ID = "_frd";

        public MessagingContract() : base()
        {
        }

        public void SendMessage(Address from, Address to, byte[] content)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(content.Length >= MIN_MESSAGE_LENGTH, "message too small");
            Runtime.Expect(content.Length <= MAX_MESSAGE_LENGTH, "message too large");

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
            Runtime.Expect(IsWitness(target), "invalid witness");

            Runtime.Expect(friend != Address.Null, "friend address must not be null");
            Runtime.Expect(friend != target, "friend must be different from target address");

            var list = Storage.FindCollectionForAddress<Address>(FRIEND_ID, target);
            list.Add(friend);

            Runtime.Notify(EventKind.FriendAdd, target, friend);
        }

        public void RemoveFriend(Address target, Address friend)
        {
            Runtime.Expect(IsWitness(target), "invalid witness");

            Runtime.Expect(friend != Address.Null, "friend address must not be null");
            Runtime.Expect(friend != target, "friend must be different from target address");

            var list = Storage.FindCollectionForAddress<Address>(FRIEND_ID, target);
            Runtime.Expect(list.Contains(friend), "friend not found");
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
