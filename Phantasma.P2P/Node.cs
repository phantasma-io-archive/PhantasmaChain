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
using Phantasma.Blockchain.Contracts;
using Phantasma.Network.P2P.Messages;
using Phantasma.Core.Utils;
using Phantasma.Domain;
using System.Threading;

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

    public struct PendingBlock
    {
        public string chain;
        public readonly Block block;
        public readonly List<Transaction> transactions;

        public PendingBlock(string chain, Block block, List<Transaction> transactions)
        {
            this.chain = chain;
            this.block = block;
            this.transactions = transactions;
        }
    }

    public sealed partial class Node : Runnable
    {
        public readonly static int MaxActiveConnections = 64;

        public readonly string Version;

        public readonly int Port;
        public readonly string Host;
        public readonly string PublicEndpoint;
        public readonly PeerCaps Capabilities;

        public Address Address => Keys.Address;

        public readonly PhantasmaKeys Keys;
        public readonly Logger Logger;

        public IEnumerable<Peer> Peers => _peers.Values;

        private Mempool _mempool;

        private Dictionary<string, Peer> _peers = new Dictionary<string, Peer>(StringComparer.InvariantCultureIgnoreCase);

        private TcpListener listener;

        private List<EndpointEntry> _knownEndpoints = new List<EndpointEntry>();

        private bool listening = false;

        public Nexus Nexus { get; private set; }

        public BigInteger MinimumFee => _mempool.MinimumFee;
        public uint MinimumPoW => _mempool.GettMinimumProofOfWork();

        private Dictionary<string, uint> _receipts = new Dictionary<string, uint>();
        private Dictionary<Address, Cache<Event>> _events = new Dictionary<Address, Cache<Event>>();

        private Dictionary<string, PendingBlock> _pendingBlocks = new Dictionary<string, PendingBlock>();
        private Dictionary<string, BigInteger> _knownHeights = new Dictionary<string, BigInteger>(); // known external heights for chains; dictionary key is the chain name

        private DateTime _lastRequestTime = DateTime.UtcNow;

        public bool IsFullySynced { get; private set; }

        public string ProxyURL = null;

        public Node(string version, Nexus nexus, Mempool mempool, PhantasmaKeys keys, string publicHost, int port, PeerCaps caps, IEnumerable<string> seeds, Logger log)
        {
            if (mempool != null)
            {
                Throw.If(!caps.HasFlag(PeerCaps.Mempool), "mempool not included in caps but a mempool instance was passed");
                Throw.If(keys.Address != mempool.ValidatorAddress, "invalid mempool");
            }
            else
            {
                Throw.If(caps.HasFlag(PeerCaps.Mempool), "mempool included in caps but mempool instance is null");
            }

            this.Logger = Logger.Init(log);

            this.Version = version;
            this.Nexus = nexus;
            this.Port = port;
            this.Keys = keys;
            this.Capabilities = caps;

            if (Capabilities.HasFlag(PeerCaps.Sync))
            {
                this.Nexus.AddPlugin(new NodePlugin(this));
            }

            if (Capabilities.HasFlag(PeerCaps.Mempool))
            {
                Throw.IfNull(mempool, nameof(mempool));
                this._mempool = mempool;
            }
            else
            {
                this._mempool = null;
            }

            Throw.IfNullOrEmpty(publicHost, nameof(publicHost));

            Throw.If(publicHost.Contains(":"), "invalid host, protocol or port number should not be included");
            this.Host = publicHost;

            this.PublicEndpoint = $"tcp:{publicHost}:{port}";

            if (this.Capabilities.HasFlag(PeerCaps.Sync))
            {
                QueueEndpoints(seeds);

                // TODO this is a security issue, later change this to be configurable and default to localhost
                var bindAddress = IPAddress.Any;

                listener = new TcpListener(bindAddress, port);

                if (seeds.Any())
                {
                    // temporary HACK
                    var baseURL = "http:" + seeds.First().Split(':')[1];
                    ProxyURL = baseURL + ":7078/api"; 
                }
            }
        }

        private void QueueEndpoints(IEnumerable<string> hosts)
        {
            Throw.IfNull(hosts, nameof(hosts));

            if (!hosts.Any())
            {
                return;
            }

            lock (_knownEndpoints)
            {
                foreach (var host in hosts)
                {
                    Endpoint endpoint = new Endpoint();
                    try
                    {
                        endpoint = Endpoint.FromString(host);
                    }
                    catch (ChainException e)
                    {
                        Logger.Warning("Failed to add endpoint: " + e.Message);
                    }

                    var entry = new EndpointEntry(endpoint);
                    _knownEndpoints.Add(entry);
                }
            }
        }

        public bool ParseEndpoint(string src, out PeerProtocol protocol, out IPAddress ipAddress, out int port)
        {
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

            if (!IPAddress.TryParse(src, out ipAddress))
            {
                //if (Socket.OSSupportsIPv6)
                //{
                //    if (src == "localhost")
                //    {
                //        ipAddress = IPAddress.IPv6Loopback;
                //    }
                //    else
                //    {
                //        ipAddress = Endpoint.ResolveAddress(src, AddressFamily.InterNetworkV6);
                //    }
                //}
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

            protocol = PeerProtocol.TCP;
            return true;
        }

        private DateTime _lastPeerConnect = DateTime.MinValue;

        protected override bool Run()
        {
            Thread.Sleep(1000);

            if (this.Capabilities.HasFlag(PeerCaps.Sync))
            {
                if (!listening)
                {
                    listening = true;
                    var accept = listener.BeginAcceptSocket(new AsyncCallback(DoAcceptSocketCallback), listener);
                }

                var now = DateTime.UtcNow;
                var diff = now - _lastPeerConnect;
                if (diff.TotalSeconds >= 1)
                {
                    ConnectToPeers();
                    _lastPeerConnect = now;
                }
            }

            // check if we have any cached blocks TODO: needs to be revisited when we have multiple chains
            lock (_pendingBlocks)
            {
                if (_pendingBlocks.Count > 0)
                {
                    var chains = Nexus.GetChains(Nexus.RootStorage).Select(x => Nexus.GetChainByName(x));

                    foreach (var chain in chains)
                    {
                        HandlePendingBlocks(chain);
                    }
                }
                else
                {
                    UpdateRequests();
                }
            }

            return true;
        }

        private void UpdateRequests()
        {
            var currentTime = DateTime.UtcNow;
            var diff = currentTime - _lastRequestTime;
            if (diff.TotalSeconds > 10)
            {
                lock (_peers)
                {
                    if (_peers.Count > 0)
                    {
                        var peerList = _peers.Keys.ToArray();
                        var randomIndex = (int)(currentTime.Ticks % peerList.Length);
                        var peerKey = peerList[randomIndex];
                        var peer = _peers[peerKey];
                        _lastRequestTime = currentTime;

                        var request = new RequestMessage(this.Address, this.PublicEndpoint, RequestKind.Chains, Nexus.Name);
                        SendMessage(peer, request);
                    }
                }
            }
        }

        protected override void OnStart()
        {
            if (this.Capabilities.HasFlag(PeerCaps.Sync))
            {
                Logger.Message($"Phantasma node listening on port {Port}, using address: {Address}");

                listener.Start();
            }
            else
            {
                Logger.Warning($"Since sync not enabled, this node won't accept connections from other nodes.");
            }
        }

        protected override void OnStop()
        {
            listener.Stop();
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
                for (int i = 0; i < _knownEndpoints.Count; i++)
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

                    var client = new TcpClient();
                    var result = client.BeginConnect(target.endpoint.Host, target.endpoint.Port, null, null);

                    var signal = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));

                    if (signal && client.Client != null && client.Client.Connected)
                    {
                        Logger.Debug("Connected to peer: " + target.endpoint);
                        target.status = EndpointStatus.Connected;

                        client.EndConnect(result);
                        Task.Run(() => { HandleConnection(client.Client); });
                        return;
                    }
                    else
                    {
                        Logger.Debug("Could not reach peer: " + target.endpoint);
                        target.status = EndpointStatus.Disabled;
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
            catch
            {
                return;
            }

            Logger.Debug("New connection accepted from " + socket.RemoteEndPoint.ToString());
            Task.Run(() => { HandleConnection(socket); });
        }

        private bool SendMessage(Peer peer, Message msg)
        {
            Throw.IfNull(peer, nameof(peer));
            Throw.IfNull(msg, nameof(msg));

            Logger.Debug("Sending " + msg.GetType().Name + " to  " + peer.Endpoint);

            msg.Sign(this.Keys);

            try
            {
                peer.Send(msg);
            }
            catch
            {
                return false;
            }

            return true;
        }

        private void HandleConnection(Socket socket)
        {
            var peer = new TCPPeer(socket);

            // this initial message is not only used to fetch chains but also to verify identity of peers
            var requestKind = RequestKind.Chains | RequestKind.Peers;
            if (Capabilities.HasFlag(PeerCaps.Mempool))
            {
                requestKind |= RequestKind.Mempool;
            }

            var request = new RequestMessage(this.Address, this.PublicEndpoint, requestKind, Nexus.Name);
            var active = SendMessage(peer, request);

            string ip = ((IPEndPoint)(socket.RemoteEndPoint)).Address.ToString();
            Logger.Debug($"Incoming connection from " + ip);

            while (active)
            {
                var msg = peer.Receive();
                if (msg == null)
                {
                    break;
                }

                Logger.Debug($"Got {msg.GetType().Name} from: {msg.Address.Text}");
                foreach (var line in msg.GetDescription())
                {
                    Logger.Debug(line);
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

            Logger.Debug("Disconnected from peer: " + peer.Endpoint);
            lock (_peers)
            {
                var entry = new EndpointEntry(peer.Endpoint);
                _knownEndpoints.Remove(entry);
                Logger.Debug("removed endpoint: " + entry.endpoint);

                var peerKey = peer.Endpoint.ToString();
                if (_peers.ContainsKey(peerKey))
                {
                    _peers.Remove(peerKey);
                    Logger.Message("Removed peer: " + peerKey);
                }
            }

            socket.Close();
        }

        // will return true if theres no more blocks 
        private void HandlePendingBlocks(Chain chain)
        {
            var start = chain.Height + 1;
            var last = start;
            int count = 0;

            do
            {
                var nextHeight = chain.Height + 1;
                var nextKey = $"{chain.Name}.{nextHeight}";
                if (_pendingBlocks.ContainsKey(nextKey))
                {
                    var entry = _pendingBlocks[nextKey];
                    if (!HandleBlock(chain, entry.block, entry.transactions))
                    {
                        throw new NodeException($"Something went wrong when adding block {entry.block.Height} to {chain.Name} chain");
                    }

                    _pendingBlocks.Remove(nextKey);
                    last = nextHeight;
                    count++;
                }
                else
                {
                    _pendingBlocks.Clear();
                    break;
                }

            } while (true);

            if (count > 0)
            {
                BigInteger expectedHeight = 0;
                lock (_knownHeights)
                {
                    if (_knownHeights.TryGetValue(chain.Name, out expectedHeight))
                    {
                        if (last <= expectedHeight)
                        {
                            int percent = (int)((last * 100) / expectedHeight);
                            if (start == last)
                            {
                                Logger.Message($"{this.Version}: Added block #{start} to {chain.Name} ...{percent}%");
                            }
                            else
                            {
                                Logger.Message($"{this.Version}: Added blocks #{start} to #{last} to {chain.Name} ...{percent}%");
                            }

                            if (expectedHeight == chain.Height)
                            {
                                IsFullySynced = true; // TODO when sidechains are avaible this should be reviewed
                            }
                        }
                        else
                        {
                            Logger.Message($"{this.Version}: Added block #{start} to {chain.Name}");
                            IsFullySynced = true; // TODO when sidechains are avaible this should be reviewed
                        }
                    }
                }

                if (expectedHeight == 0) 
                {
                    Logger.Message($"{this.Version}: Added block #{start} to {chain.Name}");
                    IsFullySynced = true; // TODO when sidechains are avaible this should be reviewed
                }
            }
        }

        private bool HandleBlock(Chain chain, Block block, IList<Transaction> transactions)
        {
            if (block.Height != chain.Height + 1)
            {
                throw new NodeException("unexpected block height");
            }

            try
            {
                var oracle = new BlockOracleReader(Nexus, block);
                Transaction inflationTx;
                var changeSet = chain.ProcessTransactions(block, transactions, oracle, 1, out inflationTx, null); // null, because we don't want to modify the block

                chain.AddBlock(block, transactions, 1, changeSet);
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString());
                throw new NodeException($"Failed to add block {block.Height} to {chain.Name} chain");
            }

            return true;

        }

        private Message HandleMessage(Peer peer, Message msg)
        {
            if (msg.IsSigned && !msg.Address.IsNull)
            {
                if (msg.Address.IsUser)
                {
                    peer.SetAddress(msg.Address);
                }
                else
                {
                    return new ErrorMessage(Address, this.PublicEndpoint, P2PError.InvalidAddress);
                }
            }
            else
            {
                return new ErrorMessage(Address, this.PublicEndpoint, P2PError.MessageShouldBeSigned);
            }

            Endpoint endpoint;
            try
            {
                endpoint = Endpoint.FromString(msg.Host);
            }
            catch (ChainException e)
            {
                return new ErrorMessage(Address, this.PublicEndpoint, P2PError.InvalidEndpoint);
            }

            var peerKey = endpoint.ToString();

            lock (_peers)
            {
                if (!_peers.ContainsKey(peerKey))
                {
                    Logger.Message("Added peer: " + peerKey);
                    peer.UpdateEndpoint(endpoint);
                    _peers[peerKey] = peer;
                }
            }

            Logger.Debug($"Got {msg.Opcode} message from {peerKey}");

            switch (msg.Opcode)
            {
                case Opcode.EVENT:
                    {
                        var evtMessage = (EventMessage)msg;
                        var evt = evtMessage.Event;
                        Logger.Message("New event: " + evt.ToString());
                        return null;
                    }

                case Opcode.REQUEST:
                    {
                        var request = (RequestMessage)msg;

                        if (request.NexusName != Nexus.Name)
                        {
                            return new ErrorMessage(Address, this.PublicEndpoint, P2PError.InvalidNexus);
                        }

                        if (request.Kind == RequestKind.None)
                        {
                            return null;
                        }

                        var answer = new ListMessage(this.Address, this.PublicEndpoint, request.Kind);

                        if (request.Kind.HasFlag(RequestKind.Peers))
                        {
                            answer.SetPeers(this.Peers.Where(x => x != peer).Select(x => x.Endpoint.ToString()));
                        }

                        if (request.Kind.HasFlag(RequestKind.Chains))
                        {
                            var chainList = Nexus.GetChains(Nexus.RootStorage);
                            var chains = chainList.Select(x => Nexus.GetChainByName(x)).Select(x => new ChainInfo(x.Name, Nexus.GetParentChainByName(x.Name), x.Height)).ToArray();
                            answer.SetChains(chains);
                        }

                        if (request.Kind.HasFlag(RequestKind.Mempool) && Capabilities.HasFlag(PeerCaps.Mempool))
                        {
                            var txs = _mempool.GetTransactions().Select(x => Base16.Encode(x.ToByteArray(true)));
                            answer.SetMempool(txs);
                        }

                        if (request.Kind.HasFlag(RequestKind.Blocks))
                        {
                            foreach (var entry in request.Blocks)
                            {
                                var chain = this.Nexus.GetChainByName(entry.Key);
                                if (chain == null)
                                {
                                    continue;
                                }

                                answer.AddBlockRange(chain, entry.Value);
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
                            IEnumerable<string> newPeers;

                            lock (_peers)
                            {
                                newPeers = listMsg.Peers.Where(x => !_peers.ContainsKey(x));
                            }

                            foreach (var entry in listMsg.Peers)
                            {
                                Logger.Message("New peer: " + entry.ToString());
                            }
                            QueueEndpoints(newPeers);
                        }

                        var blockFetches = new Dictionary<string, RequestRange>();
                        if (listMsg.Kind.HasFlag(RequestKind.Chains))
                        {
                            foreach (var entry in listMsg.Chains)
                            {
                                var chain = Nexus.GetChainByName(entry.name);
                                // NOTE if we dont find this chain then it is too soon for ask for blocks from that chain
                                if (chain != null)
                                {
                                    if (chain.Height < entry.height)
                                    {
                                        var start = chain.Height + 1;
                                        var end = entry.height;
                                        var limit = start + ListMessage.MaxBlocks - 1;

                                        if (end > limit)
                                        {
                                            end = limit;
                                        }

                                        blockFetches[entry.name] = new RequestRange(start, end);

                                        lock (_knownHeights)
                                        {
                                            BigInteger lastKnowHeight = _knownHeights.ContainsKey(chain.Name) ? _knownHeights[chain.Name] : 0;
                                            if (entry.height > lastKnowHeight)
                                            {
                                                _knownHeights[chain.Name] = entry.height;
                                                IsFullySynced = false;
                                            }
                                        }
                                    }

                                    if (chain.Height == entry.height)
                                    {
                                        IsFullySynced = true;
                                    }
                                }
                            }
                        }

                        if (listMsg.Kind.HasFlag(RequestKind.Mempool) && Capabilities.HasFlag(PeerCaps.Mempool))
                        {
                            int submittedCount = 0;
                            foreach (var txStr in listMsg.Mempool)
                            {
                                var bytes = Base16.Decode(txStr);
                                var tx = Transaction.Unserialize(bytes);
                                try
                                {
                                    _mempool.Submit(tx);
                                    submittedCount++;
                                }
                                catch
                                {
                                }

                                Logger.Message(submittedCount + " new transactions");
                            }
                        }

                        if (listMsg.Kind.HasFlag(RequestKind.Blocks))
                        {
                            Chain chain = null;
                            foreach (var entry in listMsg.Blocks)
                            {
                                chain = Nexus.GetChainByName(entry.Key);
                                if (chain == null)
                                {
                                    continue;
                                }

                                var blockRange = entry.Value;
                                foreach (var block in blockRange.blocks)
                                {
                                    var transactions = new List<Transaction>();
                                    foreach (var txHash in block.TransactionHashes)
                                    {
                                        var tx = entry.Value.transactions[txHash];
                                        transactions.Add(tx);
                                    }

                                    var maxPendingHeightExpected = chain.Height + ListMessage.MaxBlocks;

                                    if (block.Height > chain.Height && block.Height <= maxPendingHeightExpected)
                                    {
                                        var key = $"{chain.Name}.{block.Height}";
                                        lock (_pendingBlocks)
                                        {
                                            _pendingBlocks[key] = new PendingBlock(chain.Name, block, transactions);
                                        }
                                    }
                                }

                                _lastRequestTime = DateTime.UtcNow;
                                //Thread.Sleep(10000);
                            }
                        }

                        if (blockFetches.Count > 0)
                        {
                            outKind |= RequestKind.Blocks;
                        }

                        if (outKind != RequestKind.None)
                        {
                            var answer = new RequestMessage(this.Address, this.PublicEndpoint, outKind, Nexus.Name);

                            if (blockFetches.Count > 0)
                            {
                                answer.SetBlocks(blockFetches);
                            }

                            return answer;
                        }

                        return null;
                    }

                case Opcode.MEMPOOL_Add:
                    {
                        if (Capabilities.HasFlag(PeerCaps.Mempool))
                        {
                            var memtx = (MempoolAddMessage)msg;
                            int submissionCount = 0;
                            foreach (var tx in memtx.Transactions)
                            {
                                try
                                {
                                    if (_mempool.Submit(tx))
                                    {
                                        submissionCount++;
                                    }
                                }
                                catch
                                {
                                    // ignore
                                }
                            }

                            Logger.Message($"Added {submissionCount} txs to the mempool");
                        }

                        return null;
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

                        return null;
                    }
            }

            throw new NodeException("No answer sent to request " + msg.Opcode);
        }

        private Dictionary<Address, List<RelayReceipt>> _messages = new Dictionary<Address, List<RelayReceipt>>();

        public IEnumerable<RelayReceipt> GetRelayReceipts(Address from)
        {
            if (_messages.ContainsKey(from))
            {
                return _messages[from];
            }

            return Enumerable.Empty<RelayReceipt>();
        }

        public void PostRelayMessage(RelayReceipt receipt)
        {
            List<RelayReceipt> list;

            var msg = receipt.message;

            if (_messages.ContainsKey(msg.receiver))
            {
                list = _messages[msg.receiver];
            }
            else
            {
                list = new List<RelayReceipt>();
                _messages[msg.receiver] = list;
            }

            BigInteger expectedMessageIndex = 0;

            foreach (var otherReceipt in list)
            {
                var temp = otherReceipt.message;
                if (temp.sender == msg.sender && temp.index >= expectedMessageIndex)
                {
                    expectedMessageIndex = temp.index + 1;
                }
            }

            if (expectedMessageIndex > msg.index)
            {
                throw new RelayException("unexpected message index, should be at least " + expectedMessageIndex + " but it's " + msg.index);
            }

            list.Add(receipt);
        }

        internal void AddBlock(Chain chain, Block block)
        {
            if (!Capabilities.HasFlag(PeerCaps.Sync))
            {
                return;
            }

            foreach (var peer in _peers.Values)
            {
                var msg = new ListMessage(this.Keys.Address, this.PublicEndpoint, RequestKind.Blocks);
                msg.AddBlockRange(chain, block.Height, 1);

                SendMessage(peer, msg);
            }
        }

        internal void AddEvent(Event evt)
        {
            if (!Capabilities.HasFlag(PeerCaps.Events))
            {
                return;
            }

            Cache<Event> cache;

            if (_events.ContainsKey(evt.Address))
            {
                cache = _events[evt.Address];
            }
            else
            {
                cache = new Cache<Event>(250, TimeSpan.FromMinutes(60)); // TODO make this configurable
                _events[evt.Address] = cache;
            }

            cache.Add(evt);

            foreach (var peer in _peers.Values)
            {
                if (peer.Address == evt.Address)
                {
                    var msg = new EventMessage(evt.Address, this.PublicEndpoint, evt);
                    SendMessage(peer, msg);
                }
            }
        }

        public IEnumerable<Event> GetEvents(Address address)
        {
            if (Capabilities.HasFlag(PeerCaps.Events))
            {
                if (_events.ContainsKey(address))
                {
                    return _events[address].Items;
                }
                else
                {
                    return Enumerable.Empty<Event>();
                }
            }
            else
            {
                return Enumerable.Empty<Event>();
            }
        }
    }
}
