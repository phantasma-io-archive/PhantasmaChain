using Phantasma.Core.Types;
using System.Collections.Generic;
using Phantasma.Domain;
using NativeBigInt = System.Numerics.BigInteger;
using System;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain
{
    public class BlockOracleReader : OracleReader
    {
        private List<OracleEntry> _oracleData = new List<OracleEntry>();

        public BlockOracleReader(Nexus nexus, Block block) : base(nexus)
        {
            foreach (var entry in block.OracleData)
            {
                _oracleData.Add((OracleEntry)entry);
            }
        }

        public override T Read<T>(Timestamp time, string url) 
        {
            T content = null;

            foreach (var data in _oracleData)
            {
                var tag = url.Substring(url.IndexOf("//")+2);
                if (string.Equals(data.URL, tag))
                {
                    content = data.Content as T;
                    break;
                }
            }

            return content;
        }

        public override List<InteropBlock> ReadAllBlocks(string platformName, string chainName)
        {
            throw new NotImplementedException();
        }

        public override string GetCurrentHeight(string platformName, string chainName)
        {
            throw new NotImplementedException();
        }

        public override void SetCurrentHeight(string platformName, string chainName, string height)
        {
            throw new NotImplementedException();
        }

        protected override InteropBlock PullPlatformBlock(string platformName, string chainName, Hash hash, NativeBigInt height = new NativeBigInt())
        {
            throw new NotImplementedException();
        }

        protected override decimal PullPrice(Timestamp time, string symbol)
        {
            throw new NotImplementedException();
        }

        protected override InteropTransaction PullPlatformTransaction(string platformName, string chainName, Hash hash)
        {
            throw new NotImplementedException();
        }

        protected override T PullData<T>(Timestamp time, string url)
        {
            throw new NotImplementedException();
        }

        public new void Clear()
        {
        }
    }
}
