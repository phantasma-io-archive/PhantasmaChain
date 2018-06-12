using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using Phantasma.Kademlia.Common;
using Phantasma.Kademlia.Protocols;
using Phantasma.Utils;

namespace Phantasma.Kademlia
{
    public class Node : INode
    {
        [JsonIgnore]
        public Contact OurContact { get { return ourContact; } set { ourContact = value; } }

        public IBucketList BucketList { get { return bucketList; } set { bucketList = value; } }
        public IStorage Storage { get { return storage; } set { storage = value; } }
        public IStorage CacheStorage { get { return cacheStorage; } set { cacheStorage = value; } }

        [JsonIgnore]
        public DHT Dht { get { return dht; } set { dht = value; } }

        protected Contact ourContact;
        protected IBucketList bucketList;
        protected IStorage storage;
        protected IStorage cacheStorage;
        protected DHT dht;

        /// <summary>
        /// For serialization.
        /// </summary>
        public Node()
        {
        }

        /// <summary>
        /// If cache storage is not explicity provided, we use an in-memory virtual storage.
        /// </summary>
        public Node(Contact contact, IStorage storage, IStorage cacheStorage = null)
        {
            ourContact = contact;
            bucketList = new BucketList(contact);
            this.storage = storage;
            this.cacheStorage = cacheStorage;

            if (cacheStorage == null)
            {
                this.cacheStorage = new VirtualStorage();
            }
        }

        // ======= Server Entry Points =======

        public object ServerPing(CommonRequest request)
        {
            IProtocol protocol = Protocol.InstantiateProtocol(request.Protocol, request.ProtocolName);
            Ping(new Contact(protocol, new ID(request.Sender)));

            return new { RandomID = request.RandomID };
        }

        public object ServerStore(CommonRequest request)
        {
            IProtocol protocol = Protocol.InstantiateProtocol(request.Protocol, request.ProtocolName);
            Store(new Contact(protocol, new ID(request.Sender)), new ID(request.Key), request.Value, request.IsCached, request.ExpirationTimeSec);

            return new { RandomID = request.RandomID };
        }

        public object ServerFindNode(CommonRequest request)
        {
            IProtocol protocol = Protocol.InstantiateProtocol(request.Protocol, request.ProtocolName);
            var (contacts, val) = FindNode(new Contact(protocol, new ID(request.Sender)), new ID(request.Key));

            return new
            {
                Contacts = contacts.Select(c =>
                    new
                    {
                        Contact = c.ID.Value,
                        Protocol = c.Protocol,
                        ProtocolName = c.Protocol.GetType().Name
                    }).ToList(),
                RandomID = request.RandomID
            };
        }

        public object ServerFindValue(CommonRequest request)
        {
            IProtocol protocol = Protocol.InstantiateProtocol(request.Protocol, request.ProtocolName);
            var (contacts, val) = FindValue(new Contact(protocol, new ID(request.Sender)), new ID(request.Key));

            return new
            {
                Contacts = contacts?.Select(c =>
                new
                {
                    Contact = c.ID.Value,
                    Protocol = c.Protocol,
                    ProtocolName = c.Protocol.GetType().Name
                })?.ToList(),
                RandomID = request.RandomID,
                Value = val
            };
        }

        // ======= ======= ======= ======= =======

        /// <summary>
        /// Someone is pinging us.  Register the contact and respond.
        /// </summary>
        public Contact Ping(Contact sender)
        {
            Validate.IsFalse<SendingQueryToSelfException>(sender.ID == ourContact.ID, "Sender should not be ourself!");
            SendKeyValuesIfNewContact(sender);
            bucketList.AddContact(sender);

            return ourContact;
        }

        /// <summary>
        /// Store a key-value pair in the republish or cache storage.
        /// </summary>
        public void Store(Contact sender, ID key, string val, bool isCached = false, int expirationTimeSec = 0)
        {
            Validate.IsFalse<SendingQueryToSelfException>(sender.ID == ourContact.ID, "Sender should not be ourself!");
            bucketList.AddContact(sender);

            if (isCached)
            {
                cacheStorage.Set(key, val, expirationTimeSec);
            }
            else
            {
                SendKeyValuesIfNewContact(sender);
                storage.Set(key, val, Constants.EXPIRATION_TIME_SECONDS);
            }
        }

        /// <summary>
        /// From the spec: FindNode takes a 160-bit ID as an argument. The recipient of the RPC returns (IP address, UDP port, Node ID) triples 
        /// for the k nodes it knows about closest to the target ID. These triples can come from a single k-bucket, or they may come from 
        /// multiple k-buckets if the closest k-bucket is not full. In any case, the RPC recipient must return k items (unless there are 
        /// fewer than k nodes in all its k-buckets combined, in which case it returns every node it knows about).
        /// </summary>
        /// <returns></returns>
        public (List<Contact> contacts, string val) FindNode(Contact sender, ID key)
        {
            Validate.IsFalse<SendingQueryToSelfException>(sender.ID == ourContact.ID, "Sender should not be ourself!");
            SendKeyValuesIfNewContact(sender);
            bucketList.AddContact(sender);

            // Exclude sender.
            var contacts = bucketList.GetCloseContacts(key, sender.ID);

            return (contacts, null);
        }

        /// <summary>
        /// Returns either a list of close contacts or a the value, if the node's storage contains the value for the key.
        /// </summary>
        public (List<Contact> contacts, string val) FindValue(Contact sender, ID key)
        {
            Validate.IsFalse<SendingQueryToSelfException>(sender.ID == ourContact.ID, "Sender should not be ourself!");
            SendKeyValuesIfNewContact(sender);
            bucketList.AddContact(sender);

            if (storage.Contains(key))
            {
                return (null, storage.Get(key));
            }
            else if (CacheStorage.Contains(key))
            {
                return (null, CacheStorage.Get(key));
            }
            else
            {
                // Exclude sender.
                return (bucketList.GetCloseContacts(key, sender.ID), null);
            }
        }

#if DEBUG           // For unit testing
        public void SimpleStore(ID key, string val)
        {
            storage.Set(key, val);
        }
#endif

        /// <summary>
        /// For a new contact, we store values to that contact whose keys ^ ourContact are less than stored keys ^ [otherContacts].
        /// </summary>
        protected void SendKeyValuesIfNewContact(Contact sender)
        {
            List<Contact> contacts = new List<Contact>();

            if (IsNewContact(sender))
            {
                lock (bucketList)
                {
                    // Clone so we can release the lock.
                    contacts = new List<Contact>(bucketList.Buckets.SelectMany(b => b.Contacts));
                }

                if (contacts.Count() > 0)
                {
                    // and our distance to the key < any other contact's distance to the key...
                    storage.Keys.AsParallel().ForEach(k =>
                    {
                    // our min distance to the contact.
                    var distance = contacts.Min(c => k ^ c.ID);

                    // If our contact is closer, store the contact on its node.
                    if ((k ^ ourContact.ID) < distance)
                        {
                            var error = sender.Protocol.Store(ourContact, new ID(k), storage.Get(k));
                            dht?.HandleError(error, sender);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Returns true if the contact isn't in the bucket list or the pending contacts list.
        /// </summary>
        protected bool IsNewContact(Contact sender)
        {
            bool ret;

            lock (bucketList)
            {
                // If we have a new contact...
                ret = bucketList.ContactExists(sender);
            }

            if (dht != null)            // for unit testing, dht may be null
            {
                lock (dht.PendingContacts)
                {
                    ret |= dht.PendingContacts.ContainsBy(sender, c => c.ID);
                }
            }

            return !ret;
        }
    }
}