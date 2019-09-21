using System;
using System.IO;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.Blockchain.Contracts
{
    public enum EventKind
    {
        Unknown = 0,
        ChainCreate = 1,
        BlockCreate = 2,
        Unused2 = 3,
        TokenCreate = 4,
        TokenSend = 5,
        TokenReceive = 6,
        TokenMint = 7,
        TokenBurn = 8,
        TokenEscrow = 9,
        TokenStake = 10,
        TokenUnstake = 11,
        TokenClaim = 12,
        RoleDemote = 13,
        RolePromote = 14,
        AddressRegister = 15,
        AddressLink = 16,
        AddressUnlink = 17,
        GasEscrow = 18,
        GasPayment = 19,
        GasLoan = 20,
        OrderCreated = 21,
        OrderCancelled = 23,
        OrderFilled = 24,
        OrderClosed = 25,
        FeedCreate = 26,
        FeedUpdate = 27,
        FileCreate = 28,
        FileDelete = 29,
        ValidatorAdd = 30,
        ValidatorRemove = 31,
        BrokerRequest = 32,
        ValueCreate = 33,
        ValueUpdate = 34,
        PollStarted = 35,
        PollFinished = 36,
        PollVote = 37,
        Metadata = 47,
        Custom = 48,
    }

    public struct Event
    {
        public readonly EventKind Kind;
        public readonly Address Address;
        public readonly string Contract;
        public readonly byte[] Data;

        public Event(EventKind kind, Address address, string contract, byte[] data = null)
        {
            this.Kind = kind;
            this.Address = address;
            this.Contract = contract;
            this.Data = data;
        }

        public override string ToString()
        {
            return $"{Kind}/{Contract} @ {Address}: {Base16.Encode(Data)}";
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
            writer.WriteVarString(this.Contract);
            writer.WriteByteArray(this.Data);
        }

        public static Event Unserialize(BinaryReader reader)
        {
            var kind = (EventKind)reader.ReadByte();
            var address = reader.ReadAddress();
            var contract = reader.ReadVarString();
            var data = reader.ReadByteArray();
            return new Event(kind, address, contract, data);
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