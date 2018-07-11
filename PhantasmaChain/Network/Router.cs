using Phantasma.Core;
using Phantasma.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Phantasma.Network
{
    public class Router: Runnable
    {
        private List<Peer> _peers = new List<Peer>();

        public IEnumerable<Peer> Peers => _peers;

        private TcpListener listener;

        private ConcurrentQueue<DeliveredMessage> _queue;


        public Router(IEnumerable<Endpoint> seeds, int port, ConcurrentQueue<DeliveredMessage> queue) {
            this._queue = queue;

            listener = new TcpListener(IPAddress.Any, port);
        }

        protected override void OnStart()
        {
            Logger.Message("Starting TCP listener...");

            listener.Start();
        }

        protected override void OnStop()
        {
            listener.Stop();
        }

        protected override bool Run()
        {
            Socket client = listener.AcceptSocket();
            Logger.Message("New connection accepted.");

            new Thread(() =>
            {
                HandleConnection(client);
            }).Start();

            return true;
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
