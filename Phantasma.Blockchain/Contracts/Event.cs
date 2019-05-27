using System.IO;
using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

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
        Transfer                    = 23,
        Deposit                     = 24,
        Withdraw                    = 25,
        WrestlerReceived            = 26,
        WrestlerSent                = 27,
        Purchase                    = 28,
        ItemAdded                   = 29,
        ItemRemoved                 = 30,
        ItemReceived                = 31,
        ItemSent                    = 32,
        ItemSpent                   = 33,
        ItemActivated               = 34,
        ItemUnwrapped               = 35,
        Stance                      = 36,
        StatusAdded                 = 37,
        StatusRemoved               = 38,
        Buff                        = 39,
        Debuff                      = 40,
        Experience                  = 41,
        Unlock                      = 42,
        Rename                      = 43,
        Auto                        = 44,
        PotPrize                    = 45,
        Referral                    = 46,
        //ReferralStake =
        //ReferralUnstake = 
        Trophy                      = 47,
        Confusion                   = 48,
        MoveMiss                    = 49,
        SelectAvatar                = 50,
        CollectFactionReward        = 51,
        CollectChampionshipReward   = 52,
        CollectVipWrestlerReward    = 53,
        CollectVipItemReward        = 54,
        CollectVipMakeUpReward      = 55,
        PlayerJoinFaction           = 56,
        MysteryStake                = 57,
        MysteryUnstake              = 58,

        // casino => REMOVE LATER
        CasinoTableQueued   = 60,
        CasinoTableStart    = 61,
        CasinoTableCard     = 62,
        CasinoTableTurn     = 63,
        CasinoTableResult   = 64,
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
