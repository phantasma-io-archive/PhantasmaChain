using System.Collections.Generic;

using Newtonsoft.Json;

using Phantasma.Kademlia.Common;

namespace Phantasma.Kademlia
{
    public class VirtualProtocol : IProtocol
    {
        [JsonIgnore]
        public Node Node { get; set; }
        [JsonIgnore]
        public bool Responds { get; set; }

        /// <summary>
        /// For serialization.
        /// </summary>
        public VirtualProtocol()
        {
        }

        /// <summary>
        /// For unit testing with deferred node setup.
        /// </summary>
        public VirtualProtocol(bool responds = true)
        {
            Responds = responds;
        }

        /// <summary>
        /// Register the in-memory node with our virtual protocol.
        /// </summary>
        public VirtualProtocol(Node node, bool responds = true)
        {
            Node = node;
            Responds = responds;
        }

        public RpcError Ping(Contact sender)
        {
            // Ping still adds/updates the sender's contact.
            if (Responds)
            {
                Node.Ping(sender);
            }

            return new RpcError() { TimeoutError = !Responds };
        }

        /// <summary>
        /// Get the list of contacts for this node closest to the key.
        /// </summary>
        public (List<Contact> contacts, RpcError error) FindNode(Contact sender, ID key)
        {
            return (Node.FindNode(sender, key).contacts, NoError());
        }

        /// <summary>
        /// Returns either contacts or null if the value is found.
        /// </summary>
        public (List<Contact> contacts, string val, RpcError error) FindValue(Contact sender, ID key)
        {
            var (contacts, val) = Node.FindValue(sender, key);

            return (contacts, val, NoError());
        }

        /// <summary>
        /// Stores the key-value on the remote peer.
        /// </summary>
        public RpcError Store(Contact sender, ID key, string val, bool isCached = false, int expTimeSec = 0)
        {
            Node.Store(sender, key, val, isCached, expTimeSec);

            return NoError();
        }

        protected RpcError NoError()
        {
            return new RpcError();
        }
    }
}
