using System.IO;
using Phantasma.Cryptography;
using Phantasma.IO;

namespace Phantasma.Blockchain.Contracts
{
    public enum EventKind
    {
        ChainCreate         = 0,
        TokenCreate         = 1,
        TokenSend           = 2,
        TokenReceive        = 3,
        TokenMint           = 4,
        TokenBurn           = 5,
        TokenEscrow         = 6,
        TokenStake          = 7,
        TokenUnstake        = 8,
        TokenClaim          = 9,
        MasterDemote        = 10,
        MasterPromote       = 11,
        AddressRegister     = 12,
        AddressAdd          = 13,
        AddressRemove       = 14,
        GasEscrow           = 15,
        GasPayment          = 16,
        AuctionCreated      = 17,
        AuctionCancelled    = 18,
        AuctionFilled       = 19,
        Metadata            = 20,
        AddFriend           = 21,
        RemoveFriend        = 22,

        // Nachomen Events -> Remover later
        Transfer            = 23,
        Deposit             = 24,
        Withdraw            = 25,
        WrestlerReceived    = 26,
        Purchase            = 27,
        ItemAdded           = 28,
        ItemRemoved         = 29,
        ItemReceived        = 30,
        ItemSpent           = 31,
        ItemActivated       = 32,
        ItemUnwrapped       = 33,
        Stance              = 34,
        StatusAdded         = 35,
        StatusRemoved       = 36,
        Buff                = 37,
        Debuff              = 38,
        Experience          = 39,
        Unlock              = 40,
        Rename              = 41,
        Auto                = 42,
        Pot                 = 43,
        Referral            = 44,
        Trophy              = 45,
        Confusion           = 46,
        MoveMiss            = 47,
        SelectAvatar        = 48,

        // casino => REMOVE LATER
        CasinoTableQueued = 60,
        CasinoTableStart = 61,
        CasinoTableCard= 62,
        CasinoTableTurn = 63,
        CasinoTableResult = 64,
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
}
