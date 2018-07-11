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
    public sealed partial class Node: Runnable
    {
        public const int Port = 9600;
        public const int MaxConnections = 64;

        private Router router;

        private ConcurrentQueue<DeliveredMessage> queue = new ConcurrentQueue<DeliveredMessage>();
        private ConcurrentDictionary<UInt256, Transaction> _mempool = new ConcurrentDictionary<UInt256, Transaction>();

        public byte[] PublicKey => keys.PublicKey;

        private KeyPair keys;

        public readonly Logger Log;

        public Node(KeyPair keys, IEnumerable<Endpoint> seeds, Logger log)
        {
            this.keys = keys;
            this.Log = Logger.Init(log);

            //          this.ID = ID.FromBytes(keys.PublicKey);

            this.State = RaftState.Invalid;

            this.router = new Router(seeds, Port, queue, log);

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
