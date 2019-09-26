using Phantasma.Cryptography;

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
        Metadata = 47,
        Custom = 48,
    }

    public interface IEvent
    {
        EventKind Kind { get; }
        Address Address { get; }
        string Contract { get; }
        byte[] Data { get; }
    }
}
