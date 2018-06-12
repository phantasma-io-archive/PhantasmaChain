// #define TRY_CLOSEST_BUCKET

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

using Phantasma.Kademlia.Common;
using Phantasma.Utils;

namespace Phantasma.Kademlia
{
    public class Router : BaseRouter
    {
#if DEBUG   // Used for unit testing when creating the DHT.  The DHT sets the node.
        public Router()
        {
        }
#endif

        public Router(Node node)
        {
            this.node = node;
        }

        public override (bool found, List<Contact> contacts, Contact foundBy, string val) Lookup(
                    ID key,
                    Func<ID, Contact, (List<Contact> contacts, Contact foundBy, string val)> rpcCall,
                    bool giveMeAll = false)
        {
            bool haveWork = true;
            List<Contact> ret = new List<Contact>();
            List<Contact> contactedNodes = new List<Contact>();
            List<Contact> closerContacts = new List<Contact>();
            List<Contact> fartherContacts = new List<Contact>();
            List<Contact> closerUncontactedNodes = new List<Contact>();
            List<Contact> fartherUncontactedNodes = new List<Contact>();

#if TRY_CLOSEST_BUCKET
            // Spec: The lookup initiator starts by picking a nodes from its closest non-empty k-bucket
            KBucket bucket = FindClosestNonEmptyKBucket(key);

            // Not in spec -- sort by the closest nodes in the closest bucket.
            List<Contact> allNodes = node.BucketList.GetCloseContacts(key, node.OurContact.ID).Take(Constants.K).ToList(); 
            List<Contact> nodesToQuery = allNodes.Take(Constants.ALPHA).ToList();
            fartherContacts.AddRange(allNodes.Skip(Constants.ALPHA).Take(Constants.K - Constants.ALPHA));
#else
#if DEBUG
            List<Contact> allNodes = node.BucketList.GetKBucket(key).Contacts.Take(Constants.K).ToList();
#else
            // This is a bad way to get a list of close contacts with virtual nodes because we're always going to get the closest nodes right at the get go.
            List<Contact> allNodes = node.BucketList.GetCloseContacts(key, node.OurContact.ID).Take(Constants.K).ToList(); 
#endif
            List<Contact> nodesToQuery = allNodes.Take(Constants.ALPHA).ToList();

            // Also not explicitly in spec:
            // Any closer node in the alpha list is immediately added to our closer contact list, and
            // any farther node in the alpha list is immediately added to our farther contact list.
            closerContacts.AddRange(nodesToQuery.Where(n => (n.ID ^ key) < (node.OurContact.ID ^ key)));
            fartherContacts.AddRange(nodesToQuery.Where(n => (n.ID ^ key) >= (node.OurContact.ID ^ key)));

            // The remaining contacts not tested yet can be put here.
            fartherContacts.AddRange(allNodes.Skip(Constants.ALPHA).Take(Constants.K - Constants.ALPHA));
#endif

            // We're about to contact these nodes.
            contactedNodes.AddRangeDistinctBy(nodesToQuery, (a, b) => a.ID == b.ID);

            // Spec: The initiator then sends parallel, asynchronous FIND_NODE RPCS to the a nodes it has chosen, 
            // a is a system-wide concurrency parameter, such as 3.

            var queryResult = Query(key, nodesToQuery, rpcCall, closerContacts, fartherContacts);

            if (queryResult.found)
            {
#if DEBUG       // For unit testing.
                CloserContacts = closerContacts;
                FartherContacts = fartherContacts;
#endif
                return queryResult;
            }

            // Add any new closer contacts to the list we're going to return.
            ret.AddRangeDistinctBy(closerContacts, (a, b) => a.ID == b.ID);

            // Spec: The lookup terminates when the initiator has queried and gotten responses from the k closest nodes it has seen.
            while (ret.Count < Constants.K && haveWork)
            {
                closerUncontactedNodes = closerContacts.Except(contactedNodes).ToList();
                fartherUncontactedNodes = fartherContacts.Except(contactedNodes).ToList();
                bool haveCloser = closerUncontactedNodes.Count > 0;
                bool haveFarther = fartherUncontactedNodes.Count > 0;

                haveWork = haveCloser || haveFarther;

                // Spec:  Of the k nodes the initiator has heard of closest to the target...
                if (haveCloser)
                {

                    // Spec: ...it picks a that it has not yet queried and resends the FIND_NODE RPC to them. 
                    var newNodesToQuery = closerUncontactedNodes.Take(Constants.ALPHA).ToList();

                    // We're about to contact these nodes.
                    contactedNodes.AddRangeDistinctBy(newNodesToQuery, (a, b) => a.ID == b.ID);

                    queryResult = Query(key, newNodesToQuery, rpcCall, closerContacts, fartherContacts);

                    if (queryResult.found)
                    {
#if DEBUG       // For unit testing.
                        CloserContacts = closerContacts;
                        FartherContacts = fartherContacts;
#endif
                        return queryResult;
                    }
                }
                else if (haveFarther)
                {
                    var newNodesToQuery = fartherUncontactedNodes.Take(Constants.ALPHA).ToList();

                    // We're about to contact these nodes.
                    contactedNodes.AddRangeDistinctBy(fartherUncontactedNodes, (a, b) => a.ID == b.ID);

                    queryResult = Query(key, newNodesToQuery, rpcCall, closerContacts, fartherContacts);

                    if (queryResult.found)
                    {
#if DEBUG       // For unit testing.
                        CloserContacts = closerContacts;
                        FartherContacts = fartherContacts;
#endif
                        return queryResult;
                    }
                }
            }

#if DEBUG       // For unit testing.
            CloserContacts = closerContacts;
            FartherContacts = fartherContacts;
#endif

            // Spec (sort of): Return max(k) closer nodes, sorted by distance.
            // For unit testing, giveMeAll can be true so that we can match against our alternate way of getting closer contacts.
            return (false, (giveMeAll ? ret : ret.Take(Constants.K).OrderBy(c => c.ID ^ key).ToList()), null, null);
        }
   }
}