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
        TokenStake = 9,
        TokenClaim = 10,
        AddressRegister = 11,
        AddressLink = 12,
        AddressUnlink = 13,
        OrganizationCreate = 14,
        OrganizationAdd = 15,
        OrganizationRemove = 16,
        GasEscrow = 17,
        GasPayment = 18,
        AddressUnregister = 19,
        OrderCreated = 20,
        OrderCancelled = 21,
        OrderFilled = 22,
        OrderClosed = 23,
        FeedCreate = 24,
        FeedUpdate = 25,
        FileCreate = 26,
        FileDelete = 27,
        ValidatorPropose = 28,
        ValidatorElect = 29,
        ValidatorRemove = 30,
        ValidatorSwitch = 31,
        PackedNFT = 32,
        ValueCreate = 33,
        ValueUpdate = 34,
        PollCreated = 35,
        PollClosed = 36,
        PollVote = 37,
        ChannelCreate = 38,
        ChannelRefill = 39,
        ChannelSettle = 40,
        LeaderboardCreate = 41,
        LeaderboardInsert = 42,
        LeaderboardReset = 43,
        PlatformCreate = 44,
        ChainSwap = 45,
        ContractRegister = 46,
        ContractDeploy = 47,
        Migration = 48,
        Log = 49,
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
