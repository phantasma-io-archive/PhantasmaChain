using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct SwapPair
    {
        public string Symbol;
        public BigInteger Value;
    }

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

            var fromBalance = GetAvailableForSymbol(fromSymbol);
            var toBalance = GetAvailableForSymbol(toSymbol);

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

            var balance = GetAvailableForSymbol(symbol);
            balance += amount;
            _balances.Set<string, BigInteger>(symbol, balance);

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, symbol, from, this.Address, amount), "tokens transfer failed");
            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = this.Address, symbol = symbol, value = amount });
        }

        private BigInteger GetAvailableForSymbol(string symbol)
        {
            return _balances.ContainsKey<string>(symbol) ? _balances.Get<string, BigInteger>(symbol) : 0;
        }

        // TODO optimize this method without using .NET native stuff
        public SwapPair[] GetAvailable()
        {
            var resultSize = (int)_balances.Count();

            var result = new SwapPair[resultSize];
            int index = 0;
            foreach (var symbol in Runtime.Nexus.Tokens)
            {
                if (_balances.ContainsKey<string>(symbol))
                {
                    var amount = _balances.Get<string, BigInteger>(symbol);
                    result[index] = new SwapPair()
                    {
                        Symbol = symbol,
                        Value = amount
                    };

                    index++;
                }
            }

            return result;
        }

        // TODO optimize this method without using .NET native stuff
        public SwapPair[] GetRates(string symbol, BigInteger amount)
        {
            int resultSize = 0;
            foreach (var toSymbol in Runtime.Nexus.Tokens)
            {
                if (toSymbol == symbol)
                {
                    continue;
                }

                var rate = GetRate(symbol, toSymbol, amount);
                if (rate > 0)
                {
                    resultSize++;
                }
            }

            var result = new SwapPair[resultSize];
            int index = 0;
            foreach (var toSymbol in Runtime.Nexus.Tokens)
            {
                if (toSymbol == symbol)
                {
                    continue;
                }

                var rate = GetRate(symbol, toSymbol, amount);
                if (rate > 0)
                {
                    result[index] = new SwapPair()
                    {
                        Symbol = toSymbol,
                        Value = rate
                    };

                    index++;
                }
            }

            return result;
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

            var fromBalance = GetAvailableForSymbol(fromSymbol);
            var toBalance = GetAvailableForSymbol(toSymbol);

            Runtime.Expect(fromBalance > 0, "insufficient balance in pot");

            var ratio = toBalance / fromBalance;
            var limiter = ratio / 10;
            if (limiter < 2) limiter = 2;
            var maxAvailable = toBalance / limiter; 
            Runtime.Expect(total < maxAvailable, "balance limit reached"); // here should be < instead of <= because we can take more than half of the pot at once

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, fromSymbol, from, this.Address, amount), "source tokens transfer failed");
            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, toSymbol, this.Address, from, total), "target tokens transfer failed");
            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = this.Address, symbol = fromSymbol, value = amount });
            Runtime.Notify(EventKind.TokenReceive, from, new TokenEventData() { chainAddress = this.Address, symbol = toSymbol, value = total });
        }
    }
}
