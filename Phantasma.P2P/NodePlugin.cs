using Phantasma.Blockchain;

namespace Phantasma.Network.P2P
{
    internal class NodePlugin : IChainPlugin
    {
        private Node Node;

        public NodePlugin(Node node)
        {
            this.Node = node;
        }

        // this plugin is responsible for catching any event occuring in the chain and saving them
        public override void OnTransaction(Chain chain, Block block, Transaction transaction)
        {
            var events = block.GetEventsForTransaction(transaction.Hash);
            foreach (var evt in events)
            {
                Node.AddEvent(evt);
            }
        }
    }
}