using System;
using System.Numerics;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.VM;

namespace Phantasma.Domain
{
    public static class DomainExtensions
    {
        public static bool IsFungible(this IToken token)
        {
            return token.Flags.HasFlag(TokenFlags.Fungible);
        }

        public static bool IsBurnable(this IToken token)
        {
            return token.Flags.HasFlag(TokenFlags.Burnable);
        }

        public static bool IsTransferable(this IToken token)
        {
            return token.Flags.HasFlag(TokenFlags.Transferable);
        }

        public static bool IsCapped(this IToken token)
        {
            return token.MaxSupply > 0;
        }

        public static T GetKind<T>(this Event evt)
        {
            return (T)(object)evt.Kind;
        }

        public static T GetContent<T>(this Event evt)
        {
            return Serialization.Unserialize<T>(evt.Data);
        }

        public static T DecodeCustomEvent<T>(this EventKind kind)
        {
            if (kind < EventKind.Custom)
            {
                throw new Exception("Cannot cast system event");
            }

            var type = typeof(T);
            if (!type.IsEnum)
            {
                throw new Exception("Can only cast event to other enum");
            }

            var intVal = ((int)kind - (int)EventKind.Custom);
            var temp = (T)Enum.Parse(type, intVal.ToString());
            return temp;
        }

        public static EventKind EncodeCustomEvent(Enum kind)
        {
            var temp = (EventKind)((int)Convert.ChangeType(kind, kind.GetTypeCode()) + (int)EventKind.Custom);
            return temp;
        }

        public static Address GetChainAddress(this IPlatform platform)
        {
            return Address.FromHash(platform.Name);
        }

        public static IBlock GetLastBlock(this IRuntime runtime)
        {
            if (runtime.Chain.Height < 1)
            {
                return null;
            }

            return runtime.GetBlockByHeight(runtime.Chain.Height);
        }

        public static IContract GetContract(this IRuntime runtime, NativeContractKind nativeContract)
        {
            return runtime.GetContract(nativeContract.GetContractName());
        }

        public static string GetContractName(this NativeContractKind nativeContract)
        {
            return nativeContract.ToString().ToLower();
        }

        public static IChain GetRootChain(this IRuntime runtime)
        {
            return runtime.GetChainByName(DomainSettings.RootChainName);
        }

        public static void Notify<T>(this IRuntime runtime, Enum kind, Address address, T content)
        {
            var intVal = (int)(object)kind;
            runtime.Notify<T>((EventKind)(EventKind.Custom + intVal), address, content);
        }

        public static void Notify<T>(this IRuntime runtime, EventKind kind, Address address, T content)
        {
            var bytes = content == null ? new byte[0] : Serialization.Serialize(content);
            runtime.Notify(kind, address, bytes);
        }

        public static bool IsReadOnlyMode(this IRuntime runtime)
        {
            return runtime.Transaction == null;
        }

        public static bool IsRootChain(this IRuntime runtime)
        {
            var rootChain = runtime.GetRootChain();
            return runtime.Chain.Address == rootChain.Address;
        }

        public static InteropBlock ReadBlockFromOracle(this IRuntime runtime, string platform, string chain, Hash hash)
        {
            var bytes = runtime.ReadOracle($"interop://{platform}/{chain}/block/{hash}");
            var block = Serialization.Unserialize<InteropBlock>(bytes);
            return block;
        }

        public static InteropTransaction ReadTransactionFromOracle(this IRuntime runtime, string platform, string chain, Hash hash)
        {
            var url = GetOracleTransactionURL(platform, chain, hash);
            var bytes = runtime.ReadOracle(url);
            var tx = Serialization.Unserialize<InteropTransaction>(bytes);
            return tx;
        }

        public static InteropNFT ReadNFTFromOracle(this IRuntime runtime, string platform, string symbol, BigInteger tokenID)
        {
            var url = GetOracleNFTURL(platform, symbol, tokenID);
            var bytes = runtime.ReadOracle(url);
            var nft = Serialization.Unserialize<InteropNFT>(bytes);
            return nft;
        }

        public static BigInteger ReadFeeFromOracle(this IRuntime runtime, string platform)
        {
            var url = GetOracleFeeURL(platform);
            var bytes = runtime.ReadOracle(url);
            BigInteger fee;
            if (bytes == null)
            {
                fee = runtime.GetGovernanceValue("interop.fee");
            }
            else
            {
                fee = new BigInteger(bytes);
            }
            //fee = BigInteger.FromUnsignedArray(bytes, true);
            return fee;
        }

        public static string GetOracleTransactionURL(string platform, string chain, Hash hash)
        {
            return $"interop://{platform}/{chain}/tx/{hash}";
        }

        public static string GetOracleBlockURL(string platform, string chain, Hash hash)
        {
            return $"interop://{platform}/{chain}/block/{hash}";
        }

        public static string GetOracleBlockURL(string platform, string chain, BigInteger height)
        {
            return $"interop://{platform}/{chain}/block/{height}";
        }

        public static string GetOracleNFTURL(string platform, string symbol, BigInteger tokenID)
        {
            return $"interop://{platform}/nft/{symbol}/{tokenID}";
        }

        public static string GetOracleFeeURL(string platform)
        {
            return $"fee://{platform}";
        }

        public static BigInteger GetBlockCount(this IArchive archive)
        {
            var total = (archive.Size / DomainSettings.ArchiveBlockSize);

            if (archive.Size % DomainSettings.ArchiveBlockSize != 0)
            {
                total++;
            }

            return total;
        }

