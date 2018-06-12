using System;

namespace Phantasma.Network.Kademlia.Messages
{
	/// <summary>
	/// A message asking if another node will store data for us, and if we need to send the data.
	/// Maybe they already have it.
	/// We have to send the timestamp with it for people to republish stuff.
	/// </summary>
	public class StoreQuery : Message
	{
		private ID key;
		private DateTime publication;
		
		/// <summary>
		/// Make a new STORE_QUERY message.
		/// </summary>
		/// <param name="nodeID">The identificator of the sender</param>
		/// <param name="hash">A hash of the data value</param>
		/// <param name="originalPublication">The time of publication</param>
        /// <param name="nodeEndpoint">The address of the sender</param>
		public StoreQuery(ID nodeID, ID hash, DateTime originalPublication, Endpoint nodeEndpoint) : base(nodeID, nodeEndpoint)
		{
			key = hash;
			publication = originalPublication;
		}
		
		/// <summary>
		/// The hash of the data value we're asking about.
		/// </summary>
		public ID Key
		{
            get { return key; }
            set { this.key = value; }
		}

		/// <summary>
		/// the data was originally published, in UTC.
		/// </summary>
		/// <returns></returns>
		public DateTime PublicationTime
		{
            get { return publication.ToUniversalTime(); }
            set { this.publication = value; }
		}
		
        /// <summary>
        /// The default name of the message
        /// </summary>
		public override string Name
		{
            get { return "STORE_QUERY"; }
            set { }
		}
	}
}
