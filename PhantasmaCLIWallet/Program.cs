using PhantasmaChain.Core;
using PhantasmaChain.Cryptography;
using PhantasmaChain.Transactions;
using System;
using System.Collections.Generic;

namespace PhantasmaCLIWallet
{
    class Program
    {
        static void PrintChain(Chain chain)
        {
            Console.WriteLine("Listing blocks...");
            foreach (var block in chain.Blocks)
            {
                Console.WriteLine("Block #" + block.Height);
                Console.WriteLine("\tHash: " + Base58.Encode(block.Hash));

                Console.WriteLine("\tTransactions: ");
                int index = 0;
                foreach (var tx in block.Transactions)
                {
                    Console.WriteLine("\t\tTransaction #" + index);
                    Console.WriteLine("\t\tHash: " + Base58.Encode(tx.Hash));
                    Console.WriteLine("\t\tKind: " + tx.Kind);
                    Console.WriteLine();

                    index++;
                }

                Console.WriteLine("\tEvents: ");
                index = 0;
                foreach (var evt in block.Events)
                {
                    Console.WriteLine("\t\tEvent #" + index);
                    Console.WriteLine("\t\tKind: " + evt.Kind);
                    Console.WriteLine("\t\tTarget: " + ChainUtils.PublicKeyToAddress(evt.PublicKey));
                    Console.WriteLine();

                    index++;
                }

                Console.WriteLine();
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Initializng chain...");

            var owner = KeyPair.Random();
            var chain = new Chain(owner, x => Console.WriteLine(x));

            var miner = KeyPair.Random();
            var third = KeyPair.Random();

            var tx = new TransferTransaction(owner.PublicKey, 0, 1, chain.NativeToken.ID, third.PublicKey, 5);
            tx.Sign(owner);
            var block = new Block(DateTime.UtcNow.ToTimestamp(), miner.PublicKey, new List<Transaction>() { tx }, chain.lastBlock);
            chain.AddBlock(block);

            PrintChain(chain);

            Console.ReadLine();
        }
    }
}
