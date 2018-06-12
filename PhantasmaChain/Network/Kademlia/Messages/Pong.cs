using System;

namespace Phantasma.Network.Kademlia.Messages
{
	/// <summary>
	/// Represents a ping reply.
	/// </summary>
	public class Pong : Response
	{
        /// <summary>
        /// Constructor of the Ping class
        /// </summary>
        /// <param name="senderID">Identificator of the Sender</param>
        /// <param name="ping">Ping message originating the pong</param>
        /// <param name="nodeEndpoint">URI endpoint of the Sender</param>
		public Pong(ID senderID, Ping ping, Endpoint nodeEndpoint) : base(senderID, ping, nodeEndpoint)
		{
		}
		
        /// <summary>
        /// Name of the message
        /// </summary>
		public override string Name
		{
            get { return "PONG"; }
            set { }
		}
	}
}
