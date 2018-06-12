using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Phantasma.Network.Kademlia
{
    public class RemotePeer : Peer
    {
        public readonly string Host;
        public readonly int Port;

        public RemotePeer(string host, int port)
        {
            this.Host = host;

            new Thread(ReceiveMessages).Start();
        }

        public override KeyValueMessage GetValue(KeyMessage request)
        {
            throw new System.NotImplementedException();
        }

        public override KeyMessage RemoveValue(KeyMessage request)
        {
            throw new System.NotImplementedException();
        }

        public override KeyValueMessage StoreValue(KeyValueMessage request)
        {
            throw new System.NotImplementedException();
        }

        private void ReceiveMessages()
        {
            var socket = new UdpClient(this.Port);

            while (true)
            {
                var remoteEP = new IPEndPoint(IPAddress.Any, this.Port);
                var data = socket.Receive(ref remoteEP); // listen on port 

                socket.Send(new byte[] { 1 }, 1, remoteEP); // reply back
            }
        }
    }
}
