using System;

namespace Phantasma.Network.Kademlia.Messages
{
	/// <summary>
	/// A message used to search for a node.
	/// </summary>
	public class FindNode : Message
	{
		private ID target;
		
		/// <summary>
		/// Make a new FIND_NODE message
		/// </summary>
		/// <param name="nodeID">the identificator of the sender</param>
		/// <param name="toFind">The ID of the node searched</param>
        /// <param name="nodeEndpoint">The address of the sender</param>
		public FindNode(ID nodeID, ID toFind, Endpoint nodeEndpoint) : base(nodeID, nodeEndpoint)
		{
			target = toFind;
		}
		
		/// <summary>
		/// Get/Set the target of this message.
		/// </summary>
		public ID Target
		{
            get { return target; }
            set { this.target = value; }
		}
		
        /// <summary>
        /// Default Name of the message
        /// </summary>
		public override string Name
		{
            get { return "FIND_NODE"; }
            set { }
		}
	}
}
