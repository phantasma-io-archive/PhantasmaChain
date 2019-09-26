using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct SwapPair
    {
        public string Symbol;
        public BigInteger Value;
    }

    public sealed class SwapContract : SmartContract
    {
        public override string Name => Nexus.SwapContractName;

        public SwapContract() : base()
        {
        }

        public bool IsSupportedToken(string symbol)
        {
            if (!Runtime.Nexus.TokenExists(symbol))
            {
                return false;
            }

            if (symbol == Nexus.StakingTokenSymbol)
            {
                return true;
            }

            if (symbol == Nexus.FuelTokenSymbol)
            {
                return true;
            }

            var info = Runtime.Nexus.GetTokenInfo(symbol);
            return info.IsFungible() && info.Flags.HasFlag(TokenFlags.External);
        }

        // returns how many tokens would be obtained by trading from one type of another
        public BigInteger GetRate(string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(fromSymbol != toSymbol, "invalid pair");

            Runtime.Expect(IsSupportedToken(fromSymbol), "unsupported from symbol");
            Runtime.Expect(IsSupportedToken(toSymbol), "unsupported to symbol");

            var fromBalance = GetAvailableForSymbol(fromSymbol);
            Runtime.Expect(fromBalance > 0, fromSymbol + " not available in pot");

            var toBalance = GetAvailableForSymbol(toSymbol);
            Runtime.Expect(toBalance > 0, toSymbol + " not available in pot");

            var fromInfo = Runtime.Nexus.GetTokenInfo(fromSymbol);
            Runtime.Expect(fromInfo.IsFungible(), "must be fungible");

            var toInfo = Runtime.Nexus.GetTokenInfo(toSymbol);
            Runtime.Expect(toInfo.IsFungible(), "must be fungible");

            var rate = Runtime.GetTokenQuote(fromSymbol, toSymbol, amount);
            return rate;
        }

        public void DepositTokens(Address from, string symbol, BigInteger amount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "address must be user address");

            Runtime.Expect(IsSupportedToken(symbol), "token is unsupported");

            var info = Runtime.Nexus.GetTokenInfo(symbol);
            var unitAmount = UnitConversion.GetUnitValue(info.Decimals);
            Runtime.Expect(amount >= unitAmount, "invalid amount");

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, symbol, from, this.Address, amount), "tokens transfer failed");
            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = this.Address, symbol = symbol, value = amount });
        }

        private BigInteger GetAvailableForSymbol(string symbol)
        {
            var balances = new BalanceSheet(symbol);
            return balances.Get(this.Storage, this.Address);
        }

        // TODO optimize this method without using .NET native stuff
        public SwapPair[] GetAvailable()
        {
            var symbols = Runtime.Nexus.Tokens.Where(x => GetAvailableForSymbol(x) > 0);

            var result = new List<SwapPair>();

            foreach (var symbol in symbols)
            {
                var amount = GetAvailableForSymbol(symbol);
                result.Add( new SwapPair()
                {
                    Symbol = symbol,
                    Value = amount
                });
            }

            return result.ToArray();
        }

        // TODO optimize this method without using .NET native stuff
        public SwapPair[] GetRates(string fromSymbol, BigInteger amount)
        {
            var fromInfo = Runtime.Nexus.GetTokenInfo(fromSymbol);
            Runtime.Expect(fromInfo.IsFungible(), "must be fungible");

            var result = new List<SwapPair>();
            foreach (var toSymbol in Runtime.Nexus.Tokens)
            {
                if (toSymbol == fromSymbol)
                {
                    continue;
                }

                var toBalance = GetAvailableForSymbol(toSymbol);

                if (toBalance <= 0)
                {

                    continue;
                }

                var rate = GetRate(fromSymbol, toSymbol, amount);
                if (rate > 0)
                {
                    result.Add(new SwapPair()
                    {
                        Symbol = toSymbol,
                        Value = rate
                    });
                }
            }

            return result.ToArray();
        }

        public void SwapTokens(Address from, string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "address must be user address");
            Runtime.Expect(amount > 0, "invalid amount");

            var fromInfo = Runtime.Nexus.GetTokenInfo(fromSymbol);
            Runtime.Expect(IsSupportedToken(fromSymbol), "source token is unsupported");

            var toInfo = Runtime.Nexus.GetTokenInfo(toSymbol);
            Runtime.Expect(IsSupportedToken(toSymbol), "destination token is unsupported");

            var toBalance = GetAvailableForSymbol(toSymbol);

            Runtime.Expect(toBalance > 0, toSymbol + " not available in pot");

            var total = GetRate(fromSymbol, toSymbol, amount);

            Runtime.Expect(toBalance >= total, "insufficient balance in pot");

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, fromSymbol, from, this.Address, amount), "source tokens transfer failed");
            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, toSymbol, this.Address, from, total), "target tokens transfer failed");

            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = this.Address, symbol = fromSymbol, value = amount });
            Runtime.Notify(EventKind.TokenReceive, this.Address, new TokenEventData() { chainAddress = this.Address, symbol = fromSymbol, value = amount });
            Runtime.Notify(EventKind.TokenSend, this.Address, new TokenEventData() { chainAddress = this.Address, symbol = toSymbol, value = total });
            Runtime.Notify(EventKind.TokenReceive, from, new TokenEventData() { chainAddress = this.Address, symbol = toSymbol, value = total });
        }
    }
}
