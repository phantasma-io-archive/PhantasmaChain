using System.IO;
using Phantasma.Cryptography;
using Phantasma.IO;

namespace Phantasma.Blockchain.Contracts
{
    public enum EventKind
    {
        ChainCreate,
        TokenCreate,
        TokenSend,
        TokenReceive,
        TokenMint,
        TokenBurn,
        TokenEscrow,
        TokenStake,
        TokenUnstake,
        TokenClaim,
        MasterDemote,
        MasterPromote,
        AddressRegister,
        AddressAdd,
        AddressRemove,
        GasEscrow,
        GasPayment,
        AuctionCreated,
        AuctionCancelled,
        AuctionFilled,
        Metadata,

        Transfer,
        Deposit,
        Withdraw,
        WrestlerReceived,
        Purchase,
        Auction,
        ItemAdded,
        ItemRemoved,
        ItemReceived,
        ItemSpent,
        ItemActivated,
        ItemUnwrapped,
        Stance,
        StatusAdded,
        StatusRemoved,
        Buff,
        Debuff,
        Experience,
        Unlock,
        Rename,
        Auto,
        Pot,
        Referral,
        Trophy,
        Confusion,
        MoveMiss,
        AddFriend,
        RemoveFriend,
        SelectAvatar
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
