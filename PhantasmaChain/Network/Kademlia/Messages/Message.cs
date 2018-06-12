using System;

namespace Phantasma.Network.Kademlia.Messages
{
	/// <summary>
	/// Represents a generic DHT RPC message
	/// </summary>
	public abstract class Message
	{
		// All messages include sender id and address
		private ID senderID;
        private Endpoint nodeEndpoint;
		private ID conversationID;
		
		/// <summary>
		/// Make a new message, recording the sender's ID.
		/// </summary>
		/// <param name="senderID">The sender identificator</param>
        /// <param name="nodeEndpoint">The sender endpoint</param>
		public Message(ID senderID, Endpoint nodeEndpoint) {
			this.senderID = senderID;
			this.conversationID = ID.RandomID();
            this.nodeEndpoint = nodeEndpoint;
		}
		
		/// <summary>
		/// Make a new message in a given conversation.
		/// </summary>
		/// <param name="senderID">The sender identificator</param>
		/// <param name="conversationID">The conversationID regarding the message</param>
        /// <param name="nodeEndpoint">the address of the sender</param>
		public Message(ID senderID, ID conversationID, Endpoint nodeEndpoint) {
			this.senderID = senderID;
			this.conversationID = conversationID;
            this.nodeEndpoint = nodeEndpoint;
		}
		
		/// <summary>
		/// the name of the message.
		/// </summary>
        public abstract string Name
        {
            get;
            set;
        }
		
		/// <summary>
		/// the ID of the sender of the message.
		/// </summary>
		public ID SenderID
        {
            get { return senderID; }
            set { this.senderID = value; }
		}
		
		/// <summary>
		/// the ID of this conversation.
		/// </summary>
		public ID ConversationID
        {
            get { return conversationID; }
            set { this.conversationID = value; }
		}

        /// <summary>
        /// the address of the sender
        /// </summary>
        public Endpoint NodeEndpoint
        {
            get { return nodeEndpoint; }
            set { this.nodeEndpoint = value; }
        }
	}
}
