using System;

namespace Phantasma.Network.Kademlia.Messages
{
	/// <summary>
	/// Represents a response message, in the same conversation as an original message.
	/// </summary>
	public abstract class Response : Message
	{
		/// <summary>
		/// Make a reply in the same conversation as the given message.
		/// </summary>
		/// <param name="nodeID">The sender node identificator</param>
		/// <param name="respondingTo">The message the node is responding to</param>
        /// <param name="nodeEndpoint">The address of the sender</param>
		public Response(ID nodeID, Message respondingTo, Endpoint nodeEndpoint) : base(nodeID, respondingTo.ConversationID, nodeEndpoint)
		{
		}
	}
}
