using System;
using System.Collections.Generic;

namespace Phantasma.Network.Kademlia.Messages
{
	/// <summary>
	/// Send data in reply to a FindVAlue
	/// </summary>
	public class FindValueDataResponse : Response
	{
		private KademliaResource resource;
		
		/// <summary>
		/// Make a new response.
		/// </summary>
		/// <param name="nodeID">The identificator of the sender</param>
		/// <param name="request">The FindValue request generating this response</param>
		/// <param name="data">The list of KademliaResources found</param>
        /// <param name="nodeEndpoint">The address of the sender</param>
		public FindValueDataResponse(ID nodeID, FindValue request, KademliaResource data, Endpoint nodeEndpoint) : base(nodeID, request, nodeEndpoint)
		{
			resource = data;
		}
		
		/// <summary>
		/// the values returned for the key
		/// </summary>
		public KademliaResource Value
		{
            get { return resource; }
            set { this.resource = value; }
		}
		
        /// <summary>
        /// The default name of the message
        /// </summary>
		public override string Name
		{
            get { return "FIND_VALUE_RESPONSE_DATA"; }
            set { }
		}
	}
}
