using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core.Types;
using Phantasma.Blockchain.Tokens;
using System.Linq;

namespace Phantasma.Blockchain.Consensus
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

            do
            {
                BigInteger n = new BigInteger(block.Hash.ToByteArray());
                if (n < target)
                {
                    break;
                }

                block.UpdateHash(block.Nonce + 1);
            } while (true);

            return block;
        }
    }
}
