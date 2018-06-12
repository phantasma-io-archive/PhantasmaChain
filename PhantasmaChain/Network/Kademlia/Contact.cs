using System;

namespace Phantasma.Network.Kademlia
{
	/// <summary>
	/// Represents the information needed to contact another node.
	/// </summary>
	[Serializable]
	public class Contact
	{
		private ID nodeID;
        private Endpoint nodeEndpoint;
		
		/// <summary>
		/// Make a contact for a node with the given ID at the given location.
		/// </summary>
		/// <param name="id">The identificator of the node</param>
		/// <param name="endpoint">The address of the node</param>
		public Contact(ID id, Endpoint endpoint)
		{
			nodeID = id;
			nodeEndpoint = endpoint;
		}
		
		/// <summary>
		/// Get the node's ID.
		/// </summary>
		/// <returns>The node ID</returns>
		public ID NodeID {
            get { return nodeID; }
		}
		
		/// <summary>
		/// Get the node's endpoint.
		/// </summary>
		/// <returns>The address</returns>
		public Endpoint NodeEndPoint
        {
            get { return nodeEndpoint; }
		}
		
        /// <summary>
        /// Method used to obtain a string representation of the class
        /// </summary>
        /// <returns>A string representing the object</returns>
		public override string ToString()
		{
			return NodeID.ToString() + "@" + NodeEndPoint.ToString();
		}
	}
}
