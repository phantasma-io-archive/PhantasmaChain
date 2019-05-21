using System;
using System.Collections.Generic;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain
{
    public delegate byte[] OracleReaderDelegate(Hash hash, string url);

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
        public static byte[] DecodeOracle(string input)
        {
            throw new NotImplementedException();
        }
    }
}
