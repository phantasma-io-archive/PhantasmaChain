using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using Phantasma.Utils;

namespace Phantasma.Kademlia.Common
{
    public abstract class BaseRouter
    {
#if DEBUG       // for unit testing
        [JsonIgnore]
        public List<Contact> CloserContacts { get; protected set; }

        [JsonIgnore]
        public List<Contact> FartherContacts { get; protected set; }
#endif

        public Node Node { get { return node; } set { node = value; } }

        [JsonIgnore]
        public DHT Dht { get { return dht; } set { dht = value; } }

        protected DHT dht;
        protected Node node;
        protected object locker = new object();

        public abstract (bool found, List<Contact> contacts, Contact foundBy, string val) Lookup(
            ID key,
            Func<ID, Contact, (List<Contact> contacts, Contact foundBy, string val)> rpcCall,
            bool giveMeAll = false);

        /// <summary>
        /// Using the k-bucket's key (it's high value), find the closest 
        /// k-bucket the given key that isn't empty.
        /// </summary>
#if DEBUG           // For unit testing.
        public virtual KBucket FindClosestNonEmptyKBucket(ID key)
#else
        protected virtual KBucket FindClosestNonEmptyKBucket(ID key)
#endif
        {
            KBucket closest = node.BucketList.Buckets.Where(b => b.Contacts.Count > 0).OrderBy(b => b.Key ^ key).FirstOrDefault();
            Validate.IsTrue<NoNonEmptyBucketsException>(closest != null, "No non-empty buckets exist.  You must first register a peer and add that peer to your bucketlist.");

            return closest;
        }

        /// <summary>
        /// Get sorted list of closest nodes to the given key.
        /// </summary>
#if DEBUG           // For unit testing.
        public List<Contact> GetClosestNodes(ID key, KBucket bucket)
#else
        protected List<Contact> GetClosestNodes(ID key, KBucket bucket)
#endif
        {
            return bucket.Contacts.OrderBy(c => c.ID ^ key).ToList();
        }

        public bool GetCloserNodes(
            ID key,
            Contact nodeToQuery,
            Func<ID, Contact, (List<Contact> contacts, Contact foundBy, string val)> rpcCall,
            List<Contact> closerContacts,
            List<Contact> fartherContacts,
            out string val,
            out Contact foundBy)
        {
            // As in, peer's nodes:
            // Exclude ourselves and the peers we're contacting (closerContacts and fartherContacts) to a get unique list of new peers.
            var (contacts, cFoundBy, foundVal) = rpcCall(key, nodeToQuery);
            val = foundVal;
            foundBy = cFoundBy;
            List<Contact> peersNodes = contacts.
                ExceptBy(node.OurContact, c => c.ID).
                ExceptBy(nodeToQuery, c => c.ID).
                Except(closerContacts).
                Except(fartherContacts).ToList();

            // Null continuation is a special case primarily for unit testing when we have no nodes in any buckets.
            var nearestNodeDistance = nodeToQuery.ID ^ key;

            lock (locker)
            {
                closerContacts.
                    AddRangeDistinctBy(peersNodes.
                        Where(p => (p.ID ^ key) < nearestNodeDistance),
                        (a, b) => a.ID == b.ID);
            }

            lock (locker)
            {
                fartherContacts.
                    AddRangeDistinctBy(peersNodes.
                        Where(p => (p.ID ^ key) >= nearestNodeDistance),
                        (a, b) => a.ID == b.ID);
            }

            return val != null;
        }

        public (List<Contact> contacts, Contact foundBy, string val) RpcFindNodes(ID key, Contact contact)
        {
            var (newContacts, timeoutError) = contact.Protocol.FindNode(node.OurContact, key);

            // Null continuation here to support unit tests where a DHT hasn't been set up.
            dht?.HandleError(timeoutError, contact);
            
            return (newContacts, null, null);
        }

        /// <summary>
        /// For each contact, call the FindNode and return all the nodes whose contacts responded
        /// within a "reasonable" period of time, unless a value is returned, at which point we stop.
        /// </summary>
        public (List<Contact> contacts, Contact foundBy, string val) RpcFindValue(ID key, Contact contact)
        {
            List<Contact> nodes = new List<Contact>();
            string retval = null;
            Contact foundBy = null;

            var (otherContacts, val, error) = contact.Protocol.FindValue(node.OurContact, key);
            dht.HandleError(error, contact);

            if (!error.HasError)
            {
                if (otherContacts != null)
                {
                    nodes.AddRange(otherContacts);
                }
                else
                {
                    Validate.IsTrue<ValueCannotBeNullException>(val != null, "Null values are not supported nor expected.");
                    nodes.Add(contact);           // The node we just contacted found the value.
                    foundBy = contact;
                    retval = val;
                }
            }

            return (nodes, foundBy, retval);
        }

        protected (bool found, List<Contact> closerContacts, Contact foundBy, string val) Query(ID key, List<Contact> nodesToQuery, Func<ID, Contact, (List<Contact> contacts, Contact foundBy, string val)> rpcCall, List<Contact> closerContacts, List<Contact> fartherContacts)
        {
            bool found = false;
            Contact foundBy = null;
            string val = String.Empty;

            foreach (var n in nodesToQuery)
            {
                if (GetCloserNodes(key, n, rpcCall, closerContacts, fartherContacts, out val, out foundBy))
                {
                    found = true;
                    break;
                }
            }

            return (found, closerContacts, foundBy, val);
        }
    }
}
