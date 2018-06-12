using System.Collections.Generic;

using Phantasma.Kademlia.Common;

namespace Phantasma.Kademlia
{
    public class FindResult
    {
        public bool Found { get; set; }
        public List<Contact> FoundContacts { get; set; }
        public Contact FoundBy { get; set; }
        public string FoundValue { get; set; }
    }
}
