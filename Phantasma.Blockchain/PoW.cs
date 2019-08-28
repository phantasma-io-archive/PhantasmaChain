using System.Collections.Generic;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using System.Linq;
using System;

namespace Phantasma.Blockchain
{
    public class ProofOfWork
    {
        public static Block MineBlock(Chain chain, IEnumerable<Transaction> txs, byte[] extraContent = null)
        {
            var timestamp = Timestamp.Now;

            var hashes = txs.Select(tx => tx.Hash);
            var block = new Block(chain.LastBlock.Height + 1, chain.Address, timestamp, hashes, chain.LastBlock.Hash, extraContent);

            var blockDifficulty = Block.InitialDifficulty; // TODO change this later

            BigInteger target = 0;
            for (int i = 0; i <= blockDifficulty; i++)
            {
                BigInteger k = 1;
                k <<= i;
                target += k;
            }

            uint nonce = 0;
            do
            {
                BigInteger n = BigInteger.FromSignedArray(block.Hash.ToByteArray());
                if (n < target)
                {
                    break;
                }

                nonce++;
                var payload = BitConverter.GetBytes(nonce);
                block.UpdateHash(payload);
            } while (true);

            return block;
        }
    }
}
