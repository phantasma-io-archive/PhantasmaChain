using System.IO;
using System.Numerics;
using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Storage.Utils;

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
        AddressMigration = 46,
        ContractUpgrade = 47,
        Log = 48,
        Inflation = 49,
        OwnerAdded = 50,
        OwnerRemoved = 51,
        DomainCreate = 52,
        DomainDelete = 53,
        TaskStart = 54,
        TaskStop = 55,
        CrownRewards = 56,
        Infusion = 57,
        Crowdsale = 58,
        OrderBid = 59,
        Custom = 64
    }

    public struct OrganizationEventData
    {
        public readonly string Organization;
        public readonly Address MemberAddress;

        public OrganizationEventData(string organization, Address memberAddress)
        {
            this.Organization = organization;
            this.MemberAddress = memberAddress;
        }
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

    public struct InfusionEventData
    {
        public readonly string BaseSymbol;
        public readonly BigInteger TokenID;
        public readonly string InfusedSymbol;
        public readonly BigInteger InfusedValue;
        public readonly string ChainName;

        public InfusionEventData(string baseSymbol, BigInteger tokenID, string infusedSymbol, BigInteger infusedValue, string chainName)
        {
            BaseSymbol = baseSymbol;
            TokenID = tokenID;
            InfusedSymbol = infusedSymbol;
            InfusedValue = infusedValue;
            ChainName = chainName;
        }
    }
    public enum TypeAuction
    {
        Fixed = 0,
        Classic = 1,
        Reserve = 2,
        Dutch = 3,
    }       

    public struct MarketEventData
    {
        public string BaseSymbol;
        public string QuoteSymbol;
        public BigInteger ID;
        public BigInteger Price;
        public BigInteger EndPrice;
        public TypeAuction Type;
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
