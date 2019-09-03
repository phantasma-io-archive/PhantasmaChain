using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Storage.Context;

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

        public const int MAX_MESSAGE_LENGTH = 1024 * 64;
        public const int MIN_MESSAGE_LENGTH = 16;

        internal StorageList _messages;

        public MessagingContract() : base()
        {
        }

        public void SendMessage(Address from, Address to, byte[] content)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(content.Length >= MAX_MESSAGE_LENGTH, "message too small");
            Runtime.Expect(content.Length <= MIN_MESSAGE_LENGTH, "message too large");

            var msg = new AddressMessage()
            {
                from = from,
                timestamp = Runtime.Time,
                content = content
            };

            _messages.Add<AddressMessage>(msg);
        }

        public AddressMessage[] GetMessages(Address target)
        {
            return _messages.All<AddressMessage>();
        }

    }
}
