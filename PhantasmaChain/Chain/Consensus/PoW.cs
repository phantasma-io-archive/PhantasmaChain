using System;
using System.Collections.Generic;
using Phantasma.Mathematics;
using Phantasma.VM.Types;

namespace Phantasma.Blockchain.Consensus
{
    public class ProofOfWork
    {
        public static Block MineBlock(Chain chain, Address minerAddress, IEnumerable<Transaction> txs)
        {
            var timestamp = Timestamp.Now;
            var block = new Block(timestamp, minerAddress, chain.NativeTokenAddress, txs, chain.lastBlock);

            BigInteger target = 0;
            for (int i = 0; i <= block.difficulty; i++)
            {
                BigInteger k = 1;
                k <<= i;
                target += k;
            }

            do
            {
                BigInteger n = block.Hash;
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
