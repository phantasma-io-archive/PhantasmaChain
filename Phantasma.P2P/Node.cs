using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using System.Net;
using System;
using System.Threading.Tasks;
using Phantasma.Cryptography;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Numerics;
using Phantasma.Blockchain;
using Phantasma.Network.P2P.Messages;

namespace Phantasma.Network.P2P
{
    public enum EndpointStatus
    {
        Waiting,
        Disabled,
        Connected,
    }

    public class EndpointEntry
    {
        public readonly Endpoint endpoint;
        public DateTime lastPing;
        public int pingDelay;
        public EndpointStatus status;

        public EndpointEntry(Endpoint endpoint)
        {
            this.endpoint = endpoint;
            this.lastPing = DateTime.UtcNow;
            this.pingDelay = 32;
            this.status = EndpointStatus.Waiting;
        }
    }

    public sealed partial class Node: Runnable
    {
        public readonly static int MaxActiveConnections = 64;

        public readonly int Port;
        public Address Address => keys.Address;

        public readonly KeyPair keys;
        public readonly Logger Logger;

        public IEnumerable<Peer> Peers => _peers;

        private Mempool _mempool;

        private List<Peer> _peers = new List<Peer>();

        private TcpClient client;
        private TcpListener listener;

        private List<EndpointEntry> _knownEndpoints = new List<EndpointEntry>();

        private bool listening = false;

        public Nexus Nexus { get; private set; }

        public Node(Nexus nexus, Mempool mempool, KeyPair keys, int port, IEnumerable<string> seeds, Logger log)
        {
            Throw.IfNull(mempool, nameof(mempool));
            Throw.If(keys.Address != mempool.ValidatorAddress, "invalid mempool");

            this.Nexus = nexus;
            this.Port = port;
            this.keys = keys;

            this.Logger = Logger.Init(log);

            this._mempool = mempool;

            QueueEndpoints(seeds.Select(seed => ParseEndpoint(seed)));

            // TODO this is a security issue, later change this to be configurable and default to localhost
            var bindAddress = IPAddress.Any;

            listener = new TcpListener(bindAddress, port);
            client = new TcpClient();
        }

        private void QueueEndpoints(IEnumerable<Endpoint> endpoints)
        {
            Throw.IfNull(endpoints, nameof(endpoints));

            if (!endpoints.Any())
            {
                return;
            }

            lock (_knownEndpoints)
            {
                foreach (var endpoint in endpoints)
                {
                    var entry = new EndpointEntry(endpoint);
                    _knownEndpoints.Add(entry);
                }
            }
        }

        public Endpoint ParseEndpoint(string src)
        {
            int port;

            if (src.Contains(":"))
            {
                var temp = src.Split(':');
                Throw.If(temp.Length != 2, "Invalid endpoint format");
                src = temp[0];
                port = int.Parse(temp[1]);
            }
            else
            {
                port = this.Port;
            }

            IPAddress ipAddress;

            if (!IPAddress.TryParse(src, out ipAddress))
            {
                if (Socket.OSSupportsIPv6)
                {
                    if (src == "localhost")
                    {
                        ipAddress = IPAddress.IPv6Loopback;
                    }
                    else
                    {
                        ipAddress = Endpoint.ResolveAddress(src, AddressFamily.InterNetworkV6);
                    }
                }
                if (ipAddress == null)
                {
                    ipAddress = Endpoint.ResolveAddress(src, AddressFamily.InterNetwork);
                }
            }

            if (ipAddress == null)
            {
                throw new Exception("Invalid address: " + src);
            }
            else
            {
                src = ipAddress.ToString();
            }

            return new Endpoint(PeerProtocol.TCP, src, port);
        }

        protected override bool Run()
        {
            ConnectToPeers();

            if (!listening)
            {
                listening = true;
                var accept = listener.BeginAcceptSocket(new AsyncCallback(DoAcceptSocketCallback), listener);
            }

            return true;
        }

        protected override void OnStart()
        {
            Logger.Message($"Phantasma node listening on port {Port}, with address {Address}...");

            listener.Start();
        }

        protected override void OnStop()
        {
            listener.Stop();
        }

