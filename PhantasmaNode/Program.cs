using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Phantasma.Blockchain.Consensus;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Network;
using Phantasma.Utils;
using Phantasma.Mathematics;
using Phantasma.Utils.Log;

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
                Console.WriteLine("\tHash: " + block.Hash);

                Console.WriteLine("\tTransactions: ");
                int index = 0;
                foreach (var tx in block.Transactions)
                {
                    Console.WriteLine("\t\tTransaction #" + index);
                    Console.WriteLine("\t\tHash: " + tx.Hash);
                    Console.WriteLine();

                    index++;
                }

                Console.WriteLine("\tEvents: ");
                index = 0;
                foreach (var evt in block.Events)
                {
                    Console.WriteLine("\t\tEvent #" + index);
                    Console.WriteLine("\t\tKind: " + evt.Kind);
                    Console.WriteLine("\t\tTarget: " + evt.Address.Text);
                    Console.WriteLine();

                    index++;
                }

                Console.WriteLine();
            }
        }

        static void TestChain(Logger log)
        {
            Console.WriteLine("Initializng chain...");

            var owner = KeyPair.Generate();
            Console.WriteLine("Genesis Address: " + owner.Address);

            var chain = new Chain(owner, log);

            var miner = KeyPair.Generate();
            var third = KeyPair.Generate();

            var nativeToken = Chain.GetNativeContract(NativeContractKind.Token);
            var tx = new Transaction(owner.Address, ScriptUtils.TransferScript(nativeToken.Address, owner.Address, third.Address, 5), 0, 0);
            tx.Sign(owner);

            var nextHeight = chain.lastBlock.Height + 1;
            Console.WriteLine("Mining block #" + nextHeight);

            var block = ProofOfWork.MineBlock(chain, miner.Address, new List<Transaction>() { tx });
            chain.AddBlock(block);

            PrintChain(chain);
        }

        static void Main(string[] args)
        {
            /*var publicKey = new byte[32];
            var rnd = new Random();
            rnd.NextBytes(publicKey);
            byte opcode = 1;
            do
            {
                var bytes = new byte[] { opcode }.Concat(publicKey).ToArray();
                var str = Base58.Encode(bytes);
                if (str.StartsWith("C"))
                {
                    return;
                }
                opcode++;
            } while (opcode < 255);
            */

            var node_keys = KeyPair.Generate();
            var log = new ConsoleLogger();
            var seeds = new List<Endpoint>();
            int port = 6060;
            var node = new Node(node_keys, port, seeds, log);

            Console.WriteLine("Phantasma Node starting...");
            node.Start();

            Console.CancelKeyPress += delegate {
                Console.WriteLine("Phantasma Node stopping...");
                node.Stop();
            };

            while (node.IsRunning)
            {
                Thread.Sleep(100);
            }
        }
    }
}
