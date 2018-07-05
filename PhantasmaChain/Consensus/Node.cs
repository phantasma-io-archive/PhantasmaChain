using Phantasma.Core;
using Phantasma.Network;
using Phantasma.Network.Kademlia;
using Phantasma.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Phantasma.Consensus
{
    public sealed class Node
    {
        public const int Port = 9600;
        public const int MaxConnections = 64;

        public readonly ID ID;
        private DHT dht;

        public RaftState State { get; private set; }
        private NetPeer Leader = null;

        public bool Running { get; private set; }

        public Node(KeyPair keys, IEnumerable<Endpoint> seeds)
        {
            this.ID = ID.FromBytes(keys.PublicKey);

            this.State = RaftState.Follower;

            var listener = new EventBasedNetListener();
            var server = new NetManager(listener, MaxConnections, "Phantasma");

            if (seeds.Any())
            {
                foreach (var seed in seeds)
                {
                    server.Connect(seed);
                }
            }
            else
            {
                server.Start(Port);
            }

            var kademliaNode = new KademliaNode(server, this.ID);
            this.dht = new DHT(seeds.First(), kademliaNode, false);

            listener.PeerConnectedEvent += peer =>
            {
                Logger.Message($"Got connection: {peer.EndPoint}"); // Show peer ip
                var writer = new NetDataWriter();
                writer.Put("Hello client!");                                // Put some string
                peer.Send(writer, SendOptions.ReliableOrdered);             // Send with reliability
            };

            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                this.Running = true;
                while (Running)
                {
                    server.PollEvents();
                    Thread.Sleep(15);
                }

                server.Stop();
            }).Start();
        }

        private bool _active;

        public void Stop()
        {
            this.Running = false;
        }
    }
}
