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
        RoleDemote = 10,
        RolePromote = 11,
        AddressRegister = 12,
        AddressLink = 13,
        AddressUnlink = 14,
        GasEscrow = 15,
        GasPayment = 16,
        GasLoan = 17,
        OrderCreated = 18,
        OrderCancelled = 19,
        OrderFilled = 20,
        OrderClosed = 21,
        FeedCreate = 22,
        FeedUpdate = 23,
        FileCreate = 24,
        FileDelete = 25,
        ValidatorAdd = 26,
        ValidatorRemove = 27,
        ValidatorUpdate = 28,
        BrokerRequest = 29,
        ValueCreate = 30,
        ValueUpdate = 31,
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