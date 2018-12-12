using Phantasma.Blockchain;
using Phantasma.Core;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Phantasma.Network.P2P
{
    public class TCPPeer : Peer
    {
        private readonly Socket _socket;

        public TCPPeer(Nexus nexus, Socket socket) : base(nexus, MakeEndpoint(socket))
        {
            this._socket = socket;
            this.Status = Status.Anonymous;
        }

        private static Endpoint MakeEndpoint(Socket socket)
        {
            var remoteIpEndPoint = socket.RemoteEndPoint as IPEndPoint;
            return new Endpoint(PeerProtocol.TCP, remoteIpEndPoint.Address.ToString(), remoteIpEndPoint.Port);
        }

        public override Message Receive()
        {
            try
            {
                using (var stream = new NetworkStream(_socket))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        var msg = Message.Unserialize(reader);
                        return msg;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        public override void Send(Message msg)
        {
            Throw.IfNull(msg, nameof(msg));

            var bytes = msg.ToByteArray(true);
            int size = bytes.Length;
            int offset = 0;
            while (size > 0)
            {
                var bytesSent = _socket.Send(bytes, offset, size, SocketFlags.None);

                offset += bytesSent;
                size -= bytesSent;
            }
        }
    }
}
