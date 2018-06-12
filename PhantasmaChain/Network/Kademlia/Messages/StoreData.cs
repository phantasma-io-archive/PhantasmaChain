using System;

namespace Phantasma.Network.Kademlia.Messages
{
	/// <summary>
	/// Send along the data in response to an affirmative StoreResponse.
	/// </summary>
	public class StoreData : Response
	{
		private byte[] data;
		private DateTime publication;
		
		/// <summary>
		/// Make a mesage to store the given data.
		/// </summary>
		/// <param name="nodeID">The sender identificator</param>
		/// <param name="request">The StoreResponse message that originated this message</param>
		/// <param name="theData">The CompleteTag to store</param>
		/// <param name="originalPublication">The publication datetime</param>
        /// <param name="nodeEndpoint">The sender node's kademlia address</param>
        /// <param name="transportUri">The sender node's transport uri</param>
		public StoreData(ID nodeID, StoreResponse request, byte[] theData, DateTime originalPublication, Endpoint nodeEndpoint) : base(nodeID, request, nodeEndpoint)
		{
			this.data = theData;
			this.publication = originalPublication;
		}
		
		/// <summary>
		/// The data to store.
		/// </summary>
		public byte[] Data
		{
            get { return data; }
            set { this.data = value; }
		}
		
		/// <summary>
		/// When the data was originally published, in UTC.
		/// </summary>
		public DateTime PublicationTime
		{
            get { return publication.ToUniversalTime(); }
            set { this.publication = value; }
		}

        /// <summary>
        /// Default name of the message
        /// </summary>
		public override string Name
		{
            get { return "STORE_DATA"; }
            set { }
		}
	}
}
