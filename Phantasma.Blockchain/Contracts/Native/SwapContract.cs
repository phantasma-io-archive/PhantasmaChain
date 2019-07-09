using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class SwapContract : SmartContract
    {
        public override string Name => "swap";

        internal StorageMap _balances; //<string, BigInteger> 
        internal BigInteger _total; 

        public SwapContract() : base()
        {
        }

        // returns how many tokens would be obtained by trading from one type of another
        public BigInteger GetRate(string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(fromSymbol != toSymbol, "invalid pair");

            Runtime.Expect(_balances.ContainsKey<string>(fromSymbol), fromSymbol + " not available in pot");
            Runtime.Expect(_balances.ContainsKey<string>(toSymbol), toSymbol + " not available in pot");

            var fromBalance = GetAvailable(fromSymbol);
            var toBalance = GetAvailable(toSymbol);

            var fromInfo = Runtime.Nexus.GetTokenInfo(fromSymbol);
            Runtime.Expect(fromInfo.IsFungible, "must be fungible");

            var toInfo = Runtime.Nexus.GetTokenInfo(toSymbol);
            Runtime.Expect(toInfo.IsFungible, "must be fungible");
            BigInteger total;

            if (fromBalance < toBalance)
            {
                total = UnitConversion.ToBigInteger((UnitConversion.ToDecimal(amount, fromInfo.Decimals) / UnitConversion.ToDecimal(toBalance, toInfo.Decimals)) * UnitConversion.ToDecimal(fromBalance + amount, fromInfo.Decimals), toInfo.Decimals);
            }
            else
            {
                total = UnitConversion.ToBigInteger((UnitConversion.ToDecimal(amount, fromInfo.Decimals) * UnitConversion.ToDecimal(fromBalance + amount, fromInfo.Decimals)) / UnitConversion.ToDecimal(toBalance, toInfo.Decimals), toInfo.Decimals);
            }

            return total;
        }

        public void DepositTokens(Address from, string symbol, BigInteger amount)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var info = Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(info.IsFungible, "must be fungible");

            var unitAmount = UnitConversion.GetUnitValue(info.Decimals);
            Runtime.Expect(amount >= unitAmount, "invalid amount");

            _total += amount;

            var balance = GetAvailable(symbol);
            balance += amount;
            _balances.Set<string, BigInteger>(symbol, balance);

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, symbol, from, Runtime.Chain.Address, amount), "tokens transfer failed");
            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = symbol, value = amount });
        }

        private BigInteger GetAvailable(string symbol)
        {
            return _balances.ContainsKey<string>(symbol) ? _balances.Get<string, BigInteger>(symbol) : 0;
        }

        public void SwapTokens(Address from, string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(amount > 0, "invalid amount");

            var fromInfo = Runtime.Nexus.GetTokenInfo(fromSymbol);
            Runtime.Expect(fromInfo.IsFungible, "must be fungible");

            var toInfo = Runtime.Nexus.GetTokenInfo(toSymbol);
            Runtime.Expect(toInfo.IsFungible, "must be fungible");

            Runtime.Expect(_balances.ContainsKey<string>(toSymbol), toSymbol + " not available in pot");

            var total = GetRate(fromSymbol, toSymbol, amount);

            var fromBalance = GetAvailable(fromSymbol);
            var toBalance = GetAvailable(toSymbol);

            Runtime.Expect(fromBalance > 0, "insufficient balance in pot");

            var ratio = toBalance / fromBalance;
            var limiter = ratio / 10;
            if (limiter < 2) limiter = 2;
            var maxAvailable = toBalance / limiter; 
            Runtime.Expect(total < maxAvailable, "balance limit reached"); // here should be < instead of <= because we can take more than half of the pot at once

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, fromSymbol, from, Runtime.Chain.Address, amount), "source tokens transfer failed");
            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, toSymbol, Runtime.Chain.Address, from, total), "target tokens transfer failed");
            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = fromSymbol, value = amount });
            Runtime.Notify(EventKind.TokenReceive, from, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = toSymbol, value = total });
        }
    }
}
