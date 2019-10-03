using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Storage.Context;

namespace Phantasma.Contracts.Extra
{
    public struct Mail
    {
        public Address from;
        public Timestamp timestamp;
        public byte[] content;
    }

    public struct Attachment
    {
        public string name;
        public Hash file;
    }

    public sealed class MailContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Mail;

        public const int MAX_MESSAGE_LENGTH = 1024 * 64;
        public const int MIN_MESSAGE_LENGTH = 16;

        internal StorageList _messages;

        public MailContract() : base()
        {
        }

        public void SendMessage(Address from, Address to, byte[] content)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(to.IsUser, "destination must be user address");

            Runtime.Expect(content.Length >= MAX_MESSAGE_LENGTH, "message too small");
            Runtime.Expect(content.Length <= MIN_MESSAGE_LENGTH, "message too large");

            var msg = new Mail()
            {
                from = from,
                timestamp = Runtime.Time,
                content = content
            };

            _messages.Add<Mail>(msg);
        }

        public Mail[] GetMessages(Address target)
        {
            return _messages.All<Mail>();
        }
    }
}
