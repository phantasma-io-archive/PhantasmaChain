using System;

namespace Phantasma.Network.Kademlia
{
    /// <summary>
    /// Stores info about a node in the DHT network
    /// </summary>
    public class NodeInfo
    {
        /// <summary>
        /// 32 bit node id
        /// </summary>
        /// <remarks>
        /// Being explicit about the 32 bit declaration
        /// since it's crucial to how routing will work between DHT nodes.
        /// </remarks>
        public UInt32 NodeId { get; set; }

        /// <summary>
        /// Host name (or IP)
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        /// Port running on
        /// </summary>
        public int Port { get; set; }
    }
}
