using Phantasma.Core.Types;
using Phantasma.Core;
using System.Collections.Generic;
using Phantasma.Domain;
using NativeBigInt = System.Numerics.BigInteger;
using System;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain
{
    public class BlockOracleReader : OracleReader
    {
        private Block Block;

        public BlockOracleReader(Nexus nexus, Block block) : base(nexus)
        {
            this.Block = block;
        }

        public override T Read<T>(Timestamp time, string url) where T : class 
        {
            Console.WriteLine("read now");
            T content = null;

            foreach (var data in Block.OracleData)
            {
                var tag = url.Substring(url.IndexOf("//")+2);
                if (string.Equals(data.URL, tag))
                {
                    content = data.Content as T;
                    break;
                }
            }

            Throw.IfNull(content, "Block oracle content cannot be null!");
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
            Console.WriteLine("Cleared!!!!!!!!11");
        }
    }
}
