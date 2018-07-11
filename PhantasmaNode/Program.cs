using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Consensus;
using Phantasma.Utils;
using System;
using System.Collections.Generic;
using Phantasma.Network;
using System.Threading;

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
                    Console.WriteLine();

                    index++;
                }

                Console.WriteLine("\tEvents: ");
                index = 0;
                foreach (var evt in block.Events)
                {
                    Console.WriteLine("\t\tEvent #" + index);
                    Console.WriteLine("\t\tKind: " + evt.Kind);
                    Console.WriteLine("\t\tTarget: " + evt.PublicKey.PublicKeyToAddress());
                    Console.WriteLine();

                    index++;
                }

                Console.WriteLine();
            }
        }

        static void TestChain() {
            Console.WriteLine("Initializng chain...");

            var owner = KeyPair.Random();
            Console.WriteLine("Genesis Address: " + owner.address);

            var chain = new Chain(owner);

            var miner = KeyPair.Random();
            var third = KeyPair.Random();

            var tx = new Transaction(owner.PublicKey, ScriptUtils.TransferScript(chain.NativeTokenPubKey, owner.PublicKey, third.PublicKey, 5), 0, 0);
            tx.Sign(owner);

            var nextHeight = chain.lastBlock.Height + 1;
            Console.WriteLine("Mining block #" + nextHeight);

            var block = ProofOfWork.MineBlock(chain, miner.PublicKey, new List<Transaction>() { tx });
            chain.AddBlock(block);

            PrintChain(chain);
        }

        static void Main(string[] args)
        {
            var node_keys = KeyPair.Random();
            var seeds = new List<Endpoint>();
            var node = new Node(node_keys, seeds);

            Console.WriteLine("Phantasma Node starting...");
            node.Start();

            Console.CancelKeyPress += delegate {
                Console.WriteLine("Phantasma Node stopping...");
                node.Stop();
            };

            while (node.IsRunning) {
                Thread.Sleep(100);
            }
        }
    }
}
