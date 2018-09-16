using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

using Phantasma.Cryptography;
using Phantasma.Network.P2P.Messages;
using Phantasma.Core;
using Phantasma.Core.Log;

namespace Phantasma.Network.P2P
{
    public class Router: Runnable
    {
        private List<Peer> _peers = new List<Peer>();

        public IEnumerable<Peer> Peers => _peers;

        private TcpClient client;
        private TcpListener listener;

        private ConcurrentQueue<DeliveredMessage> _queue;

        public readonly Logger Log;

        public readonly int Port;

        private List<Socket> _connections = new List<Socket>();
        private List<Endpoint> _activeSeeds = new List<Endpoint>();
        private List<Endpoint> _disabledSeeds = new List<Endpoint>();

        private bool listening = false;

        public Router(IEnumerable<Endpoint> seeds, int port, ConcurrentQueue<DeliveredMessage> queue, Logger log) {
            this.Port = port;
            this.Log = Logger.Init(log);
            this._queue = queue;
            this._activeSeeds = seeds.ToList();

            listener = new TcpListener(IPAddress.Any, port);
            client = new TcpClient();
        }

        protected override void OnStart()
        {
            Log.Message($"Starting TCP listener on {Port}...");

            listener.Start();
        }

        protected override void OnStop()
        {
            listener.Stop();
        }

        protected override bool Run()
        {
            if (_connections.Count == 0 && _activeSeeds.Count > 0) {
                var idx = Environment.TickCount % _activeSeeds.Count;
                var target = _activeSeeds[idx];

                var result = client.BeginConnect(target.Host, target.Port, null, null);

                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));

                if (!success)
                {
                    Log.Message("Could not reach seed " + target);
                    _disabledSeeds.Add(target);
                    _activeSeeds.RemoveAt(idx);
                }
                else {
                    Log.Message("Connected to seed " + target);
                    client.EndConnect(result);

                    InitConnection(client.Client);
                    return true;
                }
            }

            if (!listening) {
                Log.Debug("Waiting for new connections");
                listening = true;
                var accept = listener.BeginAcceptSocket(new AsyncCallback(DoAcceptSocketCallback), listener);
            }

            return true;
        }

        // Process the client connection.
        public void DoAcceptSocketCallback(IAsyncResult ar)
        {
            listening = false;

            // Get the listener that handles the client request.
            TcpListener listener = (TcpListener)ar.AsyncState;

            Socket socket = listener.EndAcceptSocket(ar);
            Log.Message("New connection accepted from "+socket.RemoteEndPoint.ToString());

            InitConnection(socket);
        }

        public void InitConnection(Socket socket) {
            _connections.Add(socket);

            var msg = new PeerJoinMessage(Address.Null);
//            msg.Serialize()
        }

        private void HandleConnection(Socket client) {
            var endpoint = GetEndpoint(client);

            // TODO  add peer to list

            while (true) {
                var msg = GetMessage(client);
                if (msg == null) {
                    break;
                }

                var src = new DeliveredMessage() { message = msg, source = endpoint };
                _queue.Enqueue(src);
            }

            client.Close();

            // TODO remove peer from list
        }

        private Endpoint GetEndpoint(Socket client)
        {
            throw new NotImplementedException();
        }

        private Message GetMessage(Socket client)
        {
            try {
                using (var stream = new NetworkStream(client))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        var msg = Message.Unserialize(reader);
                        return msg;
                    }
                }
            }
            catch {
                return null;
            }

        }
    }
}
