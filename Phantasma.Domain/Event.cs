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
        TokenCreate = 2,
        TokenSend = 3,
        TokenReceive = 4,
        TokenMint = 5,
        TokenBurn = 6,
        TokenStake = 7,
        TokenClaim = 8,
        AddressRegister = 9,
        AddressLink = 10,
        AddressUnlink = 11,
        OrganizationCreate = 12,
        OrganizationAdd = 13,
        OrganizationRemove = 14,
        GasEscrow = 15,
        GasPayment = 16,
        AddressUnregister = 17,
        OrderCreated = 18,
        OrderCancelled = 19,
        OrderFilled = 20,
        OrderClosed = 21,
        FeedCreate = 22,
        FeedUpdate = 23,
        FileCreate = 24,
        FileDelete = 25,
        ValidatorPropose = 26,
        ValidatorElect = 27,
        ValidatorRemove = 28,
        ValidatorSwitch = 29,
        PackedNFT = 30,
        ValueCreate = 31,
        ValueUpdate = 32,
        PollCreated = 33,
        PollClosed = 34,
        PollVote = 35,
        ChannelCreate = 36,
        ChannelRefill = 37,
        ChannelSettle = 38,
        LeaderboardCreate = 39,
        LeaderboardInsert = 40,
        LeaderboardReset = 41,
        PlatformCreate = 42,
        ChainSwap = 43,
        ContractRegister = 44,
        ContractDeploy = 45,
        Migration = 46,
        Log = 47,
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

    public struct ChainValueEventData
    {
        public string Name;
        public BigInteger Value;
    }

    public struct TransactionSettleEventData
    {
        public readonly Hash Hash;
        public readonly string Platform;
        public readonly string Chain;

        public TransactionSettleEventData(Hash hash, string platform, string chain)
        {
            Hash = hash;
            Platform = platform;
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
