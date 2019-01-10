using Phantasma.Blockchain.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct MarketAuction
    {
        public readonly Address Creator;
        public readonly Timestamp StartDate;
        public readonly Timestamp EndDate;
        public readonly BigInteger ID;
        public readonly BigInteger Price;

        public MarketAuction(Address creator, Timestamp startDate, Timestamp endDate, BigInteger iD, BigInteger price)
        {
            Creator = creator;
            StartDate = startDate;
            EndDate = endDate;
            ID = iD;
            Price = price;
        }
    }

    public sealed class MarketContract : SmartContract
    {
        public override string Name => "exchange";

        private StorageMap _auctions; //<string, Collection<MarketAuction>>

        public MarketContract() : base()
        {
        }

        public void CreateAuction(Address from, string symbol, BigInteger ID, BigInteger price)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid base token");
            Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), "token must be non-fungible");

            var ownerships = Runtime.Chain.GetTokenOwnerships(token);
            var owner = ownerships.GetOwner(ID);
            Runtime.Expect(owner == from, "invalid owner");

            Runtime.Expect(token.Transfer(ownerships, from, Runtime.Chain.Address, ID), "transfer failed");

            var auction = new MarketAuction(from, Timestamp.Now, Timestamp.Now + TimeSpan.FromDays(5), ID, price);
            var list = _auctions.Get<string, StorageList>(symbol);
            list.Add(auction);
        }
    }
}