        private bool IsKnown(Endpoint endpoint)
        {
            lock (_peers)
            {
                foreach (var peer in _peers)
                {
                    if (peer.Endpoint.Equals(endpoint))
                    {
                        return true;
                    }
                }
            }

            lock (_knownEndpoints)
            {
                foreach (var peer in _knownEndpoints)
                {
                    if (peer.endpoint.Equals(endpoint))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void ConnectToPeers()
        {
            lock (_peers)
            {
                if (_peers.Count >= MaxActiveConnections)
                {
                    return;
                }
            }

            lock (_knownEndpoints)
            {
                _knownEndpoints.RemoveAll(x => x.endpoint.Protocol != PeerProtocol.TCP);

                var possibleTargets = new List<int>();
                for (int i=0; i<_knownEndpoints.Count; i++)
                {
                    if (_knownEndpoints[i].status == EndpointStatus.Waiting)
                    {
                        possibleTargets.Add(i);
                    }
                }

                if (possibleTargets.Count > 0)
                {
                    // adds a bit of pseudo randomness to connection order
                    var idx = Environment.TickCount % possibleTargets.Count;
                    idx = possibleTargets[idx];
                    var target = _knownEndpoints[idx];

                    var result = client.BeginConnect(target.endpoint.Host, target.endpoint.Port, null, null);

                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));

                    if (!success)
                    {
                        Logger.Message("Could not reach peer: " + target.endpoint);
                        target.status = EndpointStatus.Disabled;
                        return;
                    }
                    else
                    {
                        Logger.Message("Connected to peer: " + target.endpoint);
                        client.EndConnect(result);
                        target.status = EndpointStatus.Connected;

                        Task.Run(() => { HandleConnection(client.Client); });
                        return;
                    }
                }
            }

            var disabledConnections = _knownEndpoints.Where(x => x.status == EndpointStatus.Disabled);
            if (disabledConnections.Any())
            {
                lock (_knownEndpoints)
                {
                    var currentTime = DateTime.UtcNow;
                    foreach (var entry in disabledConnections)
                    {
                        var diff = currentTime - entry.lastPing;
                        if (diff.TotalSeconds >= entry.pingDelay)
                        {
                            entry.lastPing = currentTime;
                            entry.pingDelay *= 2;
                            entry.status = EndpointStatus.Waiting;
                        }
                    }
                }
            }
        }

        // Process the client connection.
        public void DoAcceptSocketCallback(IAsyncResult ar)
        {
            listening = false;

            // Get the listener that handles the client request.
            var listener = (TcpListener)ar.AsyncState;

            Socket socket;
            try
            {
                socket = listener.EndAcceptSocket(ar);
            }
            catch (ObjectDisposedException e)
            {
                return;
            }

            Logger.Message("New connection accepted from " + socket.RemoteEndPoint.ToString());
            Task.Run(() => { HandleConnection(socket); });
        }

        private bool SendMessage(Peer peer, Message msg)
        {
            Throw.IfNull(peer, nameof(peer));
            Throw.IfNull(msg, nameof(msg));

            Logger.Message("Sending "+msg.GetType().Name+" to  " + peer.Endpoint);

            msg.Sign(this.keys);

            try
            {
                peer.Send(msg);
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }

        private void HandleConnection(Socket socket)
        {
            var peer = new TCPPeer(socket);
            lock (_peers)
            {
                _peers.Add(peer);
            }

            // this initial message is not only used to fetch chains but also to verify identity of peers
            var request = new RequestMessage(RequestKind.Chains | RequestKind.Peers | RequestKind.Mempool, Nexus.Name, this.Address);
            var active = SendMessage(peer, request);

            while (active)
            {
                var msg = peer.Receive();
                if (msg == null)
                {
                    break;
                }

                Console.WriteLine("Got: " + msg.GetType().Name);
                Console.WriteLine("From: " + msg.Address.Text);
                foreach (var line in msg.GetDescription())
                {
                    Console.WriteLine(line);
                }

                var answer = HandleMessage(peer, msg);
                if (answer != null)
                {
                    if (!SendMessage(peer, answer))
                    {
                        break;
                    }
                }
            }

            Logger.Message("Disconnected from peer: " + peer.Endpoint);

            socket.Close();

            lock (_peers)
            {
                _peers.Remove(peer);
            }
        }

        private Message HandleMessage(Peer peer, Message msg)
        {
            if (msg.IsSigned && msg.Address != Address.Null)
            {
                peer.SetAddress(msg.Address);
            }
            else
            {
                return new ErrorMessage(Address, P2PError.MessageShouldBeSigned);
            }

            switch (msg.Opcode) {
                case Opcode.REQUEST:
                    {
                        var request = (RequestMessage)msg;

                        if (request.NexusName != Nexus.Name)
                        {
                            return new ErrorMessage(Address, P2PError.InvalidNexus);
                        }

                        if (request.Kind == RequestKind.None)
                        {
                            return null;
                        }

                        var answer = new ListMessage(this.Address, request.Kind);

                        if (request.Kind.HasFlag(RequestKind.Peers))
                        {
                            answer.SetPeers(this.Peers.Where(x => x != peer).Select(x => x.Endpoint));
                        }

                        if (request.Kind.HasFlag(RequestKind.Chains))
                        {
                            var chains = Nexus.Chains.Select(x => new ChainInfo(x.Name, x.ParentChain != null ? x.ParentChain.Name: "", x.LastBlock != null ? x.LastBlock.Height : 0));
                            answer.SetChains(chains);
                        }

                        if (request.Kind.HasFlag(RequestKind.Mempool))
                        {
                            var txs = _mempool.GetTransactions().Select(x => Base16.Encode(x.ToByteArray(true)));
                            answer.SetMempool(txs);
                        }

                        if (request.Kind.HasFlag(RequestKind.Blocks))
                        {
                            foreach (var entry in request.Blocks)
                            {
                                var chain = this.Nexus.FindChainByName(entry.Key);
                                if (chain == null)
                                {
                                    continue;
                                }

                                var startBlock = entry.Value;
                                if (startBlock > chain.BlockHeight)
                                {
                                    continue;
                                }

                                var blockList = new List<string>();
                                var currentBlock = startBlock;
                                while (blockList.Count < 50 && currentBlock <= chain.BlockHeight)
                                {
                                    var block = chain.FindBlockByHeight(currentBlock);
                                    var bytes = block.ToByteArray();
                                    var str = Base16.Encode(bytes);

                                    foreach (var tx in chain.GetBlockTransactions(block))
                                    {
                                        var txBytes = tx.ToByteArray(true);
                                        str += "/" + Base16.Encode(txBytes);
                                    }

                                    blockList.Add(str);
                                    currentBlock++;
                                }

                                answer.AddBlockRange(chain.Name, startBlock, blockList);
                            }
                        }

                        return answer;
                    }

                case Opcode.LIST:
                    {
                        var listMsg = (ListMessage)msg;

                        var outKind = RequestKind.None;

                        if (listMsg.Kind.HasFlag(RequestKind.Peers))
                        {
                            var newPeers = listMsg.Peers.Where(x => !IsKnown(x));
                            foreach (var entry in listMsg.Peers)
                            {
                                Logger.Message("New peer: " + entry.ToString());
                            }
                            QueueEndpoints(newPeers);
                        }

                        var blockFetches = new Dictionary<string, uint>();
                        if (listMsg.Kind.HasFlag(RequestKind.Chains))
                        {
                            foreach (var entry in listMsg.Chains)
                            {
                                var chain = Nexus.FindChainByName(entry.name);
                                // NOTE if we dont find this chain then it is too soon for ask for blocks from that chain
                                if (chain != null && chain.BlockHeight < entry.height)
                                {
                                    blockFetches[entry.name] = chain.BlockHeight + 1;
                                }
                            }
                        }

                        if (listMsg.Kind.HasFlag(RequestKind.Mempool))
                        {
                            int submittedCount = 0;
                            foreach (var txStr in listMsg.Mempool)
                            {
                                var bytes = Base16.Decode(txStr);
                                var tx = Transaction.Unserialize(bytes);
                                if (this._mempool.Submit(tx))
                                {
                                    submittedCount++;
                                }

                                Logger.Message(submittedCount + " new transactions");
                            }
                        }

                        if (listMsg.Kind.HasFlag(RequestKind.Blocks))
                        {
                            bool addedBlocks = false;

                            foreach (var entry in listMsg.Blocks)
                            {
                                var chain = Nexus.FindChainByName(entry.Key);
                                if (chain == null)
                                {
                                    continue;
                                }

                                var blockRange = entry.Value;
                                var currentBlock = blockRange.startHeight;
                                foreach (var rawBlock in blockRange.rawBlocks)
                                {
                                    var temp = rawBlock.Split('/');

                                    var block = Block.Unserialize(Base16.Decode(temp[0]));

                                    var transactions = new List<Transaction>();
                                    for (int i= 1; i<temp.Length; i++)
                                    {
                                        var tx = Transaction.Unserialize(Base16.Decode(temp[i]));
                                        transactions.Add(tx);
                                    }

                                    try
                                    {
                                        chain.AddBlock(block, transactions);
                                    }
                                    catch (Exception e)
                                    {
                                        throw new Exception("block add failed");
                                    }

                                    Logger.Message($"Added block #{currentBlock} to {chain.Name}");
                                    addedBlocks = true;
                                    currentBlock++;
                                }
                            }

                            if (addedBlocks)
                            {
                                outKind |= RequestKind.Chains;
                            }
                        }

                        if (blockFetches.Count > 0)
                        {
                            outKind |= RequestKind.Blocks;
                        }

                        if (outKind != RequestKind.None)
                        {
                            var answer = new RequestMessage(outKind, Nexus.Name, this.Address);

                            if (blockFetches.Count > 0)
                            {
                                answer.SetBlocks(blockFetches);
                            }

                            return answer;
                        }

                        break;
                    }

                case Opcode.MEMPOOL_Add:
                    {
                        _mempool.Disabled = true;

                        var memtx = (MempoolAddMessage)msg;
                        var prevSize = _mempool.Size;
                        foreach (var tx in memtx.Transactions)
                        {
                            _mempool.Submit(tx);
                        }
                        var count = _mempool.Size - prevSize;
                        Logger.Message($"Added {count} txs to the mempool");
                        break;
                    }

                case Opcode.BLOCKS_List:
                    {
                        break;
                    }

                case Opcode.ERROR:
                    {
                        var errorMsg = (ErrorMessage)msg;
                        if (string.IsNullOrEmpty(errorMsg.Text))
                        {
                            Logger.Error($"ERROR: {errorMsg.Code}");
                        }
                        else
                        {
                            Logger.Error($"ERROR: {errorMsg.Code} ({errorMsg.Text})");
                        }
                        break;
                    }
            }

            Logger.Message("No answer sent.");
            return null;
        }


    }
}
