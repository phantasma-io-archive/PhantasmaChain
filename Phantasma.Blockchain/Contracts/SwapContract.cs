using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Utils;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using Phantasma.Storage.Context;
using Phantasma.Blockchain.Tokens;
using Phantasma.VM;

namespace Phantasma.Blockchain.Contracts
{
    public struct LPTokenContentROM: ISerializable
    {
        public string Symbol0;
        public string Symbol1;

        public LPTokenContentROM(string Symbol0, string Symbol1)
        {
            this.Symbol0 = Symbol0;
            this.Symbol1 = Symbol1;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Symbol0);
            writer.WriteVarString(Symbol1);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Symbol0 = reader.ReadVarString();
            Symbol1 = reader.ReadVarString();
        }
    }

    public struct LPTokenContentRAM : ISerializable
    {
        public BigInteger Amount0;
        public BigInteger Amount1;
        public BigInteger Liquidity;

        public LPTokenContentRAM(BigInteger Amount0, BigInteger Amount1, BigInteger Liquidity)
        {
            this.Amount0 = Amount0;
            this.Amount1 = Amount1;
            this.Liquidity = Liquidity;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteBigInteger(Amount0);
            writer.WriteBigInteger(Amount1);
            writer.WriteBigInteger(Liquidity);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Amount0 = reader.ReadBigInteger();
            Amount1 = reader.ReadBigInteger();
            Liquidity = reader.ReadBigInteger();
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
        }
    }

    public sealed class SwapContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Swap;

        internal BigInteger _swapVersion;

        public SwapContract() : base()
        {
        }

        public BigInteger GetSwapVersion()
        {
            if (_swapVersion <= 0) // support for legacy versions
            {
                return 2;
            }
            else
            {
                return _swapVersion;
            }
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
            return info.IsFungible() && info.Flags.HasFlag(TokenFlags.Swappable);
        }

        public const string SwapMakerFeePercentTag = "swap.fee.maker";
        public const string SwapTakerFeePercentTag = "swap.fee.taker";

        // returns how many tokens would be obtained by trading from one type of another
        public BigInteger GetRate(string fromSymbol, string toSymbol, BigInteger amount)
        {
            if (Runtime.ProtocolVersion >= 7)
            {
                return GetRateV3(fromSymbol, toSymbol, amount);
            }
            else if (Runtime.ProtocolVersion >= 3)
            {
                return GetRateV2(fromSymbol, toSymbol, amount);
            }
            else
            {
                return GetRateV1(fromSymbol, toSymbol, amount);
            }
        }


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

        private BigInteger GetRateV3(string fromSymbol, string toSymbol, BigInteger amount)
        {
            Runtime.Expect(fromSymbol != toSymbol, "invalid pair");

            Runtime.Expect(IsSupportedToken(fromSymbol), "unsupported from symbol");
            Runtime.Expect(IsSupportedToken(toSymbol), "unsupported to symbol");

            var fromInfo = Runtime.GetToken(fromSymbol);
            Runtime.Expect(fromInfo.IsFungible(), "must be fungible");

            var toInfo = Runtime.GetToken(toSymbol);
            Runtime.Expect(toInfo.IsFungible(), "must be fungible");

            BigInteger rate = 0;

            Pool pool = GetPool(fromSymbol, toSymbol);
            BigInteger tokenAmount = 0;
            if (pool.Symbol0 == fromSymbol)
            {
                tokenAmount = (amount * pool.TotalLiquidity) / pool.Amount0; // PoolAmount0
            }
            else
            {
                tokenAmount = (amount * pool.TotalLiquidity) / pool.Amount1; // PoolAmount1
            }

            rate = tokenAmount;
            Runtime.Expect(rate >= 0, "invalid swap rate");

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

        public void MigrateToV3() 
        {
            var owner = Runtime.GenesisAddress;
            Runtime.Expect(Runtime.IsWitness(owner), "invalid witness");

            Runtime.Expect(GetSwapVersion() < 3, "Migration failed, wrong version");

            var existsLP = Runtime.TokenExists(DomainSettings.LiquidityTokenSymbol);
            Runtime.Expect(!existsLP, "LP token already exists!");

            var tokenScript = new byte[] { (byte)VM.Opcode.RET }; // TODO maybe fetch a pre-compiled Tomb script here, like for Crown?
            var abi = ContractInterface.Empty;
            Runtime.CreateToken(owner, DomainSettings.LiquidityTokenSymbol, DomainSettings.LiquidityTokenSymbol, 0, 0, TokenFlags.Transferable | TokenFlags.Burnable, tokenScript, abi);

            byte[] nftScript;
            ContractInterface nftABI;

            var url = "https://www.22series.com/part_info?id=*";
            Tokens.TokenUtils.GenerateNFTDummyScript(DomainSettings.LiquidityTokenSymbol, $"{DomainSettings.LiquidityTokenSymbol} #*", $"{DomainSettings.LiquidityTokenSymbol} #*", url, url, out nftScript, out nftABI);
            Runtime.CreateTokenSeries(DomainSettings.LiquidityTokenSymbol, this.Address, 1, 0, TokenSeriesMode.Duplicated, nftScript, nftABI);

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

            // create a pool for every found fungible token
            foreach (var symbol in sortedTokens)
            {
                var amount = tokens[symbol];
                //var soulAmount = ????; // how should we calculate how much SOUL to put in each pool, based in soulTotal variable? soulAvg = soulTotal / sortedTokens.Length??
                //CreateLiquidityPool(symbol, DomainSettings.StakingTokenSymbol, amount, soulAmount); TODO finish this
            }

            _swapVersion = 3;
        }

        public void SwapFee(Address from, string fromSymbol, BigInteger feeAmount)
        {
            var protocol = Runtime.ProtocolVersion;

            if (protocol >= 7)
            {
                SwapFeeV3(from, fromSymbol, feeAmount);
            }
            else
            if (protocol >= 3)
            {
                SwapFeeV2(from, fromSymbol, feeAmount);
            }
            else
            {
                SwapFeeV1(from, fromSymbol, feeAmount);
            }
        }

        private void SwapFeeV3(Address from, string fromSymbol, BigInteger feeAmount)
        {
            Runtime.Expect(GetSwapVersion() == 3, "call migrateV3 first");

            throw new ChainException("TODO implemented swapV3");
        }

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
            Runtime.Expect(toPotBalance >= total, $"insufficient balance in pot, have {(double)toPotBalance/toSymbolDecimals} {toSymbol} in pot, need {(double)total/toSymbolDecimals} {toSymbol}, have {(double)fromBalance/fromSymbolDecimals} {fromSymbol} to convert from");

            var half = toPotBalance / 2;
            Runtime.Expect(total < half, $"taking too much {toSymbol} from pot at once");

            Runtime.TransferTokens(fromSymbol, from, this.Address, amount);
            Runtime.TransferTokens(toSymbol, this.Address, from, total);
        }

        #region DEXify
        // value in "per thousands"
        public const int FeeConstant = 3;
        public const int DEXSeriesID = 1;
        internal StorageMap _pools;
        internal StorageMap _lp_tokens;

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

            if (_lp_tokens.ContainsKey(GetLPTokensKey(from, symbol0, symbol1)) || _lp_tokens.ContainsKey(GetLPTokensKey(from, symbol0, symbol1)))
            {
                return true;
            }

            return false;
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
        }

        /// <summary>
        /// This method is used to check if the pool is Real or Virtual.
        /// </summary>
        /// <param name="symbol0">Symbol of 1st Token</param>
        /// <param name="symbol1">Symbol of 2nd Token</param>
        /// <returns></returns>
        private bool PoolIsReal(string symbol0, string symbol1)
        {
            return symbol0 == "SOUL" || symbol1 == "SOUL";
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
            Runtime.Expect(!PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            if (_pools.ContainsKey($"{symbol0}_{symbol1}"))
            {
                return _pools.Get<string, Pool>($"{symbol0}_{symbol1}");
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
            if ( _pools.ContainsKey($"{symbol0}_{symbol1}") || _pools.ContainsKey($"{symbol0}_{symbol1}") ){
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
            Runtime.Expect(GetSwapVersion() == 3, "call migrateV3 first");

            // Check the if the input is valid
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(amount0 > 0, "invalid amount");
            Runtime.Expect(amount1 > 0, "invalid amount");
            Runtime.Expect(symbol0 == "SOUL" || symbol1 == "SOUL", "Virtual pools are not supported yet!");

            // Check if pool exists
            Runtime.Expect(!PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} already exists.");

            var token0Info = Runtime.GetToken(symbol0);
            Runtime.Expect(IsSupportedToken(symbol0), "source token is unsupported");

            var fromBalance = Runtime.GetBalance(symbol0, from);
            Runtime.Expect(fromBalance > 0, $"not enough {symbol0} balance");

            var token1Info = Runtime.GetToken(symbol1);
            Runtime.Expect(IsSupportedToken(symbol1), "destination token is unsupported");

            //var total = GetRate(symbol0, symbol1, amount0);
            //Runtime.Expect(total > 0, "amount to swap needs to be larger than zero");

            BigInteger feeRatio = (amount0 * FeeConstant) / 1000;

            // Get the token address
            // Token0 Address
            Address token0Address = TokenUtils.GetContractAddress(symbol0);

            // Token1 Address
            Address token1Address = TokenUtils.GetContractAddress(symbol1);

            BigInteger TLP = (BigInteger)Math.Sqrt((double)(amount0 * amount1));

            // Create the pool
            Pool pool = new Pool(symbol0, symbol1, token0Address.Text, token1Address.Text, amount0, amount1, feeRatio, TLP);

            // Give LP Token to the address
            LPTokenContentROM nftROM = new LPTokenContentROM(symbol0, symbol1);
            LPTokenContentRAM nftRAM = new LPTokenContentRAM(amount0, amount1, TLP);

            var nftID = Runtime.MintToken(DomainSettings.LiquidityTokenSymbol, this.Address, from, VMObject.FromStruct(nftROM).AsByteArray(), VMObject.FromStruct(nftRAM).AsByteArray(), DEXSeriesID);
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
            Runtime.Expect(amount0 > 0, "invalid amount");
            Runtime.Expect(amount1 > 0, "invalid amount");
            Runtime.Expect(symbol0 != symbol1, "Symbols are the same...");

            // if its virtual we need an aditional step

            // Check if pool exists
            if (!PoolExists(symbol0, symbol1))
            {
                CreatePool(from, symbol0, amount0, symbol1, amount1);
                return;
            }

            // Get pool
            Pool pool = GetPool(symbol0, symbol1);
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

                // CALCULATE BASED ON THIS lp_amount = (SOUL_USER  * LP_TOTAL )/  SOUL_TOTAL
                if (pool.Symbol0 == symbol1)
                {
                    var lp_amount = (amount0 * pool.TotalLiquidity) / pool.Amount0;

                    nftRAM.Amount0 += amount0;
                    nftRAM.Amount1 += amount1;
                    nftRAM.Liquidity += lp_amount;
                }
                else
                {
                    var lp_amount = (amount1 * pool.TotalLiquidity) / pool.Amount0;

                    nftRAM.Amount0 += amount1;
                    nftRAM.Amount1 += amount0;
                    nftRAM.Liquidity += lp_amount;
                }

                Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, nftRAM.Serialize());
            }
            else
            {
                // MINT NFT and give to the user
                // CALCULATE BASED ON THIS lp_amount = (SOUL_USER  * LP_TOTAL )/  SOUL_TOTAL
                BigInteger nftID = 0;
                LPTokenContentROM nftROM = new LPTokenContentROM(pool.Symbol0, pool.Symbol1);
                LPTokenContentRAM nftRAM = new LPTokenContentRAM();

                // Verify the order of the assets
                if ( pool.Symbol0 == symbol1)
                {
                    var lp_amount = (amount0 * pool.TotalLiquidity) / pool.Amount0;
                    nftRAM = new LPTokenContentRAM(amount0, amount1, lp_amount);
                }
                else
                {
                    var lp_amount = (amount1 * pool.TotalLiquidity) / pool.Amount0;
                    nftRAM = new LPTokenContentRAM(amount1, amount0, lp_amount);
                }

                nftID = Runtime.MintToken(DomainSettings.LiquidityTokenSymbol, this.Address, from, nftROM.Serialize(), nftRAM.Serialize(), DEXSeriesID);
                AddToLPTokens(from, nftID, pool.Symbol0, pool.Symbol1);
            }

            // Update the pool values
            if (symbol0 == pool.Symbol0 && symbol1 == pool.Symbol1)
            {
                pool.Amount0 += amount0;
                pool.Amount1 += amount1;
            }
            else
            {
                pool.Amount1 += amount0;
                pool.Amount0 += amount1;
            }

            pool.TotalLiquidity += liquidity;
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
            Runtime.Expect(amount0 > 0, "invalid amount");
            Runtime.Expect(amount1 > 0, "invalid amount");

            // Check if user has LP Token
            Runtime.Expect(!UserHasLP(from, symbol0, symbol1), "User doesn't have LP");

            // Check if pool exists
            Runtime.Expect(!PoolExists(symbol0, symbol1), $"Pool {symbol0}/{symbol1} doesn't exist.");

            // Get Pool
            Pool pool = GetPool(symbol0, symbol1);
            bool isRealPool = PoolIsReal(pool.Symbol0, pool.Symbol1);
            BigInteger liquidity = 0;

            // Update the user NFT
            var lpKey = GetLPTokensKey(from, pool.Symbol0, pool.Symbol1);
            var nftID = _lp_tokens.Get<string, BigInteger>(lpKey);
            var nft = Runtime.ReadToken(DomainSettings.LiquidityTokenSymbol, nftID);
            LPTokenContentRAM nftRAM = VMObject.FromBytes(nft.RAM).AsStruct<LPTokenContentRAM>();

            if ( pool.Symbol0 == symbol0 )
            {
                liquidity = (amount0 * pool.TotalLiquidity) / pool.Amount0;
            }
            else
            {
                liquidity = (amount1 * pool.TotalLiquidity) / pool.Amount0;
            }

            Runtime.Expect(nftRAM.Liquidity - liquidity >= 0, "Trying to remove more than you have...");

            // If the new amount will be = 0 then burn the NFT
            if (nftRAM.Liquidity - liquidity == 0)
            {
                // Burn NFT
                Runtime.BurnToken(DomainSettings.LiquidityTokenSymbol, from, nftID);
            }
            else
            {
                // Update NFT
                if (pool.Symbol0 == symbol0)
                {
                    nftRAM.Amount0 -= amount0;
                    nftRAM.Amount1 -= amount1;
                    Runtime.TransferTokens(symbol0, this.Address, from, amount0);
                }
                else
                {
                    nftRAM.Amount0 -= amount1;
                    nftRAM.Amount1 -= amount0;
                    Runtime.TransferTokens(symbol0, this.Address, from, amount1);

                }

                nftRAM.Liquidity -= liquidity;

                Runtime.WriteToken(from, DomainSettings.LiquidityTokenSymbol, nftID, nftRAM.Serialize());
            }

            // Update the pool values
            if (symbol0 == pool.Symbol0 && symbol1 == pool.Symbol1)
            {
                pool.Amount0 -= amount0;
                pool.Amount1 -= amount1;
            }
            else
            {
                pool.Amount1 -= amount0;
                pool.Amount0 -= amount1;
            }

            pool.TotalLiquidity -= liquidity;

        }

        

        private bool ValidateTrade(BigInteger amount0, BigInteger amount1, Pool pool, bool isBuying = false)
        {
            if (isBuying)
            {
                if (pool.Amount0 - amount0 > 0 && pool.Amount1 + amount1 > 0)
                    return true;
            }
            else
            {
                if (pool.Amount0 + amount0 > 0 && pool.Amount1 - amount1 > 0)
                    return true;
            }

            return false;
        }

        // Helpers
        //Runtime.GetBalance(symbol0, this.Address);
        //Runtime.GetBalance(symbol1, this.Address);
        #endregion
    }
}
