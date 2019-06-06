using System;
using System.IO;
using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.Blockchain.Contracts
{
    public enum EventKind
    {
        ChainCreate = 0,
        TokenCreate = 1,
        TokenSend = 2,
        TokenReceive = 3,
        TokenMint = 4,
        TokenBurn = 5,
        TokenEscrow = 6,
        TokenStake = 7,
        TokenUnstake = 8,
        TokenClaim = 9,
        MasterDemote = 10,
        MasterPromote = 11,
        AddressRegister = 12,
        AddressAdd = 13,
        AddressRemove = 14,
        GasEscrow = 15,
        GasPayment = 16,
        OrderCreated = 17,
        OrderCancelled = 18,
        OrderFilled = 19,
        OrderClosed = 20,
        AddFriend = 21,
        RemoveFriend = 22,
        Metadata = 23,
        Custom = 24,
    }

    public class Event
    {
        public readonly EventKind Kind;
        public readonly Address Address;
        public readonly byte[] Data;

        public Event(EventKind kind, Address address, byte[] data = null)
        {
            this.Kind = kind;
            this.Address = address;
            this.Data = data;
        }

        public T GetKind<T>()
        {
            return (T)(object)Kind;
        }

        public T GetContent<T>()
        {
            return Serialization.Unserialize<T>(this.Data);
        }

        public void Serialize(BinaryWriter writer)
        {
            var n = (int)(object)this.Kind; // TODO is this the most clean way to do this?
            writer.Write((byte)n);
            writer.WriteAddress(this.Address);
            writer.WriteByteArray(this.Data);
        }

        internal static Event Unserialize(BinaryReader reader)
        {
            var kind = (EventKind)reader.ReadByte();
            var address = reader.ReadAddress();
            var data = reader.ReadByteArray();
            return new Event(kind, address, data);
        }
    }

    public static class EventKindExtensions
    {
        public static T DecodeCustomEvent<T>(this EventKind kind)
        {
            if (kind < EventKind.Custom)
            {
                throw new Exception("Cannot cast system event");
            }

            var type = typeof(T);
            if (!type.IsEnum)
            {
                throw new Exception("Can only cast event to other enum");
            }

            var intVal = ((int)kind - (int)EventKind.Custom);
            var temp = (T)Enum.Parse(type, intVal.ToString());
            return temp;
        }

        public static EventKind EncodeCustomEvent(Enum kind)
        {
            var temp = (EventKind)((int)Convert.ChangeType(kind, kind.GetTypeCode()) + (int)EventKind.Custom);
            return temp;
        }
    }
}