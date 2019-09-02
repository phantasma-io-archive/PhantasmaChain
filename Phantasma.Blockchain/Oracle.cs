using System;
using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain
{
    public delegate byte[] OracleReaderDelegate(string url);

    public struct OracleEntry
    {
        public readonly string URL;
        public readonly byte[] Content;

        public OracleEntry(string uRL, byte[] content)
        {
            URL = uRL;
            Content = content;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is OracleEntry))
            {
                return false;
            }

            var entry = (OracleEntry)obj;
            return URL == entry.URL &&
                   EqualityComparer<byte[]>.Default.Equals(Content, entry.Content);
        }

        public override int GetHashCode()
        {
            var hashCode = 1993480784;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(URL);
            hashCode = hashCode * -1521134295 + EqualityComparer<byte[]>.Default.GetHashCode(Content);
            return hashCode;
        }
    }

    public static class OracleUtils
    {
        // returns USD value
        public static BigInteger GetPrice(OracleReaderDelegate reader, Nexus nexus, string symbol)
        {
            if (symbol == "USD")
            {
                return 1;
            }

            if (symbol == "KCAL")
            {
                var result = GetPrice(reader, nexus, "SOUL");
                result /= 5;
                return result;
            }

            var bytes = reader("price://" + symbol);
            var value = BigInteger.FromUnsignedArray(bytes, true);
            return value;
        }

        public static BigInteger GetQuote(OracleReaderDelegate reader, Nexus nexus, string baseSymbol, string quoteSymbol, BigInteger amount)
        {
            var basePrice = GetPrice(reader, nexus, baseSymbol);
            var quotePrice = GetPrice(reader, nexus, quoteSymbol);

            BigInteger result;

            var baseToken = nexus.GetTokenInfo(baseSymbol);
            var quoteToken = nexus.GetTokenInfo(quoteSymbol);

            result = basePrice * amount;
            result = UnitConversion.ConvertDecimals(result, baseToken.Decimals, Nexus.FiatTokenDecimals);

            result /= quotePrice;

            result = UnitConversion.ConvertDecimals(result, Nexus.FiatTokenDecimals, quoteToken.Decimals);

            return result;
        }
    }
}
