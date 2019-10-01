using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Utils;
using System.IO;

namespace Phantasma.Domain
{
    public enum EventKind
    {
        Unknown = 0,
        ChainCreate = 1,
        BlockCreate = 2,
        BlockClose = 3,
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
        ValidatorPropose = 30,
        ValidatorElect = 31,
        ValidatorRemove = 32,
        ValidatorSwitch = 33,
        BrokerRequest = 34,
        ValueCreate = 35,
        ValueUpdate = 36,
        PollCreated = 37,
        PollClosed = 38,
        PollVote = 39,
        ChannelCreate = 40,
        ChannelRefill = 41,
        ChannelSettle = 42,
        LeaderboardCreate = 43,
        LeaderboardInsert = 44,
        LeaderboardReset = 45,
        PlatformCreate = 46,
        TransactionSettle = 47,
        PackedNFT = 48,
        Custom = 64,
    }

    public struct TokenEventData
    {
        public readonly string Symbol;
        public readonly BigInteger Value;
        public readonly string ChainName;

        public TokenEventData(string symbol, BigInteger value, string chainName)
        {
            this.Symbol = symbol;
            this.Value = value;
            this.ChainName = chainName;
        }
    }

    public struct RoleEventData
    {
        public string role;
        public Timestamp date;
    }

    public struct TransactionSettleEventData
    {
        public readonly Hash Hash;
        public readonly string Chain;

        public TransactionSettleEventData(Hash hash, string chain)
        {
            Hash = hash;
            Chain = chain;
        }
    }

    public struct GasEventData
    {
        public readonly Address address;
        public readonly BigInteger price;
        public readonly BigInteger amount;

        public GasEventData(Address address, BigInteger price, BigInteger amount)
        {
            this.address = address;
            this.price = price;
            this.amount = amount;
        }
    }

    public struct Event
    {
        public EventKind Kind { get; private set; }
        public Address Address { get; private set; }
        public string Contract { get; private set; }
        public byte[] Data { get; private set; }

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
}
