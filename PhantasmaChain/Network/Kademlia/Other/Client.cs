using ArgumentValidator;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Phantasma.Network.Kademlia
{
    public class Client
    {
        private readonly NodeInfo nodeInfo;
        private readonly RoutingTable routingTable;
        private readonly Peer localClient;

        public Client(NodeInfo nodeInfo, RoutingTable routingTable)
        {
            Throw.IfNull(nodeInfo, nameof(nodeInfo));
            Throw.IfNull(routingTable, nameof(routingTable));

            this.routingTable = routingTable;
            this.nodeInfo = nodeInfo;

            this.localClient = CreateLocalClient();
        }

        public Peer CreateLocalClient()
        {
            var store = new NodeStore();
            var client = new LocalPeer(store);
            return client;
        }

        public Peer CreateRemoteClient(NodeInfo nodeInfo)
        {
            var client = new RemotePeer(nodeInfo.HostName, nodeInfo.Port);

            return client;
        }

        public Task<KeyValueMessage> GetValue(KeyMessage request)
        {
            var client = this.GetClient(request.Key);
            var response = client.GetValue(request);

            return Task.FromResult(response);
        }

        public Task<KeyMessage> RemoveValue(KeyMessage request)
        {
            var client = this.GetClient(request.Key);
            var response = client.RemoveValue(request);

            return Task.FromResult(response);
        }

        public Task<KeyValueMessage> StoreValue(KeyValueMessage request)
        {
            var client = this.GetClient(request.Key);
            var response = client.StoreValue(request);

            return Task.FromResult(response);
        }

        private Peer GetClient(string key)
        {
            // Find the node which should have this key
            var remoteNode = this.routingTable.FindNode(key);

            // Return true if it's the local node
            var isLocal = remoteNode.NodeId == this.nodeInfo.NodeId;

            if (isLocal)
            {
                return this.localClient;
            }

            return this.CreateRemoteClient(remoteNode);
        }
    }
}
