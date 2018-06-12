using System;

namespace Phantasma.Network.Kademlia.Messages
{
	/// <summary>
	/// Represents a ping message, used to see if a remote node is up.
	/// </summary>
	public class Ping : Message
	{
        /// <summary>
        /// Constructor of the ping message class
        /// </summary>
        /// <param name="senderID">Identificator of te sender</param>
        /// <param name="nodeEndpoint">Endpoint of the sender</param>
		public Ping(ID senderID, Endpoint nodeEndpoint) : base(senderID, nodeEndpoint)
		{
		}
		
        /// <summary>
        /// Default Property indicating the name of the message.
        /// </summary>
		public override string Name
		{
            get { return "PING"; }
            set { }
		}
	}
}
