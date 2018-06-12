namespace Phantasma.Network.Kademlia.Messages
{
	/// <summary>
	/// Represents a request to get a value.
	/// Receiver should either send key or a node list.
	/// </summary>
	public class FindValue : Message
	{
		private ID key;
		
		/// <summary>
		/// Make a new FindValue message.
		/// </summary>
		/// <param name="nodeID">The sender identificator</param>
		/// <param name="wantedKey">The desired key by the sender</param>
        /// <param name="nodeEndpoint">The address of the sender</param>
		public FindValue(ID nodeID, ID wantedKey, Endpoint nodeEndpoint) : base(nodeID, nodeEndpoint)
		{
			this.key = wantedKey;
		}
		
		/// <summary>
		/// The key that the message searches.
		/// </summary>
		public ID Key {
            get { return key; }
            set { this.key = value; }
		}
		
        /// <summary>
        /// the default name of the message
        /// </summary>
		public override string Name
		{
            get { return "FIND_VALUE"; }
            set { }
		}
	}
}
