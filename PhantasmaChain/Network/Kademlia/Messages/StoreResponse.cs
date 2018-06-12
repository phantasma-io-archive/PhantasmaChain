using System;

namespace Phantasma.Network.Kademlia.Messages
{
	/// <summary>
	/// A reply to a store query.
	/// </summary>
	public class StoreResponse : Response
	{
		private bool sendData;
		
        /// <summary>
        /// Constructor of the class
        /// </summary>
        /// <param name="nodeID">Identificator of the sender</param>
        /// <param name="query">StoreQuery originating this message</param>
        /// <param name="accept">Param that indicates if the node has accepted the request to store</param>
        /// <param name="nodeEndpoint">Address of the sender node</param>
		public StoreResponse(ID nodeID, StoreQuery query, bool accept, Endpoint nodeEndpoint) : base(nodeID, query, nodeEndpoint)
		{
			sendData = accept;
		}
		
		/// <summary>
		/// Indicator to verify if it is necessary or not to send data
		/// </summary>
		public bool ShouldSendData
		{
            get { return sendData; }
            set { this.sendData = value; }
		}
		
        /// <summary>
        /// Default name of the message
        /// </summary>
		public override string Name
		{
            get { return "STORE_RESPONSE"; }
            set { }
		}
	}
}
