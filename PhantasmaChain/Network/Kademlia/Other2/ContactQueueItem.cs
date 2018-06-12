using System;
using System.Collections.Generic;

using Phantasma.Kademlia.Common;

namespace Phantasma.Kademlia
{
    public class ContactQueueItem
    {
        public ID Key { get; set; }
        public Contact Contact { get; set; }
        public Func<ID, Contact, (List<Contact> contacts, Contact foundBy, string val)> RpcCall { get; set; }
        public List<Contact> CloserContacts { get; set; }
        public List<Contact> FartherContacts { get; set; }
        public FindResult FindResult { get; set; }
    }
}