        // price is in quote Tokens
        public static BigInteger ConvertQuoteToBase(this IRuntime runtime, BigInteger quoteAmount, BigInteger price, IToken baseToken, IToken quoteToken)
        {
            var temp = UnitConversion.ToDecimal(quoteAmount, quoteToken.Decimals) / UnitConversion.ToDecimal(price, quoteToken.Decimals);
            return UnitConversion.ToBigInteger(temp, baseToken.Decimals);
        }

        public static BigInteger ConvertBaseToQuote(this IRuntime runtime, BigInteger baseAmount, BigInteger price, IToken baseToken, IToken quoteToken)
        {
            var baseVal = UnitConversion.ToDecimal(baseAmount, baseToken.Decimals);
            var quoteVal = UnitConversion.ToDecimal(price, quoteToken.Decimals);
            var temp = baseVal * quoteVal;
            return UnitConversion.ToBigInteger(temp, quoteToken.Decimals);
        }


        public static BigInteger GetTokenQuote(this IRuntime runtime, string baseSymbol, string quoteSymbol, BigInteger amount)
        {
            if (runtime.ProtocolVersion >= 3)
            {
                return GetTokenQuoteV2(runtime, baseSymbol, quoteSymbol, amount);
            }
            else
            {
                return GetTokenQuoteV1(runtime, baseSymbol, quoteSymbol, amount);
            }
        }

        // converts amount in baseSymbol to amount in quoteSymbol
        public static BigInteger GetTokenQuoteV1(IRuntime runtime, string baseSymbol, string quoteSymbol, BigInteger amount)
        {

            if (baseSymbol == quoteSymbol)
                return amount;

            // old
            var basePrice = runtime.GetTokenPrice(baseSymbol);

            var baseToken = runtime.GetToken(baseSymbol);
            var fiatToken = runtime.GetToken(DomainSettings.FiatTokenSymbol);

            // this gives how many dollars is "amount"
            BigInteger result = runtime.ConvertBaseToQuote(amount, basePrice, baseToken, fiatToken);
            if (quoteSymbol == DomainSettings.FiatTokenSymbol)
            {
                return result;
            }

            var quotePrice = runtime.GetTokenPrice(quoteSymbol);
            var quoteToken = runtime.GetToken(quoteSymbol);

            result = runtime.ConvertQuoteToBase(result, quotePrice, quoteToken, fiatToken);
            return result;
        }

        public static BigInteger GetTokenQuoteV2(IRuntime runtime, string baseSymbol, string quoteSymbol, BigInteger amount)
        {
            if (baseSymbol == quoteSymbol)
                return amount;

            var basePrice = runtime.GetTokenPrice(baseSymbol);

            var baseToken = runtime.GetToken(baseSymbol);

            if (quoteSymbol == DomainSettings.FiatTokenSymbol)
            {
                var fiatToken = runtime.GetToken(DomainSettings.FiatTokenSymbol);

                // this gives how many dollars is "amount"
                BigInteger result = runtime.ConvertBaseToQuote(amount, basePrice, baseToken, fiatToken);

                return result;
            }
            else
            {
                var quotePrice = runtime.GetTokenPrice(quoteSymbol);
                var quoteToken = runtime.GetToken(quoteSymbol);

                if (quoteToken.Decimals <= baseToken.Decimals)
                {
                    var result = ((basePrice * amount) / quotePrice);

                    var diff = baseToken.Decimals - quoteToken.Decimals;
                    var pow = BigInteger.Pow(10, diff);
                    result /= pow;

                    return result;
                }
                else // here we invert order of calculations for improved precision
                {
                    var diff = quoteToken.Decimals - baseToken.Decimals;
                    var pow = BigInteger.Pow(10, diff);

                    amount *= pow;

                    var result = ((basePrice * amount) / quotePrice);

                    return result;
                }
            }
        }
        public static TriggerResult InvokeTrigger(this IRuntime runtime, bool allowThrow, byte[] script, NativeContractKind contextName, ContractInterface abi, string triggerName, params object[] args)
        {
            return runtime.InvokeTrigger(allowThrow, script, contextName.ToString().ToLower(), abi, triggerName, args);
        }

        public static VMObject CallNFT(this IRuntime runtime, string symbol, BigInteger seriesID, ContractMethod method, params object[] args)
        {
            //var series = Nexus.GetTokenSeries(this.RootStorage, symbol, seriesID);
            var contextName = $"{symbol}#{seriesID}";

            return runtime.CallContext(contextName, (uint)method.offset, method.name, args);
        }

        public static VMObject CallContext(this IRuntime runtime, string contextName, ContractMethod method, params object[] args)
        {
            runtime.Expect(method != null, "trying to call null method for context: " + contextName);

            NativeContractKind nativeKind;
            if (Enum.TryParse<NativeContractKind>(contextName, true, out nativeKind))
            {
                return runtime.CallContext(contextName, 0, method.name, args);
            }

            runtime.Expect(method.offset >= 0, "invalid offset for method: " + method.name);
            return runtime.CallContext(contextName, (uint)method.offset, method.name, args);
        }

        public static VMObject CallNativeContext(this IRuntime runtime, NativeContractKind nativeContract, string methodName, params object[] args)
        {
            return runtime.CallContext(nativeContract.GetContractName(), 0, methodName, args);
        }
    }
}
