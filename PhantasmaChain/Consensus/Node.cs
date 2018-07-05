using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Network;
using Phantasma.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Phantasma.Consensus
{
    public sealed partial class Node
    {
        public const int Port = 9600;
        public const int MaxConnections = 64;

        public bool Running { get; private set; }

        private Router router;

        private ConcurrentQueue<DeliveredMessage> queue;
        private ConcurrentDictionary<UInt256, Transaction> _mempool = new ConcurrentDictionary<UInt256, Transaction>();

        public byte[] PublicKey => keys.PublicKey;

        private KeyPair keys;

        public Node(KeyPair keys, IEnumerable<Endpoint> seeds)
        {
            this.keys = keys;

  //          this.ID = ID.FromBytes(keys.PublicKey);

            this.State = RaftState.Invalid;

            var router = new Router(seeds, Port, queue);

            //var kademliaNode = new KademliaNode(server, this.ID);
            //this.dht = new DHT(seeds.First(), kademliaNode, false);


            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                this.Running = true;
                while (Running)
                {
                    UpdateRAFT();

                    DeliveredMessage item;
                    while (queue.TryDequeue(out item)) {
                        HandleMessage(item);
                    };

                    Thread.Sleep(15);
                }

                //router.Stop();
            }).Start();
        }

        private bool IsLeader(Message msg) {
            if (Leader == null) {
                return false;
            }

            return msg.IsSigned() && Leader.SequenceEqual(msg.PublicKey);
        }

        private void UpdateLeader(Message msg) {
            if (Leader != null && Leader.SequenceEqual(msg.PublicKey))
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
                        if (msg.IsSigned())
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
                            Leader = null;
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
                            this.Vote = msg.PublicKey;
                            // TODO send vote msg
                        }
                        
                        break;
                    }

                case Opcode.RAFT_Vote:
                    {
                        if (!ReceivedVotes.Contains(msg.PublicKey)) {
                            ReceivedVotes.Add(msg.PublicKey);

                            // TODO check if received majority votes, become leader
                        }
                        break;
                    }

                case Opcode.RAFT_Lead:
                    {
                        // TODO verify sigs of majority
                        if (!IsLeader(msg))
                        {
                            Leader = msg.PublicKey;
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
                        if (Leader == null) {
                            Leader = msg.PublicKey;
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

                case Opcode.CHAIN_Height:
                    {
                        break;
                    }

                case Opcode.CHAIN_Get:
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

        private bool _active;

        public void Stop()
        {
            this.Running = false;
        }


    }
}
