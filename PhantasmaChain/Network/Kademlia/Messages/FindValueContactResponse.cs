using System;
using System.Collections.Generic;

namespace Phantasma.Network.Kademlia.Messages
{
	/// <summary>
	/// Description of FindKeyContactResponse.
	/// </summary>
	public class FindValueContactResponse : Response
	{
		private List<Contact> contacts;
		
		/// <summary>
		/// Make a new response reporting contacts to try.
		/// </summary>
		/// <param name="nodeID">the sender identificator</param>
		/// <param name="request">The FindValue message orginating this response</param>
		/// <param name="close">The list of suggested contacts</param>
        /// <param name="nodeEndpoint">The address of the sender</param>
		public FindValueContactResponse(ID nodeID, FindValue request, List<Contact> close, Endpoint nodeEndpoint) : base(nodeID, request, nodeEndpoint)
		{
			contacts = close;
		}
		
		/// <summary>
		/// The list of contacts sent.
		/// </summary>
		public List<Contact> Contacts
		{
            get { return contacts; }
            set { this.contacts = value; }
		}
		
        /// <summary>
        /// The default name for the message
        /// </summary>
		public override string Name
		{
            get { return "FIND_VALUE_RESPONSE_CONTACTS"; }
            set { }
		}
	}
}
