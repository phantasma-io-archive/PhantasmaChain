using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Phantasma.Cryptography;
using Phantasma.Network.P2P;
using Phantasma.Core;
using Phantasma.Core.Log;

namespace Phantasma.Blockchain.Consensus
{
    public sealed partial class Node: Runnable
    {
        public readonly int Port;
 
        private Router router;

        private ConcurrentQueue<DeliveredMessage> queue = new ConcurrentQueue<DeliveredMessage>();
        private ConcurrentDictionary<Hash, Transaction> _mempool = new ConcurrentDictionary<Hash, Transaction>();

        public Address Address => keys.Address;

        public readonly KeyPair keys;

        public readonly Logger Log;

        public Node(Nexus nexus, KeyPair keys, int port, IEnumerable<Endpoint> seeds, Logger log)
        {
            this.keys = keys;
            this.Port = port;

            this.Log = Logger.Init(log);

            //          this.ID = ID.FromBytes(keys.PublicKey);

            this.State = RaftState.Invalid;

            this.router = new Router(nexus, seeds, Port, queue, log);

            //var kademliaNode = new KademliaNode(server, this.ID);
            //this.dht = new DHT(seeds.First(), kademliaNode, false);
        }

        protected override bool Run()
        {
            UpdateRAFT();

            DeliveredMessage item;
            while (queue.TryDequeue(out item))
            {
                HandleMessage(item);
            };

            Thread.Sleep(15);
            return true;
        }

        protected override void OnStart()
        {
            this.router.Start();
        }

        protected override void OnStop()
        {
            this.router.Stop();
        }

        private bool IsLeader(Message msg) {
            if (Leader == null) {
                return false;
            }

            return msg.IsSigned && Leader == msg.Address;
        }

        private void UpdateLeader(Message msg) {
            if (Leader != Address.Null && Leader == msg.Address)
            {
                _lastLeaderBeat = DateTime.UtcNow;
            }
        }

        private void HandleMessage(DeliveredMessage item)
        {
            var msg = item.message;

            switch (msg.Opcode) {

                case Opcode.PEER_Join:
                    {
                        // TODO add peer to list and send him list of peers
                        if (msg.IsSigned)
                        {
                        }
                        else {
                            // send error
                        }
                        break;
                    }

                case Opcode.PEER_Leave:
                    {
                        if (IsLeader(msg)) {
                            Leader = Address.Null;
                        }
                        break;
                    }

                case Opcode.PEER_List:
                    {
                        // TODO check for any unknown peer and add to the list
                        break;
                    }

                    // get a request for voting
                case Opcode.RAFT_Request:
                    {
                        if (this.Vote == null) {
                            this.Vote = msg.Address;
                            // TODO send vote msg
                        }
                        
                        break;
                    }

                case Opcode.RAFT_Vote:
                    {
                        if (!ReceivedVotes.Contains(msg.Address)) {
                            ReceivedVotes.Add(msg.Address);

                            // TODO check if received majority votes, become leader
                        }
                        break;
                    }

                case Opcode.RAFT_Lead:
                    {
                        // TODO verify sigs of majority
                        if (!IsLeader(msg))
                        {
                            Leader = msg.Address;
                            UpdateLeader(msg);

                            SetState(RaftState.Follower);
                        }

                        break;
                    }

                    // received from leader new content
                case Opcode.RAFT_Replicate:
                    {
                        if (IsLeader(msg))
                        {
                            UpdateLeader(msg);
                        }

                        break;
                    }

                case Opcode.RAFT_Confirm:
                    {
                        if (this.State == RaftState.Leader)
                        {
                        }
                        else {
                            // send error 
                        }

                        break;
                    }

                case Opcode.RAFT_Commit:
                    {
                        UpdateLeader(msg);

                        break;
                    }

                case Opcode.RAFT_Beat:
                    {
                        if (Leader == Address.Null) {
                            Leader = msg.Address;
                        }

                        UpdateLeader(msg);

                        break;
                    }

                case Opcode.MEMPOOL_Add:
                    {
                        break;
                    }

                case Opcode.MEMPOOL_Get:
                    {
                        break;
                    }

                case Opcode.BLOCKS_List:
                    {
                        break;
                    }

                case Opcode.BLOCKS_Request:
                    {
                        break;
                    }

                case Opcode.CHAIN_Request:
                    {
                        break;
                    }

                case Opcode.CHAIN_Values:
                    {
                        break;
                    }

                case Opcode.SHARD_Submit:
                    {
                        break;
                    }

                case Opcode.ERROR:
                    {
                        break;
                    }
            }
        }


    }
}
