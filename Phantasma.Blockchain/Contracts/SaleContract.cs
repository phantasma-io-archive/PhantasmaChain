using System;
using System.Numerics;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Storage;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts
{
    [Flags]
    public enum SaleFlags
    {
        None = 0,
        Whitelist = 1,
    }

    public enum SaleEventKind
    {
        Creation,
        SoftCap,
        HardCap,
        AddedToWhitelist,
        RemovedFromWhitelist,
        Distribution,
        Refund,
        PriceChange,
        Participation,
    }

    public struct SaleEventData
    {
        public Hash saleHash;
        public SaleEventKind kind;
    }

    public struct SaleInfo
    {
        public Address Creator;
        public string Name;
        public SaleFlags Flags;
        public Timestamp StartDate;
        public Timestamp EndDate;

        public string SellSymbol;
        public string ReceiveSymbol;
        public BigInteger Price;
        public BigInteger GlobalSoftCap;
        public BigInteger GlobalHardCap;
        public BigInteger UserSoftCap;
        public BigInteger UserHardCap;
    }

    public sealed class SaleContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Sale;

        internal StorageMap _saleMap; //<Hash, Collection<StorageEntry>>
        internal StorageMap _buyerAmounts; //<Hash, Collection<StorageEntry>>
        internal StorageMap _buyerAddresses; //<Hash, Collection<StorageEntry>>
        internal StorageMap _whitelistedAddresses; //<Hash, Collection<StorageEntry>>
        internal StorageList _saleList; //List<Hash>
        internal StorageMap _saleSupply; //Map<Hash, BigInteger>


        public SaleContract() : base()
        {
        }

        public Hash CreateSale(Address from, string name, SaleFlags flags, Timestamp startDate, Timestamp endDate, string sellSymbol, string receiveSymbol, BigInteger price, BigInteger globalSoftCap, BigInteger globalHardCap, BigInteger userSoftCap, BigInteger userHardCap)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(Runtime.TokenExists(sellSymbol), "token must exist: " + sellSymbol);

            var token = Runtime.GetToken(sellSymbol);
            Runtime.Expect(token.IsFungible(), "token must be fungible: " + sellSymbol);
            Runtime.Expect(token.IsTransferable(), "token must be transferable: " + sellSymbol);

            Runtime.Expect(price >= 1, "invalid price");
            Runtime.Expect(globalSoftCap >= 0, "invalid softcap");
            Runtime.Expect(globalHardCap > 0, "invalid hard cap");
            Runtime.Expect(globalHardCap >= globalSoftCap, "hard cap must be larger or equal to soft capt");
            Runtime.Expect(userSoftCap >= 0, "invalid user soft cap");
            Runtime.Expect(userHardCap >= userSoftCap, "invalid user hard cap");

            Runtime.Expect(receiveSymbol != sellSymbol, "invalid receive token symbol: " + receiveSymbol);


            // TODO remove this later when Cosmic Swaps 2.0 are released
            Runtime.Expect(receiveSymbol == DomainSettings.StakingTokenSymbol, "invalid receive token symbol: " + receiveSymbol);

            Runtime.TransferTokens(sellSymbol, from, this.Address, globalHardCap);

            var sale = new SaleInfo()
            {
                Creator = from,
                Name = name,
                Flags = flags,
                StartDate = startDate,
                EndDate = endDate,
                SellSymbol = sellSymbol,
                ReceiveSymbol = receiveSymbol,
                Price = price,
                GlobalSoftCap = globalSoftCap,
                GlobalHardCap = globalHardCap,
                UserSoftCap = userSoftCap,
                UserHardCap = userHardCap,
            };

            var bytes = Serialization.Serialize(sale);
            var hash = Hash.FromBytes(bytes);

            _saleList.Add(hash);
            _saleMap.Set(hash, sale);
            _saleSupply.Set<Hash, BigInteger>(hash, 0);

            Runtime.Notify(EventKind.Crowdsale, from, new SaleEventData() { kind = SaleEventKind.Creation, saleHash = hash });

            return hash;
        }

        public bool IsSaleActive(Hash saleHash)
        {
            if (_saleMap.ContainsKey(saleHash))
            {
                var sale = _saleMap.Get<Hash, SaleInfo>(saleHash);

                if (Runtime.Time < sale.StartDate)
                {
                    return false;
                }

                if (Runtime.Time > sale.EndDate)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        public SaleInfo GetSale(Hash saleHash)
        {
            return _saleMap.Get<Hash, SaleInfo>(saleHash);
        }

        public Address[] GetSaleParticipants(Hash saleHash)
        {
            var addressMap = _buyerAddresses.Get<Hash, StorageList>(saleHash);
            return addressMap.All<Address>();
        }

        public Address[] GetSaleWhitelists(Hash saleHash)
        {
            var addressMap = _whitelistedAddresses.Get<Hash, StorageList>(saleHash);
            return addressMap.All<Address>();
        }

        public bool IsWhitelisted(Hash saleHash, Address address)
        {
            var addressMap = _whitelistedAddresses.Get<Hash, StorageList>(saleHash);

            return addressMap.Contains<Address>(address);
        }

        public void AddToWhitelist(Hash saleHash, Address target)
        {
            Runtime.Expect(_saleMap.ContainsKey<Hash>(saleHash), "sale does not exist");

            var sale = _saleMap.Get<Hash, SaleInfo>(saleHash);
            Runtime.Expect(Runtime.Time < sale.EndDate, "sale has reached end date");

            Runtime.Expect(sale.Flags.HasFlag(SaleFlags.Whitelist), "this sale is not using whitelists");

            Runtime.Expect(Runtime.IsWitness(sale.Creator), "invalid witness");
            Runtime.Expect(target != sale.Creator, "sale creator can't be whitelisted");

            var addressMap = _whitelistedAddresses.Get<Hash, StorageList>(saleHash);

            if (!addressMap.Contains<Address>(target))
            {
                addressMap.Add<Address>(target);
                Runtime.Notify(EventKind.Crowdsale, target, new SaleEventData() { kind = SaleEventKind.AddedToWhitelist, saleHash = saleHash });
            }
        }

        public void RemoveFromWhitelist(Hash saleHash, Address target)
        {
            Runtime.Expect(_saleMap.ContainsKey<Hash>(saleHash), "sale does not exist");

            var sale = _saleMap.Get<Hash, SaleInfo>(saleHash);
            Runtime.Expect(Runtime.Time < sale.EndDate, "sale has reached end date");

            Runtime.Expect(sale.Flags.HasFlag(SaleFlags.Whitelist), "this sale is not using whitelists");

            Runtime.Expect(Runtime.IsWitness(sale.Creator), "invalid witness");

            var addressMap = _whitelistedAddresses.Get<Hash, StorageList>(saleHash);

            if (addressMap.Contains<Address>(target))
            {
                addressMap.Remove<Address>(target);
                Runtime.Notify(EventKind.Crowdsale, target, new SaleEventData() { kind = SaleEventKind.RemovedFromWhitelist, saleHash = saleHash });
            }
        }

        public BigInteger GetPurchasedAmount(Hash saleHash, Address address)
        {
            var amountMap = _buyerAmounts.Get<Hash, StorageMap>(saleHash);
            var totalAmount = amountMap.Get<Address, BigInteger>(address);
            return totalAmount;
        }

        public BigInteger GetSoldAmount(Hash saleHash)
        {
            var total = _saleSupply.Get<Hash, BigInteger>(saleHash);
            return total;
        }

        public void Purchase(Address from, Hash saleHash, string quoteSymbol, BigInteger quoteAmount)
        {
            //For now, prevent purchases with other tokens 
            Runtime.Expect(quoteSymbol == DomainSettings.StakingTokenSymbol, "invalid receive token symbol: " + quoteSymbol + ". SOUL token must be used for purchase");

            Runtime.Expect(Runtime.TokenExists(quoteSymbol), "token must exist: " + quoteSymbol);
            var quoteToken = Runtime.GetToken(quoteSymbol);

            Runtime.Expect(_saleMap.ContainsKey<Hash>(saleHash), "sale does not exist");
            var sale = _saleMap.Get<Hash, SaleInfo>(saleHash);

            Runtime.Expect(Runtime.Time >= sale.StartDate, "sale has not started");
            Runtime.Expect(Runtime.Time < sale.EndDate, "sale has reached end date");

            Runtime.Expect(quoteSymbol != sale.SellSymbol, "cannot participate in the sale using " + quoteSymbol);
            Runtime.Expect(from != sale.Creator, "sale creator can't participate");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            if (sale.Flags.HasFlag(SaleFlags.Whitelist))
            {
                Runtime.Expect(IsWhitelisted(saleHash, from), "address is not whitelisted");
            }

            var saleToken = Runtime.GetToken(sale.SellSymbol);
            var convertedAmount = Runtime.ConvertQuoteToBase(quoteAmount, UnitConversion.GetUnitValue(quoteToken.Decimals), saleToken, quoteToken) * sale.Price;

            var temp = UnitConversion.ToDecimal(convertedAmount, saleToken.Decimals);
            Runtime.Expect(temp >= 1, "cannot purchase very tiny amount");

            var previousSupply = _saleSupply.Get<Hash, BigInteger>(saleHash);
            var nextSupply = previousSupply + convertedAmount;

            //Runtime.Expect(nextSupply <= sale.HardCap, "hard cap reached");
            if (nextSupply > sale.GlobalHardCap)
            {
                convertedAmount = sale.GlobalHardCap - previousSupply;
                Runtime.Expect(convertedAmount > 0, "hard cap reached");
                quoteAmount = Runtime.ConvertBaseToQuote(convertedAmount, sale.Price, saleToken, quoteToken);
                nextSupply = 0;
            }

            Runtime.TransferTokens(quoteSymbol, from, this.Address, quoteAmount);
            Runtime.Notify(EventKind.Crowdsale, from, new SaleEventData() { kind = SaleEventKind.Participation, saleHash = saleHash });

            _saleSupply.Set<Hash, BigInteger>(saleHash, nextSupply);

            if (nextSupply == 0)
            {
                Runtime.Notify(EventKind.Crowdsale, from, new SaleEventData() { kind = SaleEventKind.HardCap, saleHash = saleHash });
            }
            else
            if (previousSupply < sale.GlobalSoftCap && nextSupply >= sale.GlobalSoftCap)
            {
                Runtime.Notify(EventKind.Crowdsale, from, new SaleEventData() { kind = SaleEventKind.SoftCap, saleHash = saleHash });
            }

            if (quoteSymbol != sale.ReceiveSymbol)
            {
                Runtime.CallNativeContext(NativeContractKind.Swap, nameof(SwapContract.SwapTokens), this.Address, quoteSymbol, sale.ReceiveSymbol, quoteAmount);
            }

            var amountMap = _buyerAmounts.Get<Hash, StorageMap>(saleHash);
            var totalAmount = amountMap.Get<Address, BigInteger>(from);

            var newAmount = totalAmount + convertedAmount;

            if (sale.UserSoftCap > 0)
            {
                Runtime.Expect(newAmount >= sale.UserSoftCap, "user purchase minimum limit not reached");
            }

            if (sale.UserHardCap > 0)
            {
                Runtime.Expect(newAmount <= sale.UserHardCap, "user purchase maximum limit exceeded");
            }

            var addressMap = _buyerAddresses.Get<Hash, StorageList>(saleHash);
            if (!addressMap.Contains<Address>(from))
            {
                addressMap.Add<Address>(from);
            }

            amountMap.Set<Address, BigInteger>(from, newAmount);
        }

        // anyone can call this, not only manager, in order to be able to trigger refunds
        public void CloseSale(Address from, Hash saleHash)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(_saleMap.ContainsKey(saleHash), "sale does not exist or already closed");

            var sale = _saleMap.Get<Hash, SaleInfo>(saleHash);

            Runtime.Expect(Runtime.Time > sale.EndDate, "sale still not reached end date");

            var soldSupply = _saleSupply.Get<Hash, BigInteger>(saleHash);
            var buyerAddresses = GetSaleParticipants(saleHash);

            var amountMap = _buyerAmounts.Get<Hash, StorageMap>(saleHash);

            var saleToken = Runtime.GetToken(sale.SellSymbol);
            var receiveToken = Runtime.GetToken(sale.ReceiveSymbol);

            if (soldSupply >= sale.GlobalSoftCap) // if at least soft cap reached, send tokens to buyers and funds to sellers
            {
                foreach (var buyer in buyerAddresses)
                {
                    var amount = amountMap.Get<Address, BigInteger>(buyer);
                    Runtime.TransferTokens(sale.SellSymbol, this.Address, buyer, amount);
                }

                var fundsAmount = Runtime.ConvertBaseToQuote(soldSupply, UnitConversion.GetUnitValue(receiveToken.Decimals), saleToken, receiveToken);
                fundsAmount /= sale.Price;
                Runtime.TransferTokens(sale.ReceiveSymbol, this.Address, sale.Creator, fundsAmount);
            }
            else // otherwise return funds to buyers and return tokens to sellers
            {
                foreach (var buyer in buyerAddresses)
                {
                    var amount = amountMap.Get<Address, BigInteger>(buyer);

                    amount = Runtime.ConvertBaseToQuote(amount, sale.Price, saleToken, receiveToken);
                    Runtime.TransferTokens(sale.ReceiveSymbol, this.Address, buyer, amount);
                }

                Runtime.TransferTokens(sale.SellSymbol, this.Address, sale.Creator, sale.GlobalHardCap);
            }
        }

        public Hash GetLatestSaleHash()
        {
            var count = (int)_saleList.Count();

            if (count <= 0)
            {
                return Hash.Null;
            }

            var index = count - 1;
            var firstHash = _saleList.Get<Hash>(index);
            return firstHash;
        }

        public void EditSalePrice(Hash saleHash, BigInteger price)
        {
            Runtime.Expect(_saleMap.ContainsKey(saleHash), "sale does not exist or already closed");

            var sale = _saleMap.Get<Hash, SaleInfo>(saleHash);

            Runtime.Expect(Runtime.IsWitness(sale.Creator), "invalid witness");

            Runtime.Expect(Runtime.Time < sale.EndDate, "sale has reached end date");

            Runtime.Expect(price > 0, "invalid price");
            Runtime.Expect(price != sale.Price, "price must be different");

            var soldSupply = _saleSupply.Get<Hash, BigInteger>(saleHash);
            Runtime.Expect(soldSupply == 0, "sale already started");

            Runtime.Expect(Runtime.Time < sale.EndDate, "sale already reached end date");

            sale.Price = price;
            _saleMap.Set<Hash, SaleInfo>(saleHash, sale);

            Runtime.Notify(EventKind.Crowdsale, sale.Creator, new SaleEventData() { kind = SaleEventKind.PriceChange, saleHash = saleHash });
        }
    }
}
