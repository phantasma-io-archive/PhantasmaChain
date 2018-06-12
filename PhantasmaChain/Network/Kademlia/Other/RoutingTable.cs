using System.Collections.Generic;
using System.Linq;
using ArgumentValidator;
using Phantasma.Utils;

namespace Phantasma.Network.Kademlia
{
    public class RoutingTable 
    {
        private IList<NodeInfo> nodes;

        private readonly Sha256HashGenerator hashGenerator;

        public RoutingTable(Sha256HashGenerator hashGenerator)
            : this(hashGenerator, new List<NodeInfo>())
        {
        }

        public RoutingTable(Sha256HashGenerator hashGenerator, IList<NodeInfo> nodes)
        {
            Throw.IfNull(nodes, nameof(nodes));
            Throw.IfNull(hashGenerator, nameof(hashGenerator));

            this.nodes = nodes;
            this.hashGenerator = hashGenerator;
        }

        private IList<NodeInfo> SortedNodes
        {
            get
            {
                return this.Nodes.OrderBy(node => node.NodeId).ToList();
            }
        }

        public IList<NodeInfo> Nodes
        {
            get
            {
                return this.nodes;
            }
            set
            {
                this.nodes = value;
            }
        }

        public NodeInfo FindNode(string key)
        {
            NodeInfo partitionNode;

            // Hash the key to get the "partition" key
            var partitionKey = this.hashGenerator.Hash(key);

            Log.Message("RoutingTable - FindNode: key = " + key);
            Log.Message("RoutingTable - FindNode: partitionKey = " + partitionKey);

            // Now find the last node which has an id smaller than the
            // partition key. We also make sure that the nodes are sorted,
            // this might be overkill but unless we have millions of nodes
            // it should be OK performance wise.
            partitionNode = this.SortedNodes.LastOrDefault(n => n.NodeId <= partitionKey);

            // If we haven't found any node, we'll give the load to the last node.
            // If the node ids aren't evenly distributed we could be in trouble
            // since the last node might get overloaded.
            if (partitionNode == null)
            {
                Log.Message("RoutingTable - FindNode: Didn't find node, reverting to last");
                partitionNode = this.SortedNodes.Last();
            }

            Log.Message("RoutingTable - FindNode: FoundNode = " + partitionNode.NodeId);

            return partitionNode;
        }
    }
}
