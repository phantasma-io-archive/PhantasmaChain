using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Utils;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Xml;
using Phantasma.Storage.Context;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.VM;

namespace Phantasma.Blockchain.Contracts
{
    public struct LPTokenContentROM: ISerializable
    {
        public string Symbol0;
        public string Symbol1;
        public BigInteger ID;

        public LPTokenContentROM(string Symbol0, string Symbol1, BigInteger ID)
        {
            this.Symbol0 = Symbol0;
            this.Symbol1 = Symbol1;
            this.ID = ID;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Symbol0);
            writer.WriteVarString(Symbol1);
            writer.WriteBigInteger(ID);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Symbol0 = reader.ReadVarString();
            Symbol1 = reader.ReadVarString();
            ID = reader.ReadBigInteger();
        }
    }

    public struct LPTokenContentRAM : ISerializable
    {
        public BigInteger Amount0;
        public BigInteger Amount1;
        public BigInteger Liquidity;
        public BigInteger ClaimedFees;

        public LPTokenContentRAM(BigInteger Amount0, BigInteger Amount1, BigInteger Liquidity)
        {
            this.Amount0 = Amount0;
            this.Amount1 = Amount1;
            this.Liquidity = Liquidity;
            this.ClaimedFees = 0;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteBigInteger(Amount0);
            writer.WriteBigInteger(Amount1);
            writer.WriteBigInteger(Liquidity);
            writer.WriteBigInteger(ClaimedFees);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Amount0 = reader.ReadBigInteger();
            Amount1 = reader.ReadBigInteger();
            Liquidity = reader.ReadBigInteger();
            ClaimedFees = reader.ReadBigInteger();
        }
    }

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
    
    public struct TradingVolume: ISerializable
    {
        public string Symbol0;
        public string Symbol1;
        public string Day;
        public BigInteger Volume;

        public TradingVolume(string Symbol0, string Symbol1, string Day, BigInteger Volume)
        {
            this.Symbol0 = Symbol0;
            this.Symbol1 = Symbol1;
            this.Day = Day;
            this.Volume = Volume;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Symbol0);
            writer.WriteVarString(Symbol1);
            writer.WriteVarString(Day);
            writer.WriteBigInteger(Volume);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Symbol0 = reader.ReadVarString();
            Symbol1 = reader.ReadVarString();
            Day = reader.ReadVarString();
            Volume = reader.ReadBigInteger();
        }
    }

    public struct Pool: ISerializable
    {
        public string Symbol0; // Symbol
        public string Symbol1; // Pair
        public string Symbol0Address;
        public string Symbol1Address;
        public BigInteger Amount0;
        public BigInteger Amount1;
        public BigInteger FeeRatio;
        public BigInteger TotalLiquidity;
        public BigInteger FeesForUsers;
        public BigInteger FeesForOwner;


        public Pool(string Symbol0, string Symbol1, string Symbol0Address, string Symbol1Address, BigInteger Amount0, BigInteger Amount1, BigInteger FeeRatio, BigInteger TotalLiquidity)
        {
            this.Symbol0 = Symbol0;
            this.Symbol1 = Symbol1;
            this.Symbol0Address = Symbol0Address;
            this.Symbol1Address = Symbol1Address;
            this.Amount0 = Amount0;
            this.Amount1 = Amount1;
            this.FeeRatio = FeeRatio;
            this.TotalLiquidity = TotalLiquidity;
            this.FeesForUsers = 0;
            this.FeesForOwner = 0;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Symbol0);
            writer.WriteVarString(Symbol1);
            writer.WriteVarString(Symbol0Address);
            writer.WriteVarString(Symbol1Address);
            writer.WriteBigInteger(Amount0);
            writer.WriteBigInteger(Amount1);
            writer.WriteBigInteger(FeeRatio);
            writer.WriteBigInteger(TotalLiquidity);
            writer.WriteBigInteger(FeesForUsers);
            writer.WriteBigInteger(FeesForOwner);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Symbol0 = reader.ReadVarString();
            Symbol1 = reader.ReadVarString();
            Symbol0Address = reader.ReadVarString();
            Symbol1Address = reader.ReadVarString();
            Amount0 = reader.ReadBigInteger();
            Amount1 = reader.ReadBigInteger();
            FeeRatio = reader.ReadBigInteger();
            TotalLiquidity = reader.ReadBigInteger();
            FeesForUsers = reader.ReadBigInteger();
            FeesForOwner = reader.ReadBigInteger();
        }
    }

    public struct LPHolderInfo : ISerializable
    {
        public Address address;
        public BigInteger unclaimed;
        public BigInteger claimed;

        public LPHolderInfo(Address address, BigInteger unclaimed, BigInteger claimed)
        {
            this.address = address;
            this.unclaimed = unclaimed;
            this.claimed = claimed;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteAddress(address);
            writer.WriteBigInteger(unclaimed);
            writer.WriteBigInteger(claimed);
        }

        public void UnserializeData(BinaryReader reader)
        {
            address = reader.ReadAddress();
            unclaimed = reader.ReadBigInteger();
            claimed = reader.ReadBigInteger();
        }
    }

    public sealed class SwapContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Swap;

        internal BigInteger _DEXversion;

        public SwapContract() : base()
        {
        }

        public BigInteger GetSwapVersion()
        {
            if (_DEXversion > 0) // support for legacy versions
            {
                return 2 + _DEXversion;
            }
            else
            {
                if (Runtime.ProtocolVersion >= 3)
                {
                    return 2;
                }
                else
                {
                    return 1;
                }
            }
        }

        /// <summary>
        /// Check if the token is supported
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
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
            return info.IsFungible() && info.Flags.HasFlag(TokenFlags.Transferable);
        }

        public const string SwapMakerFeePercentTag = "swap.fee.maker";
        public const string SwapTakerFeePercentTag = "swap.fee.taker";

        // returns how many tokens would be obtained by trading from one type of another
        public BigInteger GetRate(string fromSymbol, string toSymbol, BigInteger amount)
        {
            var swapVersion = GetSwapVersion();

            if (swapVersion >= 3)
            {
                return GetRateV3(fromSymbol, toSymbol, amount);
            }
            else if (swapVersion == 2)
            {
                return GetRateV2(fromSymbol, toSymbol, amount);
            }
            else 
            {
                return GetRateV1(fromSymbol, toSymbol, amount);
            }
        }

        /// <summary>
        /// Old version to get Rate
        /// </summary>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private BigInteger GetRateV1(string fromSymbol, string toSymbol, BigInteger amount)
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

        /// <summary>
        /// Old Version to get rate.
        /// </summary>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private BigInteger GetRateV2(string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(fromSymbol != toSymbol, "invalid pair");

            Runtime.Expect(IsSupportedToken(fromSymbol), "unsupported from symbol");
            Runtime.Expect(IsSupportedToken(toSymbol), "unsupported to symbol");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(fromInfo.IsFungible(), "must be fungible");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(toInfo.IsFungible(), "must be fungible");

            var rate = Runtime.GetTokenQuote(fromSymbol, toSymbol, amount);

            Runtime.Expect(rate >= 0, "invalid swap rate");

            return rate;
        }
        
        /// <summary>
        /// Get the Rate for the trade (with fees included)
        /// </summary>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="amount">Amount of fromSymbol to Swap</param>
        /// <returns></returns>
        private BigInteger GetRateV3(string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(fromSymbol != toSymbol, "invalid pair");

            Runtime.Expect(IsSupportedToken(fromSymbol), "unsupported from symbol");
            Runtime.Expect(IsSupportedToken(toSymbol), $"unsupported to symbol -> {toSymbol}");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(fromInfo.IsFungible(), "must be fungible");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(toInfo.IsFungible(), "must be fungible");

            //Runtime.Expect(PoolExists(fromSymbol, toSymbol), $"Pool {fromSymbol}/{toSymbol} doesn't exist.");
            if ( !PoolIsReal(fromSymbol, toSymbol))
            {
                BigInteger rate1 = GetRate(fromSymbol, "SOUL", amount);
                BigInteger rate2 = GetRate("SOUL", toSymbol, rate1);
                return rate2;
            }

            BigInteger rate = 0;

            Pool pool = GetPool(fromSymbol, toSymbol);
            BigInteger tokenAmount = 0;

            //BigInteger power = 0;
            //BigInteger rateForSwap = 0;

            BigInteger feeAmount = pool.FeeRatio;

            bool canBeTraded = false;

            // dy = y * 0.997 * dx /  ( x + 0.997 * dx )
            if (pool.Symbol0 == fromSymbol)
            {
                tokenAmount = pool.Amount1 * (1 - feeAmount / 100) * amount / (pool.Amount0 + (1 - feeAmount / 100) * amount);
                canBeTraded = ValidateTrade(amount, tokenAmount, pool, true);
                //power = (BigInteger)Math.Pow((long)(pool.Amount0 - amount), 2);
                //rateForSwap = pool.Amount0 * pool.Amount1 * 10000000000 / power;
            }
            else
            {

                tokenAmount = pool.Amount0 * (1-feeAmount / 100) * amount / (pool.Amount1 + (1 - feeAmount / 100) * amount);
                canBeTraded = ValidateTrade(tokenAmount, amount, pool, false);
                //power = (BigInteger)Math.Pow((long)(pool.Amount1 + amount), 2);
                //rateForSwap =  pool.Amount0 * pool.Amount1 * 10000000000 / power;
            }

            rate = tokenAmount;
            Runtime.Expect(canBeTraded, "Can't be traded, the trade is not valid.");
            Runtime.Expect(rate >= 0, "invalid swap rate");

            return rate;
        }

        /// <summary>
        /// Method used to deposit tokens
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol"></param>
        /// <param name="amount"></param>
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

        /// <summary>
        /// Get Available for Symbol (Amount of tokens inside the contract)
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
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

                if (toSymbol == DomainSettings.LiquidityTokenSymbol)
                    continue;

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

        

        /// <summary>
        /// Method used to convert a Symbol into KCAL
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="feeAmount"></param>
        public void SwapFee(Address from, string fromSymbol, BigInteger feeAmount)
        {
            var protocol = Runtime.ProtocolVersion;

            var swapVersion = GetSwapVersion();

            if (swapVersion >= 3)
            {
                SwapFeeV3(from, fromSymbol, feeAmount);
            }
            else
            if (swapVersion == 2)
            {
                SwapFeeV2(from, fromSymbol, feeAmount);
            }
            else
            {
                SwapFeeV1(from, fromSymbol, feeAmount);
            }
        }

        /// <summary>
        /// Swap Fee -> Method used to convert a Symbol into KCAL, Using Pools
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="feeAmount"></param>
        private void SwapFeeV3(Address from, string fromSymbol, BigInteger feeAmount)
        {
            Runtime.Expect(_DEXversion >= 1, "call migrateV3 first");
            var feeSymbol = DomainSettings.FuelTokenSymbol;

            // Need to remove the fees
            var token = Runtime.GetToken(fromSymbol);
            BigInteger minAmount;


            var feeBalance = Runtime.GetBalance(feeSymbol, from);
            feeAmount -= UnitConversion.ConvertDecimals(feeBalance, DomainSettings.FuelTokenDecimals, token.Decimals);
            if (feeAmount <= 0)
            {
                return;
            }

            
            // different tokens have different decimals, so we need to make sure a certain minimum amount is swapped
            if (token.Decimals == 0)
            {
                minAmount = 1;
            }
            else
            {
                var diff = DomainSettings.FuelTokenDecimals - token.Decimals;
                if (diff > 0)
                {
                    minAmount = BigInteger.Pow(10, diff);
                }
                else
                {
                    minAmount = 1 * BigInteger.Pow(10, token.Decimals);
                }
            }

            if (!PoolIsReal(fromSymbol, feeSymbol))
            {
                var rate = GetRate(fromSymbol, "SOUL", feeAmount);
                SwapTokens(from, fromSymbol, "SOUL", feeAmount);
                SwapTokens(from, "SOUL", feeSymbol, rate);
                return;
            }else
            {
                var amountInOtherSymbol = GetRate(feeSymbol, fromSymbol, feeAmount);
                var amountIKCAL = GetRate(fromSymbol, feeSymbol, feeAmount);
                //Console.WriteLine($"AmountOther: {amountInOtherSymbol} | feeAmount:{feeAmount} | feeBalance:{feeBalance} | amountOfKcal: {amountIKCAL}" );

                if (amountInOtherSymbol < minAmount)
                {
                    amountInOtherSymbol = minAmount;
                }

                // round up
                //amountInOtherSymbol++;

                SwapTokens(from, fromSymbol, feeSymbol, feeAmount);
            }           

            var finalFeeBalance = Runtime.GetBalance(feeSymbol, from);
            Runtime.Expect(finalFeeBalance >= feeBalance, $"something went wrong in swapfee finalFeeBalance: {finalFeeBalance} feeBalance: {feeBalance}");
        }

        /// <summary>
        /// Swap fee old
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="feeAmount"></param>
        private void SwapFeeV2(Address from, string fromSymbol, BigInteger feeAmount)
        {
            var feeSymbol = DomainSettings.FuelTokenSymbol;
            var feeBalance = Runtime.GetBalance(feeSymbol, from);
            feeAmount -= feeBalance;
            if (feeAmount <= 0)
            {
                return;
            }

            var amountInOtherSymbol = GetRate(feeSymbol, fromSymbol, feeAmount);

            var token = Runtime.GetToken(fromSymbol);
            BigInteger minAmount;

            // different tokens have different decimals, so we need to make sure a certain minimum amount is swapped
            if (token.Decimals == 0)
            {
                minAmount = 1;
            }
            else
            {
                var diff = DomainSettings.FuelTokenDecimals - token.Decimals;
                if (diff > 0)
                {
                    minAmount = BigInteger.Pow(10, diff);
                }
                else
                {
                    minAmount = 1;
                }
            }

            if (amountInOtherSymbol < minAmount)
            {
                amountInOtherSymbol = minAmount;
            }

            // round up
            amountInOtherSymbol++;

            SwapTokens(from, fromSymbol, feeSymbol, amountInOtherSymbol);

            var finalFeeBalance = Runtime.GetBalance(feeSymbol, from);
            Runtime.Expect(finalFeeBalance >= feeAmount, $"something went wrong in swapfee finalFeeBalance: {finalFeeBalance} feeAmount: {feeAmount}");
        }

        /// <summary>
        /// Swap fee old
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="feeAmount"></param>
        private void SwapFeeV1(Address from, string fromSymbol, BigInteger feeAmount)
        {
            var toSymbol = DomainSettings.FuelTokenSymbol;
            var amount = GetRate(toSymbol, fromSymbol, feeAmount);
            var token = Runtime.GetToken(fromSymbol);
            if (token.Decimals == 0 && amount < 1)
            {
                amount = 1;
            }
            else
            {
                Runtime.Expect(amount > 0, $"cannot swap {fromSymbol} as a fee");

                var balance = Runtime.GetBalance(toSymbol, from);
                amount -= balance;
            }

            if (amount > 0)
            {
                SwapTokens(from, fromSymbol, toSymbol, amount);
            }
        }

        /// <summary>
        /// Swap Fiat, swap from USD to other token
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="worth"></param>
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

        /// <summary>
        /// Swap reverse
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="total"></param>
        public void SwapReverse(Address from, string fromSymbol, string toSymbol, BigInteger total)
        {
            var amount = GetRate(toSymbol, fromSymbol, total);
            Runtime.Expect(amount > 0, $"cannot reverse swap {fromSymbol}");
            SwapTokens(from, fromSymbol, toSymbol, amount);
        }

        /// <summary>
        /// Swap tokens
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="amount"></param>
        public void SwapTokens(Address from, string fromSymbol, string toSymbol, BigInteger amount)
        {
            var swapVersion = GetSwapVersion();

            if (swapVersion >= 3)
            {
                SwapTokensV3(from, fromSymbol, toSymbol, amount);
            }
            else
            {
                SwapTokensV2(from, fromSymbol, toSymbol, amount);
            }
        }

        /// <summary>
        /// Swap token OldVersion
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="amount"></param>
        public void SwapTokensV2(Address from, string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount > 0, "invalid amount");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(IsSupportedToken(fromSymbol), "source token is unsupported");

            var fromBalance = Runtime.GetBalance(fromSymbol, from);
            Runtime.Expect(fromBalance > 0, $"not enough {fromSymbol} balance");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(IsSupportedToken(toSymbol), "destination token is unsupported");

            var total = GetRate(fromSymbol, toSymbol, amount);
            Runtime.Expect(total > 0, "amount to swap needs to be larger than zero");

            var toPotBalance = GetAvailableForSymbol(toSymbol);

            if (toPotBalance < total && toSymbol == DomainSettings.FuelTokenSymbol)
            {
                var gasAddress = SmartContract.GetAddressForNative(NativeContractKind.Gas);
                var gasBalance = Runtime.GetBalance(toSymbol, gasAddress);
                if (gasBalance >= total)
                {
                    Runtime.TransferTokens(toSymbol, gasAddress, this.Address, total);
                    toPotBalance = total;
                }
            }

            var toSymbolDecimalsInfo = Runtime.GetToken(toSymbol);
            var toSymbolDecimals = Math.Pow(10, toSymbolDecimalsInfo.Decimals);
            var fromSymbolDecimalsInfo = Runtime.GetToken(fromSymbol);
            var fromSymbolDecimals = Math.Pow(10, fromSymbolDecimalsInfo.Decimals);
            Runtime.Expect(toPotBalance >= total, $"insufficient balance in pot, have {(double)toPotBalance / toSymbolDecimals} {toSymbol} in pot, need {(double)total / toSymbolDecimals} {toSymbol}, have {(double)fromBalance / fromSymbolDecimals} {fromSymbol} to convert from");

            var half = toPotBalance / 2;
            Runtime.Expect(total < half, $"taking too much {toSymbol} from pot at once");

            Runtime.TransferTokens(fromSymbol, from, this.Address, amount);
            Runtime.TransferTokens(toSymbol, this.Address, from, total);
        }

        /// <summary>
        /// Swap tokens Pool version (DEX Version)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="fromSymbol"></param>
        /// <param name="toSymbol"></param>
        /// <param name="amount"></param>
        public void SwapTokensV3(Address from, string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount > 0, $"invalid amount, need to be higher than 0 | {amount}");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(IsSupportedToken(fromSymbol), "source token is unsupported");

            var fromBalance = Runtime.GetBalance(fromSymbol, from);
            Runtime.Expect(fromBalance > 0, $"not enough {fromSymbol} balance");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(IsSupportedToken(toSymbol), "destination token is unsupported");

            var total = GetRate(fromSymbol, toSymbol, amount);
            Runtime.Expect(total > 0, "amount to swap needs to be larger than zero");


            if (!PoolIsReal(fromSymbol, toSymbol)){
                var rate = GetRate(fromSymbol, "SOUL", amount);
                SwapTokens(from, fromSymbol, "SOUL", amount);
                SwapTokens(from, "SOUL", toSymbol, rate);
                return;
            }

            // Validate Pools
            Runtime.Expect(PoolExists(fromSymbol, toSymbol), $"Pool {fromSymbol}/{toSymbol} doesn't exist.");

            Pool pool = GetPool(fromSymbol, toSymbol);

            BigInteger toPotBalance = 0;
            if (pool.Symbol0 == fromSymbol)
                toPotBalance = pool.Amount1;
            else
                toPotBalance = pool.Amount0;

            if (toPotBalance < total && toSymbol == DomainSettings.FuelTokenSymbol)
            {
                var gasAddress = SmartContract.GetAddressForNative(NativeContractKind.Gas);
                var gasBalance = Runtime.GetBalance(toSymbol, gasAddress);
                if (gasBalance >= total)
                {
                    Runtime.TransferTokens(toSymbol, gasAddress, this.Address, total);
                    toPotBalance = total;
                }
            }

            var toSymbolDecimalsInfo = Runtime.GetToken(toSymbol);
            var toSymbolDecimals = Math.Pow(10, toSymbolDecimalsInfo.Decimals);
            var fromSymbolDecimalsInfo = Runtime.GetToken(fromSymbol);
            var fromSymbolDecimals = Math.Pow(10, fromSymbolDecimalsInfo.Decimals);
            Runtime.Expect(toPotBalance >= total, $"insufficient balance in pot, have {(double)toPotBalance / toSymbolDecimals} {toSymbol} in pot, need {(double)total / toSymbolDecimals} {toSymbol}, have {(double)fromBalance / fromSymbolDecimals} {fromSymbol} to convert from");

            bool canBeTraded = false;
            if (pool.Symbol0 == fromSymbol)
                canBeTraded = ValidateTrade(amount, total, pool, true);
            else
                canBeTraded = ValidateTrade(total, amount, pool);

            Runtime.Expect(canBeTraded, $"The trade is not valid.");

            Runtime.TransferTokens(fromSymbol, from, this.Address, amount);
            Runtime.TransferTokens(toSymbol, this.Address, from, total);
            
            // Trading volume
            if (fromSymbol == DomainSettings.StakingTokenSymbol)
                UpdateTradingVolume(pool, amount);
            else
                UpdateTradingVolume(pool, total);

            // Handle Fees
            BigInteger totalFees = total*3/100;
            BigInteger feeForUsers = totalFees * 100 / UserPercent;
            BigInteger feeForOwner = totalFees * 100 / GovernancePercent;
            pool.FeesForUsers += feeForUsers;
            pool.FeesForOwner += feeForOwner;

            DistributeFee(feeForUsers, pool.Symbol0, pool.Symbol1);

            // Save Pool
            _pools.Set<string, Pool>($"{pool.Symbol0}_{pool.Symbol1}", pool);
        }


        /// <summary>
        /// Method use to Migrate to the new SwapMechanism
        /// </summary>
        public void MigrateToV3()
        {
            var owner = Runtime.GenesisAddress;
            Runtime.Expect(Runtime.IsWitness(owner), "invalid witness");

            Runtime.Expect(_DEXversion == 0, "Migration failed, wrong version");

            var existsLP = Runtime.TokenExists(DomainSettings.LiquidityTokenSymbol);
            Runtime.Expect(!existsLP, "LP token already exists!");

            var tokenScript = new byte[] { (byte)VM.Opcode.RET }; // TODO maybe fetch a pre-compiled Tomb script here, like for Crown?
            var abi = ContractInterface.Empty;
            Runtime.CreateToken(owner, DomainSettings.LiquidityTokenSymbol, DomainSettings.LiquidityTokenSymbol, 0, 0, TokenFlags.Transferable | TokenFlags.Burnable, tokenScript, abi);

            byte[] nftScript;
            ContractInterface nftABI;

            var url = "https://www.22series.com/part_info?id=*";
            Tokens.TokenUtils.GenerateNFTDummyScript(DomainSettings.LiquidityTokenSymbol, $"{DomainSettings.LiquidityTokenSymbol} #*", $"{DomainSettings.LiquidityTokenSymbol} #*", url, url, out nftScript, out nftABI);
            Runtime.CreateTokenSeries(DomainSettings.LiquidityTokenSymbol, this.Address, 1, 0, TokenSeriesMode.Unique, nftScript, nftABI);

            // check how much SOUL we have here
            var soulTotal = Runtime.GetBalance(DomainSettings.StakingTokenSymbol, this.Address);

            // creates a new pool for SOUL and every asset that has a balance in v2
            var symbols = Runtime.GetTokens();

            var tokens = new Dictionary<string, BigInteger>();

            // fetch all fungible tokens with balance > 0
            foreach (var symbol in symbols)
            {
                if (symbol == DomainSettings.StakingTokenSymbol)
                {
                    continue;
                }

                var info = Runtime.GetToken(symbol);
                if (info.IsFungible())
                {
                    var balance = Runtime.GetBalance(symbol, this.Address);

                    if (balance > 0)
                    {
                        tokens[symbol] = balance;
                    }
                }
            }

            // sort tokens by estimated SOUL value, from low to high
            var sortedTokens = tokens.Select(x => new KeyValuePair<string, BigInteger>(x.Key, GetRateV2(x.Key, DomainSettings.StakingTokenSymbol, x.Value)))
                .OrderBy(x => x.Value)
                .Select(x => x.Key)
                .ToArray();


            // Calculate the Percent to each Pool
            var tokensPrice = new Dictionary<string, BigInteger>();
            var soulPrice = Runtime.GetTokenQuote(DomainSettings.StakingTokenSymbol, DomainSettings.FiatTokenSymbol, UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals));
            var soulTotalPrice = soulPrice * UnitConversion.ConvertDecimals(soulTotal, DomainSettings.StakingTokenDecimals, DomainSettings.FiatTokenDecimals);
            BigInteger otherTokensTotalValue = 0;
            BigInteger totalTokenAmount = 0;
            BigInteger amount = 0;
            BigInteger tokenPrice = 0;
            BigInteger tokenRatio = 0;
            BigInteger totalPrice = 0;
            BigInteger tokenAmount = 0;
            BigInteger percent = 0;
            BigInteger soulAmount = 0;
            IToken tokenInfo;

            foreach (var symbol in sortedTokens)
            {
                tokenInfo = Runtime.GetToken(symbol);

                amount = UnitConversion.ConvertDecimals(tokens[symbol], tokenInfo.Decimals, DomainSettings.FiatTokenDecimals);
                tokenPrice = Runtime.GetTokenQuote(symbol, DomainSettings.FiatTokenSymbol, UnitConversion.ToBigInteger(1, tokenInfo.Decimals));

                //Console.WriteLine($"{symbol} price {tokenPrice}$  .{tokenInfo.Decimals}  { amount}x{tokenPrice} :{ amount * tokenPrice} -> {UnitConversion.ToDecimal(tokenPrice, DomainSettings.FiatTokenDecimals)}");
                tokensPrice[symbol] = tokenPrice;
                otherTokensTotalValue += amount * tokenPrice;
            }

            // Create Pools based on that percent and on the availableSOUL and on token ratio
            BigInteger totalSOULUsed = 0;
            _DEXversion = 1;

            if (otherTokensTotalValue < soulTotalPrice)
            {
                foreach (var symbol in sortedTokens)
                {
                    tokenInfo = Runtime.GetToken(symbol);
                    totalTokenAmount = UnitConversion.ConvertDecimals(tokens[symbol], tokenInfo.Decimals, DomainSettings.FiatTokenDecimals);
                    amount = tokens[symbol];
                    soulAmount = tokensPrice[symbol] * totalTokenAmount / soulPrice;
                    //Console.WriteLine($"TokenInfo |-> .{tokenInfo.Decimals} | ${tokensPrice[symbol]} | {tokens[symbol]} {symbol} | Converted {amount} {symbol} ");
                    //Console.WriteLine($"TradeValues |-> {percent}% | {tokenAmount} | {soulAmount}/{soulTotal}\n");
                    //Console.WriteLine($"{symbol} |-> .{tokenInfo.Decimals} | ${UnitConversion.ToDecimal(tokensPrice[symbol], DomainSettings.FiatTokenDecimals)} | {UnitConversion.ToDecimal(tokens[symbol], tokenInfo.Decimals)} {symbol} | Converted {UnitConversion.ToDecimal(amount, tokenInfo.Decimals)} {symbol}");
                    //Console.WriteLine($"{symbol} Ratio |-> {tokenRatio}% | {UnitConversion.ToDecimal(amount, tokenInfo.Decimals)} {symbol}");
                    //Console.WriteLine($"SOUL |-> {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)}/{UnitConversion.ToDecimal(soulTotal, DomainSettings.StakingTokenDecimals)} | {soulPrice} | {soulTotalPrice}");
                    //Console.WriteLine($"TradeValues |-> {percent}% | {amount} | {soulAmount}/{soulTotal} -> {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)} SOUL");
                    //Console.WriteLine($"Trade {UnitConversion.ToDecimal(amount, tokenInfo.Decimals)} {symbol} for {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)} SOUL\n");
                    totalSOULUsed += soulAmount;
                    CreatePool(this.Address, DomainSettings.StakingTokenSymbol, soulAmount, symbol, amount);
                }
            }
            else
            {
                // With Price Ratio
                foreach (var symbol in sortedTokens)
                {
                    tokenInfo = Runtime.GetToken(symbol);
                    totalTokenAmount = UnitConversion.ConvertDecimals(tokens[symbol], tokenInfo.Decimals, DomainSettings.FiatTokenDecimals);
                    amount = 0;
                    percent = tokensPrice[symbol] * totalTokenAmount * 100 / otherTokensTotalValue;
                    soulAmount = UnitConversion.ConvertDecimals((soulTotal * percent / 100), DomainSettings.FiatTokenDecimals, DomainSettings.StakingTokenDecimals);

                    tokenRatio = tokensPrice[symbol] / soulPrice;
                    if ( tokenRatio != 0)
                        tokenAmount = UnitConversion.ConvertDecimals((soulAmount / tokenRatio), DomainSettings.FiatTokenDecimals, tokenInfo.Decimals);
                    else
                    {
                        tokenRatio = soulPrice/tokensPrice[symbol];
                        tokenAmount = UnitConversion.ConvertDecimals((soulAmount * tokenRatio), DomainSettings.FiatTokenDecimals, tokenInfo.Decimals);
                    }

                    //Console.WriteLine($"{symbol} |-> .{tokenInfo.Decimals} | ${UnitConversion.ToDecimal(tokensPrice[symbol], DomainSettings.FiatTokenDecimals)} | {UnitConversion.ToDecimal(tokens[symbol], tokenInfo.Decimals)} {symbol} | Converted {UnitConversion.ToDecimal(tokenAmount, tokenInfo.Decimals)} {symbol}");
                    //Console.WriteLine($"{symbol} Ratio |-> {tokenRatio}% | {UnitConversion.ToDecimal(tokenAmount, tokenInfo.Decimals)} {symbol}");
                    //Console.WriteLine($"SOUL |-> {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)}/{UnitConversion.ToDecimal(soulTotal, DomainSettings.StakingTokenDecimals)} | {soulPrice} | {soulTotalPrice}");
                    //Console.WriteLine($"TradeValues |-> {percent}% | {tokenAmount} | {soulAmount}/{soulTotal} -> {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)} SOUL");
                    //Console.WriteLine($"Trade {UnitConversion.ToDecimal(tokenAmount, tokenInfo.Decimals)} {symbol} for {UnitConversion.ToDecimal(soulAmount, DomainSettings.StakingTokenDecimals)} SOUL\n");
                    totalSOULUsed += soulAmount;
                    Runtime.Expect(soulAmount <= soulTotal, $"SOUL higher than total... {soulAmount}/{soulTotal}");
                    CreatePool(this.Address, DomainSettings.StakingTokenSymbol, soulAmount, symbol, tokenAmount);
                    Runtime.TransferTokens(symbol, this.Address, owner, tokens[symbol] - tokenAmount);
                }
            }

            Runtime.Expect(totalSOULUsed <= soulTotal, "Used more than it has...");

            // return the left overs
            Runtime.TransferTokens(DomainSettings.StakingTokenSymbol, this.Address, owner, soulTotal - totalSOULUsed);
        }

        #region DEXify
        // value in "per thousands"
        public const int FeeConstant = 3;
        public const int DEXSeriesID = 1;
        internal int UserPercent = 75;
        internal int GovernancePercent = 25;
        internal StorageMap _pools;
        internal StorageMap _lp_tokens;
        internal StorageMap _lp_holders; // <string, storage_list<Address>> |-> string : $"symbol0_symbol1" |-> Address[] : key to the list 
        internal StorageMap _trading_volume; // <string, storage_list<TradingVolume>> |-> string : $"symbol0_symbol1" |-> TradingVolume[] : key to the list 

        
        private StorageList GetTradingVolume(string symbol0, string symbol1)
        {
            string key = $"{symbol0}_{symbol1}";
            if (!_trading_volume.ContainsKey<string>(key))
            {
                StorageList newStorage = new StorageList();
                _trading_volume.Set<string, StorageList>(key, newStorage);
            }

            var _tradingList = _trading_volume.Get<string, StorageList>(key);
            return _tradingList;
        }
        // Year -> Math.floor(((time % 31556926) % 2629743) / 86400)
        // Month -> Math.floor(((time % 31556926) % 2629743) / 86400)
        // Day -> Math.floor(((time % 31556926) % 2629743) / 86400)
        private TradingVolume GetTradingVolumeToday(string symbol0, string symbol1)
        {
            var tradingList = GetTradingVolume(symbol0, symbol1);
            var index = 0;
            var count = tradingList.Count();
            var today = DateTime.Today.Date; 
            TradingVolume tempTrading = new TradingVolume(symbol0, symbol0, today.ToShortDateString(), 0);
            while (index < count)
            {
                tempTrading = tradingList.Get<TradingVolume>(index);
                if (tempTrading.Day == today.ToShortDateString())
                    return tempTrading;

                index++;
            }
            
            return tempTrading;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pool"></param>
        /// <param name="Amount">Always in SOUL AMOUNT!</param>
        private void UpdateTradingVolume(Pool pool, BigInteger Amount)
        {
            var today = DateTime.Today.Date; 
            StorageList tradingList = GetTradingVolume(pool.Symbol0, pool.Symbol1);
            var tradingToday = GetTradingVolumeToday(pool.Symbol0, pool.Symbol1);
            string key = $"{pool.Symbol0}_{pool.Symbol1}";
            tradingToday.Volume += Amount;
            
            var index = 0;
            var count = tradingList.Count();
            TradingVolume tempTrading = new TradingVolume();
            bool changed = false;
            while (index < count)
            {
                tempTrading = tradingList.Get<TradingVolume>(index);
                if (tempTrading.Day == today.ToShortDateString())
                {
                    tradingList.Replace(index, tradingToday);
                    changed = true;
                    break;
                }

                index++;
            }

            if (!changed)
            {
                tradingList.Add(tradingToday);
            }
            
            
            _trading_volume.Set<string, StorageList>(key, tradingList);
        }
        
        /// <summary>
        /// This method is used to generate the key related to the USER NFT ID, to make it easier to fetch.
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <returns>Get LP Tokens Key</returns>
        private string GetLPTokensKey(Address from, string symbol0, string symbol1) {
            return $"{from.Text}_{symbol0}_{symbol1}";
        }

        /// <summary>
        /// Get the Holders AddressList for a Specific Pool
        /// </summary>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        private StorageList GetHolderList(string symbol0, string symbol1)
        {
            string key = $"{symbol0}_{symbol1}";
            if (!_lp_holders.ContainsKey<string>(key))
            {
                StorageList newStorage = new StorageList();
                _lp_holders.Set<string, StorageList>(key, newStorage);
            }

            var _holderList = _lp_holders.Get<string, StorageList>(key);
            return _holderList;
        }

        /// <summary>
        /// This method is used to check if the user already has LP on the pool
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <returns>True or False depending if the user has it or not</returns>
        private bool UserHasLP(Address from, string symbol0, string symbol1)
        {
            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");
            // Check if a pool exist (token0 - Token 1) and (token 1 - token 0)

            if (_lp_tokens.ContainsKey(GetLPTokensKey(from, symbol0, symbol1)) || _lp_tokens.ContainsKey(GetLPTokensKey(from, symbol1, symbol0)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get LP Holder by Address
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        private LPHolderInfo GetLPHolder(Address from, string symbol0, string symbol1)
        {
            Runtime.Expect(CheckHolderIsThePool(from, symbol0, symbol1), "User is not on the list.");
            var holdersList = GetHolderList(symbol0, symbol1);
            var index = 0;
            var count = holdersList.Count();
            LPHolderInfo tempHolder = new LPHolderInfo();
            while (index < count)
            {
                tempHolder = holdersList.Get<LPHolderInfo>(index);
                if (tempHolder.address == from)
                    return tempHolder;

                index++;
            }
            return tempHolder;
        }

        /// <summary>
        /// Check if the holder is on the pool for the fees.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        private bool CheckHolderIsThePool(Address from, string symbol0, string symbol1)
        {
            var holdersList = GetHolderList(symbol0, symbol1);
            var index = 0;
            var count = holdersList.Count();
            LPHolderInfo tempHolder;
            while ( index < count)
            {
                tempHolder = holdersList.Get<LPHolderInfo>(index);
                if (tempHolder.address == from)
                    return true;

                index++;
            }
            return false;
        }

        /// <summary>
        /// Add to LP Holder for that Pool
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void AddToLPHolders(Address from, string symbol0, string symbol1)
        {
            Runtime.Expect(!CheckHolderIsThePool(from, symbol0, symbol1), "User is already on the list.");
            var holdersList = GetHolderList(symbol0, symbol1);
            var lpHolderInfo = new LPHolderInfo(from, 0, 0);
            holdersList.Add<LPHolderInfo>(lpHolderInfo);
            _lp_holders.Set<string, StorageList>($"{symbol0}_{symbol1}", holdersList);
        }

        public LPHolderInfo[] GetLPHolders(string symbol0, string symbol1)
        {
            var holdersList = GetHolderList(symbol0, symbol1);
            return holdersList.All<LPHolderInfo>();
        }

        /// <summary>
        /// Update LP Holder for that specific pool
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void UpdateLPHolders(LPHolderInfo holder, string symbol0, string symbol1)
        {
            Runtime.Expect(CheckHolderIsThePool(holder.address, symbol0, symbol1), "User is not on the list.");
            var holdersList = GetHolderList(symbol0, symbol1);
            var index = 0;
            var count = holdersList.Count();
            LPHolderInfo tempHolder = new LPHolderInfo();

            while (index < count)
            {
                tempHolder = holdersList.Get<LPHolderInfo>(index);
                if (tempHolder.address == holder.address)
                {
                    holdersList.Replace<LPHolderInfo>(index, holder);
                    _lp_holders.Set<string, StorageList>($"{symbol0}_{symbol1}", holdersList);
                    break;
                }
                index++;
            }
        }

        /// <summary>
        /// Remove From the LP Holder for that specific pool
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void RemoveFromLPHolders(Address from, string symbol0, string symbol1)
        {
            Runtime.Expect(CheckHolderIsThePool(from, symbol0, symbol1), "User is not on the list.");
            var holdersList = GetHolderList(symbol0, symbol1);
            var index = 0;
            var count = holdersList.Count();
            LPHolderInfo lpHolderInfo = new LPHolderInfo();

            while (index < count)
            {
                lpHolderInfo = holdersList.Get<LPHolderInfo>(index);
                if (lpHolderInfo.address == from)
                {
                    holdersList.RemoveAt(index);
                    _lp_holders.Set<string, StorageList>($"{symbol0}_{symbol1}", holdersList);
                    break;
                }
                index++;
            }            
        }

        /// <summary>
        /// This method is to add the NFT ID to the list of NFT in that pool
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="NFTID">NFT ID</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        private void AddToLPTokens(Address from, BigInteger NFTID, string symbol0, string symbol1)
        {
            var lptokenKey = GetLPTokensKey(from, symbol0, symbol1);
            _lp_tokens.Set<string, BigInteger>(lptokenKey, NFTID);
            AddToLPHolders(from, symbol0, symbol1);
        }

        /// <summary>
        /// Update User 
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <param name="claimedAmount"></param>
        private void UpdateUserLPToken(Address from, string symbol0, string symbol1, BigInteger claimedAmount)
        {
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} already exists.");
            Pool pool = GetPool(symbol0, symbol1);
            Runtime.Expect(UserHasLP(from, pool.Symbol0, pool.Symbol1), $"User doesn't have LP");
            var lpKey = GetLPTokensKey(from, pool.Symbol0, pool.Symbol1);
            Runtime.Expect(_lp_tokens.ContainsKey(lpKey), "Doesn't contain");
            var nftID = _lp_tokens.Get<string, BigInteger>(lpKey);
            var ram = GetMyPoolRAM(from, pool.Symbol0, pool.Symbol1);
            ram.ClaimedFees += claimedAmount;
            Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, VMObject.FromStruct(ram).AsByteArray());

        }


        /// <summary>
        /// Remove From LP TOkens
        /// </summary>
        /// <param name="from"></param>
        /// <param name="NFTID"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void RemoveFromLPTokens(Address from, BigInteger NFTID, string symbol0, string symbol1)
        {
            var lptokenKey = GetLPTokensKey(from, symbol0, symbol1);
            Runtime.Expect(!_lp_tokens.ContainsKey<string>(lptokenKey), "The user is not on the list.");
            _lp_tokens.Remove<string>(lptokenKey);
            RemoveFromLPHolders(from, symbol0, symbol1);
        }

        /// <summary>
        /// This method is used to check if the pool is Real or Virtual.
        /// </summary>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <returns></returns>
        private bool PoolIsReal(string symbol0, string symbol1)
        {
            return symbol0 == DomainSettings.StakingTokenSymbol || symbol1 == DomainSettings.StakingTokenSymbol;
        }

        public LPTokenContentRAM GetMyPoolRAM(Address from, string symbol0, string symbol1)
        {
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} already exists.");
            Pool pool = GetPool(symbol0, symbol1);
            Runtime.Expect(UserHasLP(from, pool.Symbol0, pool.Symbol1), $"User doesn't have LP");
            var lpKey = GetLPTokensKey(from, pool.Symbol0, pool.Symbol1);
            Runtime.Expect(_lp_tokens.ContainsKey(lpKey), "Doesn't contain");
            var nftID = _lp_tokens.Get<string, BigInteger>(lpKey);
            var nft = Runtime.ReadToken(DomainSettings.LiquidityTokenSymbol, nftID);
            LPTokenContentRAM nftRAM = VMObject.FromBytes(nft.RAM).AsStruct<LPTokenContentRAM>();
            return nftRAM;
        }

        /// <summary>
        /// This method is used to get all the pools.
        /// </summary>
        /// <returns>Array of Pools</returns>
        public Pool[] GetPools()
        {
            return _pools.AllValues<Pool>();
        }

        /// <summary>
        /// This method is used to get a specific pool.
        /// </summary>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <returns>Pool</returns>
        public Pool GetPool(string symbol0, string symbol1)
        {
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            if (_pools.ContainsKey($"{symbol0}_{symbol1}"))
            {
                return _pools.Get<string, Pool>($"{symbol0}_{symbol1}");
            }

            if (_pools.ContainsKey($"{symbol1}_{symbol0}"))
            {
                return _pools.Get<string, Pool>($"{symbol1}_{symbol0}");
            }

            return _pools.Get<string, Pool>($"{symbol0}_{symbol1}");
        }

        /// <summary>
        /// This method is used to check if a pool exists or not.
        /// </summary>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2ndToken</param>
        /// <returns>True or false</returns>
        private bool PoolExists(string symbol0, string symbol1)
        {
            // Check if the tokens exist
            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");

            // Check if a pool exist (token0 - Token 1) and (token 1 - token 0)
            if ( _pools.ContainsKey($"{symbol0}_{symbol1}") || _pools.ContainsKey($"{symbol1}_{symbol0}") ){
                return true;
            }

            return false;
        }

        /// <summary>
        /// This method is Used to create a Pool.
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="amount0">Amount for Symbol0</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <param name="amount1">Amount for Symbol1</param>
        public void CreatePool(Address from, string symbol0, BigInteger amount0, string symbol1,  BigInteger amount1)
        {
            Runtime.Expect(_DEXversion >= 1, "call migrateV3 first");

            // Check the if the input is valid
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount0 > 0 || amount1 > 0, "invalid amount 0");
            Runtime.Expect(symbol0 == "SOUL" || symbol1 == "SOUL", "Virtual pools are not supported yet!");

            // Check if pool exists
            Runtime.Expect(!PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} already exists.");

            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");
            
            var symbol0Price = Runtime.GetTokenQuote(symbol0, DomainSettings.FiatTokenSymbol, UnitConversion.ToBigInteger(1, token0Info.Decimals));
            var symbol1Price = Runtime.GetTokenQuote(symbol1, DomainSettings.FiatTokenSymbol, UnitConversion.ToBigInteger(1, token1Info.Decimals));
            BigInteger tradeRatio = 0;
            BigInteger tradeRatioAmount = 0;

            //Console.WriteLine($"{symbol1Price} {symbol1} | {amount1}");

            // Check ratio
            if (symbol0Price / symbol1Price > 0)
                tradeRatio = symbol0Price  / symbol1Price;
            else
                tradeRatio = symbol1Price / symbol0Price;

            if ( amount0 == 0 )
            {
                amount0 = UnitConversion.ConvertDecimals((amount1 / tradeRatio), DomainSettings.FiatTokenDecimals, token0Info.Decimals);
            }
            else
            {
                if (amount1 == 0)
                {
                    amount1 = UnitConversion.ConvertDecimals((amount0 / tradeRatio), DomainSettings.FiatTokenDecimals, token1Info.Decimals); 
                }
            }


            if (amount0 / UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, token0Info.Decimals) > 0)
                tradeRatioAmount = amount0 / UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, token0Info.Decimals);
            else
                tradeRatioAmount = amount1 / UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, token1Info.Decimals);

            //Console.WriteLine($"TradeRatio:{tradeRatio} | Amount0:{amount0} | Amount1:{amount1} | Am0/Am1:{amount0/amount1} | Am1/Am0:{amount1/ amount0}");
            var tempAm0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            var tempAm1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
            if ( tradeRatio == 0 )
            {
                tradeRatio = tradeRatioAmount;
            }
            else
            {
                // Ratio Base on the real price of token
                // TODO: Ask if this is gonna be implemented this way.
                //Runtime.Expect(tradeRatioAmount == tradeRatio, $"TradeRatio < 0 | {tradeRatio} != {tradeRatioAmount}");
                tradeRatio = tradeRatioAmount;
            }
            
            Runtime.Expect( ValidateRatio(tempAm0, tempAm1, tradeRatio), $"ratio is not true. {tradeRatio}, new {tempAm0} {tempAm1} {tempAm0 / tempAm1} {amount0/ amount1}");

            var symbol0Balance = Runtime.GetBalance(symbol0, from);
            Runtime.Expect(symbol0Balance >= amount0, $"not enough {symbol0} balance, you need {amount0}");
            var symbol1Balance = Runtime.GetBalance(symbol1, from);
            Runtime.Expect(symbol1Balance >= amount1, $"not enough {symbol1} balance, you need {amount1}");


            BigInteger feeRatio = (amount0 * FeeConstant) / 1000;
            feeRatio = FeeConstant;

            // Get the token address
            // Token0 Address
            Address token0Address = TokenUtils.GetContractAddress(symbol0);

            // Token1 Address
            Address token1Address = TokenUtils.GetContractAddress(symbol1);            

            BigInteger TLP = (BigInteger)Sqrt(amount0 * amount1);

            // Create the pool
            Pool pool = new Pool(symbol0, symbol1, token0Address.Text, token1Address.Text, amount0, amount1, feeRatio, TLP);

            _pools.Set<string, Pool>($"{symbol0}_{symbol1}", pool);

            // Give LP Token to the address
            LPTokenContentROM nftROM = new LPTokenContentROM(pool.Symbol0, pool.Symbol1, Runtime.GenerateUID());
            LPTokenContentRAM nftRAM = new LPTokenContentRAM(amount0, amount1, TLP);

            var nftID = Runtime.MintToken(DomainSettings.LiquidityTokenSymbol, this.Address, from, VMObject.FromStruct(nftROM).AsByteArray(), VMObject.FromStruct(nftRAM).AsByteArray(), DEXSeriesID);
            Runtime.TransferTokens(pool.Symbol0, from, this.Address, amount0);
            Runtime.TransferTokens(pool.Symbol1, from, this.Address, amount1);
            AddToLPTokens(from, nftID, pool.Symbol0, pool.Symbol1);
        }

        /// <summary>
        /// This method is used to Provide Liquidity to the Pool
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="amount0">Amount for Symbol0</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <param name="amount1">Amount for Symbol1</param>
        public void AddLiquidity(Address from, string symbol0, BigInteger amount0, string symbol1, BigInteger amount1)
        {
            // Check input
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount0 >= 0, "invalid amount 0");
            Runtime.Expect(amount1 >= 0, "invalid amount 1");
            Runtime.Expect(amount0 > 0 || amount1 > 0, "invalid amount, both amounts can't be 0");
            Runtime.Expect(symbol0 != symbol1, "Symbols are the same...");

            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");

            // if its virtual we need an aditional step
            Runtime.Expect(PoolIsReal(symbol0, symbol1), "Only Real pools are supported.");

            // Check if pool exists
            if (!PoolExists(symbol0, symbol1))
            {
                CreatePool(from, symbol0, amount0, symbol1, amount1);
                return;
            }

            // Get pool
            Pool pool = GetPool(symbol0, symbol1);
            BigInteger poolRatio = 0;
            BigInteger tradeRatioAmount = 0;

            // Fix inputs
            if (symbol0 != pool.Symbol0)
            {
                symbol0 = pool.Symbol0;
                symbol1 = pool.Symbol1;
                BigInteger temp = amount0;
                amount0 = amount1;
                amount1 = temp;
            }


            if (UnitConversion.ConvertDecimals(pool.Amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(pool.Amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals) > 0)
                poolRatio = UnitConversion.ConvertDecimals(pool.Amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(pool.Amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
            else
                poolRatio = UnitConversion.ConvertDecimals(pool.Amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(pool.Amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);

            // Calculate Amounts if they are 0
            if (amount0 == 0)
            {
                amount0 = UnitConversion.ConvertDecimals((amount1 / 100 / poolRatio ), DomainSettings.FiatTokenDecimals, token0Info.Decimals);
            }
            else
            {
                if (amount1 == 0)
                {
                    amount1 = UnitConversion.ConvertDecimals((amount0 / 100 / poolRatio ), DomainSettings.FiatTokenDecimals, token1Info.Decimals);
                }
            }


            if (amount1 * 100 / UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, token0Info.Decimals) > 0)
                tradeRatioAmount = amount0 * 100 / UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, token0Info.Decimals);
            else
                tradeRatioAmount = amount1 * 100 / UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, token1Info.Decimals);

            
            if (poolRatio == 0)
            {
                poolRatio = tradeRatioAmount;
            }
            else
            {
                if (tradeRatioAmount != poolRatio)
                {
                    amount1 = UnitConversion.ConvertDecimals((amount0  * 100 / poolRatio ), DomainSettings.FiatTokenDecimals, token1Info.Decimals);

                    if (amount0 * 100 / UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, token0Info.Decimals) > 0)
                        tradeRatioAmount = amount0 * 100 / UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, token0Info.Decimals);
                    else
                        tradeRatioAmount = amount1 * 100 / UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, token1Info.Decimals);

                }
                //Runtime.Expect(tradeRatioAmount == poolRatio, $"TradeRatio < 0 | {poolRatio} != {tradeRatioAmount}");
            }

            //Console.WriteLine($"Ratio:{poolRatio} | Trade:{tradeRatioAmount}");
            var tempAm0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            var tempAm1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
            Runtime.Expect(ValidateRatio(tempAm1, tempAm0*100, poolRatio), $"ratio is not true. {poolRatio}, new {tempAm0} {tempAm1} {tempAm1 * 100 / tempAm0} {amount1 * 100 /amount0 }");

            //Console.WriteLine($"ADD: ratio:{poolRatio} | amount0:{amount0} | amount1:{amount1}");
            // Check if is a virtual pool -> if one of the tokens is SOUL is real pool, if not is virtual.
            bool isRealPool = PoolIsReal(pool.Symbol0, pool.Symbol1);
            BigInteger liquidity = 0;

            // Check if user has the LP Token and Update the values
            if (UserHasLP(from, pool.Symbol0, pool.Symbol1))
            {
                // Update the NFT VALUES
                var lpKey = GetLPTokensKey(from, pool.Symbol0, pool.Symbol1);
                var nftID = _lp_tokens.Get<string, BigInteger>(lpKey);
                var nft = Runtime.ReadToken(DomainSettings.LiquidityTokenSymbol, nftID);
                LPTokenContentRAM nftRAM = VMObject.FromBytes(nft.RAM).AsStruct<LPTokenContentRAM>();
                BigInteger lp_amount = 0;

                // CALCULATE BASED ON THIS lp_amount = (SOUL_USER  * LP_TOTAL )/  SOUL_TOTAL
                if (pool.Symbol0 == symbol1)
                {
                    // TODO: calculate the amounts according to the ratio...

                    lp_amount = (amount0 * pool.TotalLiquidity) / pool.Amount0;

                    nftRAM.Amount0 += amount0;
                    nftRAM.Amount1 += amount1;
                    nftRAM.Liquidity += lp_amount;
                }
                else
                {
                    lp_amount = (amount1 * pool.TotalLiquidity) / pool.Amount0;

                    nftRAM.Amount0 += amount1;
                    nftRAM.Amount1 += amount0;
                    nftRAM.Liquidity += lp_amount;
                }

                liquidity = lp_amount;

                Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, VMObject.FromStruct(nftRAM).AsByteArray());
            }
            else
            {
                // MINT NFT and give to the user
                // CALCULATE BASED ON THIS lp_amount = (SOUL_USER  * LP_TOTAL )/  SOUL_TOTAL
                // TODO: calculate the amounts according to the ratio...
                BigInteger nftID = 0;
                LPTokenContentROM nftROM = new LPTokenContentROM(pool.Symbol0, pool.Symbol1, Runtime.GenerateUID());
                LPTokenContentRAM nftRAM = new LPTokenContentRAM();
                BigInteger lp_amount = 0;

                // Verify the order of the assets
                if ( pool.Symbol0 == symbol0)
                {
                    lp_amount = (amount0 * pool.TotalLiquidity) / pool.Amount0;
                    nftRAM = new LPTokenContentRAM(amount0, amount1, lp_amount);
                }
                else
                {
                    lp_amount = (amount1 * pool.TotalLiquidity) / pool.Amount0;
                    nftRAM = new LPTokenContentRAM(amount1, amount0, lp_amount);
                }

                liquidity = lp_amount;

                nftID = Runtime.MintToken(DomainSettings.LiquidityTokenSymbol, this.Address, from, VMObject.FromStruct(nftROM).AsByteArray(), VMObject.FromStruct(nftRAM).AsByteArray(), DEXSeriesID);
                AddToLPTokens(from, nftID, pool.Symbol0, pool.Symbol1);
            }

            //Console.WriteLine($"ADD: lp:{liquidity}");


            // Update the pool values
            Runtime.TransferTokens(pool.Symbol0, from, this.Address, amount0);
            Runtime.TransferTokens(pool.Symbol1, from, this.Address, amount1);
            pool.Amount0 += amount0;
            pool.Amount1 += amount1;

            pool.TotalLiquidity += liquidity;

            _pools.Set<string, Pool>($"{pool.Symbol0}_{pool.Symbol1}", pool);
        }

        /// <summary>
        /// This method is used to Remove Liquidity from the pool.
        /// </summary>
        /// <param name="from">User Address</param>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="amount0">Amount for Symbol0</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <param name="amount1">Amount for Symbol1</param>
        public void RemoveLiquidity(Address from, string symbol0, BigInteger amount0, string symbol1, BigInteger amount1)
        {
            // Check input
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount0 >= 0 , "invalid amount 0");
            Runtime.Expect(amount1 >= 0 , "invalid amount 1");
            Runtime.Expect(amount0 > 0 || amount1 > 0, "invalid amount, both amounts can't be 0");

            // Check if user has LP Token
            Runtime.Expect(UserHasLP(from, symbol0, symbol1), "User doesn't have LP");

            // Check if pool exists
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");

            // Get Pool
            Pool pool = GetPool(symbol0, symbol1);
            bool isRealPool = PoolIsReal(pool.Symbol0, pool.Symbol1);
            BigInteger liquidity = 0;

            // Fix inputs
            if (symbol0 != pool.Symbol0)
            {
                symbol0 = pool.Symbol0;
                symbol1 = pool.Symbol1;
                BigInteger temp = amount0;
                amount0 = amount1;
                amount1 = temp;
            }

            // Calculate Amounts
            BigInteger poolRatio = 0; 
            BigInteger tradeRatioAmount = 0;

            if (UnitConversion.ConvertDecimals(pool.Amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(pool.Amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals) > 0)
                poolRatio = UnitConversion.ConvertDecimals(pool.Amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(pool.Amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
            else
                poolRatio = UnitConversion.ConvertDecimals(pool.Amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(pool.Amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);

            // Calculate Amounts if they are 0
            if (amount0 == 0)
            {
                amount0 = UnitConversion.ConvertDecimals((amount1 / 100 / poolRatio), DomainSettings.FiatTokenDecimals, token0Info.Decimals);
            }
            else
            {
                if (amount1 == 0)
                {
                    amount1 = UnitConversion.ConvertDecimals((amount0 / 100 / poolRatio), DomainSettings.FiatTokenDecimals, token1Info.Decimals);
                }
            }


            if (amount1 * 100 / UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, token0Info.Decimals) > 0)
                tradeRatioAmount = amount0 * 100 / UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, token0Info.Decimals);
            else
                tradeRatioAmount = amount1 * 100 / UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, token1Info.Decimals);


            if (poolRatio == 0)
            {
                poolRatio = tradeRatioAmount;
            }
            else
            {
                if (tradeRatioAmount != poolRatio)
                {
                    amount1 = UnitConversion.ConvertDecimals((amount0 * 100 / poolRatio), DomainSettings.FiatTokenDecimals, token1Info.Decimals);

                    if (amount0 * 100 / UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, token0Info.Decimals) > 0)
                        tradeRatioAmount = amount0 * 100 / UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, token0Info.Decimals);
                    else
                        tradeRatioAmount = amount1 * 100 / UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, token1Info.Decimals);

                }
                //Runtime.Expect(tradeRatioAmount == poolRatio, $"TradeRatio < 0 | {poolRatio} != {tradeRatioAmount}");
            }

            //Console.WriteLine($"pool:{poolRatio} | trade:{tradeRatioAmount} | {amount0} {symbol0} for {amount1} {symbol1}");

            var tempAm0 = UnitConversion.ConvertDecimals(amount0, token0Info.Decimals, DomainSettings.FiatTokenDecimals);
            var tempAm1 = UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, DomainSettings.FiatTokenDecimals);
            Runtime.Expect(ValidateRatio(tempAm1, tempAm0*100, poolRatio), $"ratio is not true. {poolRatio}, new {tempAm0} {tempAm1} {tempAm0 / tempAm1} {amount0 / UnitConversion.ConvertDecimals(amount1, token1Info.Decimals, token0Info.Decimals)}");

            // Update the user NFT
            var lpKey = GetLPTokensKey(from, pool.Symbol0, pool.Symbol1);
            var nftID = _lp_tokens.Get<string, BigInteger>(lpKey);
            var nft = Runtime.ReadToken(DomainSettings.LiquidityTokenSymbol, nftID);
            LPTokenContentRAM nftRAM = VMObject.FromBytes(nft.RAM).AsStruct<LPTokenContentRAM>();
            BigInteger newAmount0 = nftRAM.Amount0 - amount0;
            BigInteger newAmount1 = nftRAM.Amount1 - amount1;
            BigInteger oldAmount0 = nftRAM.Amount0;
            BigInteger oldAmount1 = nftRAM.Amount1;
            BigInteger oldLP = nftRAM.Liquidity;
            BigInteger newLiquidity = newAmount0 * (pool.TotalLiquidity - nftRAM.Liquidity) / (pool.Amount0-nftRAM.Amount0);

            liquidity = (amount0 * (pool.TotalLiquidity)) / (pool.Amount0);
            
            Runtime.Expect(nftRAM.Liquidity - liquidity >= 0, "Trying to remove more than you have...");

            //Console.WriteLine($"BeforeLP:{nftRAM.Liquidity} - LiquidityToRemove:{liquidity} | FinalLP:{newLiquidity}");


            // If the new amount will be = 0 then burn the NFT
            if (nftRAM.Liquidity - liquidity == 0)
            {
                // Burn NFT
                Runtime.BurnToken(DomainSettings.LiquidityTokenSymbol, from, nftID);
                RemoveFromLPTokens(from, nftID, pool.Symbol0, pool.Symbol1);
            }
            else
            {
                Runtime.Expect(nftRAM.Amount0 - amount0 > 0, $"Lower Amount for symbol {symbol0}. | You have {nftRAM.Amount0} {symbol0}, trying to remove {amount0} {symbol0}");
                nftRAM.Amount0 = newAmount0;

                Runtime.Expect(nftRAM.Amount1 - amount1 > 0, $"Lower Amount for symbol {symbol1}. | You have {nftRAM.Amount1} {symbol1}, trying to remove {amount1} {symbol1}");
                nftRAM.Amount1 = newAmount1;

                Runtime.TransferTokens(symbol0, this.Address, from, amount0);
                Runtime.TransferTokens(symbol1, this.Address, from, amount1);
                
                nftRAM.Liquidity = newLiquidity;

                Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, VMObject.FromStruct(nftRAM).AsByteArray());
            }

            // Update the pool values
            pool.Amount0 = (pool.Amount0 - oldAmount0) + amount0;
            pool.Amount1 = (pool.Amount1 - oldAmount1) + amount1;

            pool.TotalLiquidity = pool.TotalLiquidity - oldLP + newLiquidity;

            _pools.Set<string, Pool>($"{pool.Symbol0}_{pool.Symbol1}", pool);
        }

        private bool ValidateTrade(BigInteger amount0, BigInteger amount1, Pool pool, bool isBuying = false)
        {
            if (isBuying)
            {
                if (pool.Amount0 + amount0 > 0 && pool.Amount1 - amount1 > 0)
                    return true;
            }
            else
            {
                if (pool.Amount0 - amount0 > 0 && pool.Amount1 + amount1 > 0)
                    return true;
            }

            return false;
        }

        private bool ValidateRatio(BigInteger amount0, BigInteger amount1, BigInteger ratio)
        {
            if ( amount1 / amount0 > 0)
                return amount1 / amount0 == ratio;
            return amount0 / amount1 == ratio;
        }

        private BigInteger CalculateFeeForUser(BigInteger totalFee, BigInteger liquidity, BigInteger totalLiquidity)
        {
            BigInteger feeAmount = liquidity * 1000000000000 / totalLiquidity;
            return totalFee*feeAmount/ 1000000000000;
        }

        /// <summary>
        /// Distribute Fees
        /// </summary>
        /// <param name="totalFeeAmount"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        private void DistributeFee(BigInteger totalFeeAmount, string symbol0, string symbol1)
        {
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");
            var pool = _pools.Get<string, Pool>($"{symbol0}_{symbol1}");
            var holdersList = GetHolderList(symbol0, symbol1);
            var index = 0;
            var count = holdersList.Count();
            var feeAmount = totalFeeAmount; 
            BigInteger amount = 0;
            LPHolderInfo holder = new LPHolderInfo();
            LPTokenContentRAM nftRAM = new LPTokenContentRAM();

            while (index < count)
            {
                holder = holdersList.Get<LPHolderInfo>(index);
                nftRAM = GetMyPoolRAM(holder.address, symbol0, symbol1);
                amount = CalculateFeeForUser(totalFeeAmount, nftRAM.Liquidity, pool.TotalLiquidity);
                Runtime.Expect(amount > 0, $"Amount failed for user: {holder.address}, unclaimed:{holder.unclaimed}, amount:{amount}, feeAmount:{feeAmount}, feeTotal:{totalFeeAmount}");

                feeAmount -= amount;
                holder.unclaimed += amount;
                holdersList.Replace<LPHolderInfo>(index, holder);

                index++;
            }

            // Update List
            _lp_holders.Set<string, StorageList>($"{symbol0}_{symbol1}", holdersList);
        }

        /// <summary>
        /// Method used to claim fees
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        public void ClaimFees(Address from, string symbol0, string symbol1)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            // Check if user has LP Token
            Runtime.Expect(UserHasLP(from, symbol0, symbol1), "User doesn't have LP");

            // Check if pool exists
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            var holder = GetLPHolder(from, symbol0, symbol1);
            var unclaimedAmount = holder.unclaimed;

            Runtime.TransferTokens(symbol0, this.Address, from, unclaimedAmount);

            holder.claimed += unclaimedAmount;
            holder.unclaimed = 0;

            // Update LP Holder
            UpdateLPHolders(holder, symbol0, symbol1);

            // Update NFT
            UpdateUserLPToken(from, symbol0, symbol1, unclaimedAmount);
        }

        /// <summary>
        /// Get unclaimed fees;
        /// </summary>
        /// <param name="from"></param>
        /// <param name="symbol0"></param>
        /// <param name="symbol1"></param>
        /// <returns></returns>
        public BigInteger GetUnclaimedFees(Address from, string symbol0, string symbol1)
        {
            // Check if user has LP Token
            Runtime.Expect(UserHasLP(from, symbol0, symbol1), "User doesn't have LP");

            // Check if pool exists
            Runtime.Expect(PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            var holder = GetLPHolder(from, symbol0, symbol1);
            return holder.unclaimed;
        }

        private static BigInteger Sqrt(BigInteger n)
        {
            BigInteger root = n / 2;

            while (n < root * root)
            {
                root += n / root;
                root /= 2;
            }

            return root;
        }
        // Helpers
        //Runtime.GetBalance(symbol0, this.Address);
        //Runtime.GetBalance(symbol1, this.Address);
        #endregion
    }
}
