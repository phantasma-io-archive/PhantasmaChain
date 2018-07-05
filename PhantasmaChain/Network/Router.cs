using Phantasma.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Phantasma.Network
{
    public class Router
    {
        private List<Peer> _peers = new List<Peer>();

        public IEnumerable<Peer> Peers => _peers;

        private TcpListener listener;

        private ConcurrentQueue<DeliveredMessage> _queue;


        public Router(IEnumerable<Endpoint> seeds, int port, ConcurrentQueue<DeliveredMessage> queue) {
            this._queue = queue;

            Console.WriteLine("Starting TCP listener...");

            listener = new TcpListener(IPAddress.Any, port);

            listener.Start();

            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                while (true)
                {
                    Socket client = listener.AcceptSocket();
                    Console.WriteLine("Connection accepted.");

                    new Thread(() =>
                    {
                        HandleConnection(client);
                    }).Start();
                }

                listener.Stop();
            }).Start();
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
            //byte[] data = new byte[100];
            //int size = client.Receive(data);
            throw new NotImplementedException();
        }
    }
}
