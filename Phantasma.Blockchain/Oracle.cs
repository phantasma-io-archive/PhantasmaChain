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
        public static BigInteger GetPrice(OracleReaderDelegate reader, string symbol)
        {
            if (symbol == "USD")
            {
                return 1;
            }

            var bytes = reader("price://" + symbol);
            var value = BigInteger.FromUnsignedArray(bytes, true);
            return value;
        }

        public static BigInteger GetQuote(OracleReaderDelegate reader, BigInteger amount, string baseSymbol, string quoteSymbol)
        {
            var basePrice = GetPrice(reader, baseSymbol);
            var quotePrice = GetPrice(reader, quoteSymbol);
            throw new NotImplementedException();
        }
    }
}
