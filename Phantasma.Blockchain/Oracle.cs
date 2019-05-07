using System;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain
{
    public delegate byte[] OracleReaderDelegate(Hash hash, string url);

    public static class OracleUtils
    {
        public static byte[] DecodeOracle(string input)
        {
            throw new NotImplementedException();
        }
    }
}
