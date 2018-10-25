using Phantasma.Core.Types;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct AddressMessage
    {
        public Address from;
        public Timestamp timestamp;
        public byte[] content;
    }

    public sealed class MessagesContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Messages;

        private static readonly string MESSAGE_ID = "_msg";

        public const int MIN_MESSAGE_LENGTH = 1024 * 64;
        public const int MAX_MESSAGE_LENGTH = 16;

        public MessagesContract() : base()
        {
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
    }
}
