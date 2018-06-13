using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Network.Kademlia;
using Phantasma.Utils;
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

        static void Main(string[] args)
        {
            var bytes = "540350540350540350".HexToBytes();

            //IProtocol protocol = new TcpSubnetProtocol("http://127.0.0.1", 2720, n);
            /*var protocol = new VirtualProtocol();

            DHT dht = new DHT(ID.RandomID, protocol, () => new VirtualStorage(), new Router());
            

            //server.RegisterProtocol(n, dht.Node);
            ((VirtualProtocol)protocol).Node = dht.Node;*/


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

            Console.WriteLine("Finished");
            Console.ReadLine();
        }
    }
}
