using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Contracts.Native
{
    public struct SwapPair
    {
        public string Symbol;
        public BigInteger Value;
    }

    public sealed class SwapContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Swap;

        public SwapContract() : base()
        {
        }

        public bool IsSupportedToken(string symbol)
        {
            if (!Runtime.TokenExists(symbol))
            {
                return false;
            }

            if (symbol == DomainSettings.StakingTokenSymbol)
            {
                return true;
            }

            if (symbol == DomainSettings.FuelTokenSymbol)
            {
                return true;
            }

            var info = Runtime.GetToken(symbol);
            return info.IsFungible() && info.Flags.HasFlag(TokenFlags.External);
        }

        // returns how many tokens would be obtained by trading from one type of another
        public BigInteger GetRate(string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(fromSymbol != toSymbol, "invalid pair");

            Runtime.Expect(IsSupportedToken(fromSymbol), "unsupported from symbol");
            Runtime.Expect(IsSupportedToken(toSymbol), "unsupported to symbol");

            var toBalance = GetAvailableForSymbol(toSymbol);
            Runtime.Expect(toBalance > 0, toSymbol + " not available in pot");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(fromInfo.IsFungible(), "must be fungible");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(toInfo.IsFungible(), "must be fungible");

            var rate = Runtime.GetTokenQuote(fromSymbol, toSymbol, amount);
            return rate;
        }

        public void DepositTokens(Address from, string symbol, BigInteger amount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "address must be user address");

            Runtime.Expect(IsSupportedToken(symbol), "token is unsupported");

            var info = Runtime.GetToken(symbol);
            var unitAmount = UnitConversion.GetUnitValue(info.Decimals);
            Runtime.Expect(amount >= unitAmount, "invalid amount");

            Runtime.TransferTokens(symbol, from, this.Address, amount);
        }

        private BigInteger GetAvailableForSymbol(string symbol)
        {
            return Runtime.GetBalance(symbol, this.Address);
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
            var fromInfo = Runtime.GetToken(fromSymbol);
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

        public void SwapFee(Address from, string fromSymbol, BigInteger feeAmount)
        {
            var amount = Runtime.GetTokenQuote(DomainSettings.FuelTokenSymbol, fromSymbol, feeAmount);
            Runtime.Expect(amount > 0, $"cannot swap {fromSymbol} as fee");

            var balance = Runtime.GetBalance(DomainSettings.FuelTokenSymbol, from);

            amount -= balance;

            if (amount > 0)
            {
                SwapTokens(from, fromSymbol, DomainSettings.FuelTokenSymbol, amount);
            }
        }

        public void SwapTokens(Address from, string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "address must be user address");
            Runtime.Expect(amount > 0, "invalid amount");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(IsSupportedToken(fromSymbol), "source token is unsupported");

            var fromBalance = Runtime.GetBalance(fromSymbol, from);
            Runtime.Expect(fromBalance > 0, $"not enough {fromSymbol} balance");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(IsSupportedToken(toSymbol), "destination token is unsupported");

            var toBalance = GetAvailableForSymbol(toSymbol);

            Runtime.Expect(toBalance > 0, $"not enough balance o f {toSymbol} available in the pot");

            var total = GetRate(fromSymbol, toSymbol, amount);

            Runtime.Expect(toBalance >= total, "insufficient balance in pot");

            Runtime.TransferTokens(fromSymbol, from, this.Address, amount);
            Runtime.TransferTokens(toSymbol, this.Address, from, total);
        }
    }
}
