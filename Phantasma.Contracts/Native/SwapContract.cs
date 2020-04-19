using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Context;
using Phantasma.Storage.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Contracts.Native
{
    public struct SwapPair: ISerializable
    {
        public string Symbol;
        public BigInteger Value;

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Symbol);
            writer.WriteBigInteger(Value);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Symbol = reader.ReadVarString();
            Value = reader.ReadBigInteger();
        }
    }

    public sealed class SwapContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Swap;

        public const string SwapMakerFeePercentTag = "swap.fee.maker";
        public const string SwapTakerFeePercentTag = "swap.fee.taker";

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
            return info.IsFungible() && info.Flags.HasFlag(TokenFlags.Foreign);
        }

        // returns how many tokens would be obtained by trading from one type of another
        public BigInteger GetRate(string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(fromSymbol != toSymbol, "invalid pair");

            Runtime.Expect(IsSupportedToken(fromSymbol), "unsupported from symbol");
            Runtime.Expect(IsSupportedToken(toSymbol), "unsupported to symbol");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(fromInfo.IsFungible(), "must be fungible");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(toInfo.IsFungible(), "must be fungible");

            var rate = Runtime.GetTokenQuote(fromSymbol, toSymbol, amount);

            var fromPrice = Runtime.GetTokenPrice(fromSymbol);
            var toPrice = Runtime.GetTokenPrice(toSymbol);

            var feeTag = fromPrice > toPrice ? SwapMakerFeePercentTag : SwapTakerFeePercentTag; 

            var feePercent = Runtime.GetGovernanceValue(feeTag);
            var fee = (rate * feePercent) / 100;
            rate -= fee;

            if (rate < 0)
            {
                rate = 0;
            }

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
            var symbols = Runtime.GetTokens().Where(x => GetAvailableForSymbol(x) > 0);

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
            var symbols = Runtime.GetTokens();
            foreach (var toSymbol in symbols)
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
            var toSymbol = DomainSettings.FuelTokenSymbol;

            var feeBalance = Runtime.GetBalance(toSymbol, from);
            feeAmount -= feeBalance;
            if (feeAmount <= 0)
            {
                return;
            }

            var amount = GetRate(toSymbol, fromSymbol, feeAmount);

            var token = Runtime.GetToken(fromSymbol);
            if (token.Decimals == 0 && amount < 1)
            {
                amount = 1;
            }
            else
            {
                Runtime.Expect(amount > 0, $"cannot swap {fromSymbol} as a fee");
            }

            SwapTokens(from, fromSymbol, toSymbol, amount);

            var finalFeeBalance = Runtime.GetBalance(toSymbol, from);
            Runtime.Expect(finalFeeBalance >= feeAmount, $"something went wrong in swapfee");
        }

        public void SwapReverse(Address from, string fromSymbol, string toSymbol, BigInteger total)
        {
            var amount = GetRate(toSymbol, fromSymbol, total);
            Runtime.Expect(amount > 0, $"cannot reverse swap {fromSymbol}");
            SwapTokens(from, fromSymbol, toSymbol, amount);
        }

        public void SwapFiat(Address from, string fromSymbol, string toSymbol, BigInteger worth)
        {
            var amount = GetRate(DomainSettings.FiatTokenSymbol, fromSymbol, worth);

            var token = Runtime.GetToken(fromSymbol);
            if (token.Decimals == 0 && amount < 1)
            {
                amount = 1;
            }
            else
            {
                Runtime.Expect(amount > 0, $"cannot swap {fromSymbol} based on fiat quote");
            }

            SwapTokens(from, fromSymbol, toSymbol, amount);
        }

        public void SwapTokens(Address from, string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount > 0, "invalid amount");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(IsSupportedToken(fromSymbol), "source token is unsupported");

            var fromBalance = Runtime.GetBalance(fromSymbol, from);
            Runtime.Expect(fromBalance > 0, $"not enough {fromSymbol} balance");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(IsSupportedToken(toSymbol), "destination token is unsupported");

            var toBalance = GetAvailableForSymbol(toSymbol);

            Runtime.Expect(toBalance > 0, $"not enough balance of {toSymbol} available in the pot");

            var total = GetRate(fromSymbol, toSymbol, amount);
            Runtime.Expect(total > 0, "amount to swap needs to be larger than zero");
            Runtime.Expect(toBalance >= total, "insufficient balance in pot");

            var half = toBalance / 2;
            Runtime.Expect(total < half, $"taking too much {toSymbol} from pot at once");

            Runtime.TransferTokens(fromSymbol, from, this.Address, amount);
            Runtime.TransferTokens(toSymbol, this.Address, from, total);
        }
    }
}
