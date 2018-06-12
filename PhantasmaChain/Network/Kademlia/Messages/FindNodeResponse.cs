using System;
using System.Collections.Generic;

namespace Phantasma.Network.Kademlia.Messages
{
	/// <summary>
	/// A response to a FindNode message.
	/// Contains a list of Contacts.
	/// </summary>
	public class FindNodeResponse : Response
	{
		private List<Contact> contacts;
		
        /// <summary>
        /// Constructor of the class. It calls the base constructor and store contacts into the message.
        /// </summary>
        /// <param name="nodeID">Identificator of the sender</param>
        /// <param name="request">FindNode request that orginates this response</param>
        /// <param name="recommended">List of contact that could match the request</param>
        /// <param name="nodeEndpoint">Address of the sender</param>
		public FindNodeResponse(ID nodeID, FindNode request, List<Contact> recommended, Endpoint nodeEndpoint) : base(nodeID, request, nodeEndpoint)
		{
			contacts = recommended;
		}
		
		/// <summary>
		/// Gets the list of recommended contacts.
		/// </summary>
		/// <returns></returns>
		public List<Contact> Contacts
		{
            get {return contacts;}
            set { this.contacts = value; }
		}
		
        /// <summary>
        /// Name of the message over the network
        /// </summary>
		public override string Name
		{
            get { return "FIND_NODE_RESPONSE"; }
            set { }
		}
	}
}
