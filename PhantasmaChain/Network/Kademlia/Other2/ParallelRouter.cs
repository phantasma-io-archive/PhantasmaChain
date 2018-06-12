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
    public class ParallelRouter : BaseRouter
    {
        // ======================
        // For parallel querying:
        protected ConcurrentQueue<ContactQueueItem> contactQueue;
        protected Semaphore semaphore;
        protected List<Thread> threads;

        protected bool stopWork;

        protected DateTime now;
        // ======================

#if DEBUG   // Used for unit testing when creating the DHT.  The DHT sets the node.
        public ParallelRouter()
        {
            contactQueue = new ConcurrentQueue<ContactQueueItem>();
            semaphore = new Semaphore(0, Int32.MaxValue);
            InitializeThreadPool();
        }
#endif

        public ParallelRouter(Node node)
        {
            this.node = node;
            contactQueue = new ConcurrentQueue<ContactQueueItem>();
            semaphore = new Semaphore(0, Int32.MaxValue);
            InitializeThreadPool();
        }

        /// <summary>
        /// Perform a lookup on the given key, finding either a node containing the key value
        /// or returning all closer contacts.
        /// This method is not re-entrant!  Do not call this method in parallel!
        /// </summary>
        public override (bool found, List<Contact> contacts, Contact foundBy, string val) Lookup(
            ID key,
            Func<ID, Contact, (List<Contact> contacts, Contact foundBy, string val)> rpcCall,
            bool giveMeAll = false)
        {
            stopWork = false;
            bool haveWork = true;
            FindResult findResult = new FindResult();
            List<Contact> ret = new List<Contact>();
            List<Contact> contactedNodes = new List<Contact>();
            List<Contact> closerContacts = new List<Contact>();
            List<Contact> fartherContacts = new List<Contact>();
            (bool found, List<Contact> contacts, Contact foundBy, string val) foundReturn = (false, null, null, String.Empty);

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
            // For unit testing, this is a bad way to get a list of close contacts with virtual nodes because we're always going to get the closest nodes right at the get go.
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

            nodesToQuery.ForEach(n => QueueWork(key, n, rpcCall, closerContacts, fartherContacts, findResult));
            SetQueryTime();

            // Add any new closer contacts to the list we're going to return.
            ret.AddRangeDistinctBy(closerContacts, (a, b) => a.ID == b.ID);

            // Spec: The lookup terminates when the initiator has queried and gotten responses from the k closest nodes it has seen.
            while (ret.Count < Constants.K && haveWork)
            {
                Thread.Sleep(Constants.RESPONSE_WAIT_TIME);

                if (ParallelFound(findResult, ref foundReturn))
                {
#if DEBUG       // For unit testing.
                    CloserContacts = closerContacts;
                    FartherContacts = fartherContacts;
#endif
                    StopRemainingWork();

                    return foundReturn;
                }

                List<Contact> closerUncontactedNodes = closerContacts.Except(contactedNodes).ToList();
                List<Contact> fartherUncontactedNodes = fartherContacts.Except(contactedNodes).ToList();
                bool haveCloser = closerUncontactedNodes.Count > 0;
                bool haveFarther = fartherUncontactedNodes.Count > 0;

                haveWork = haveCloser || haveFarther || !QueryTimeExpired();

                // Spec:  Of the k nodes the initiator has heard of closest to the target...
                if (haveCloser)
                {
                    // We're about to contact these nodes.
                    var alphaNodes = closerUncontactedNodes.Take(Constants.ALPHA);
                    contactedNodes.AddRangeDistinctBy(alphaNodes, (a, b) => a.ID == b.ID);
                    alphaNodes.ForEach(n => QueueWork(key, n, rpcCall, closerContacts, fartherContacts, findResult));
                    SetQueryTime();
                }
                else if (haveFarther)
                {
                    // We're about to contact these nodes.
                    var alphaNodes = fartherUncontactedNodes.Take(Constants.ALPHA);
                    contactedNodes.AddRangeDistinctBy(alphaNodes, (a, b) => a.ID == b.ID);
                    alphaNodes.ForEach(n => QueueWork(key, n, rpcCall, closerContacts, fartherContacts, findResult));
                    SetQueryTime();
                }
            }

#if DEBUG       // For unit testing.
            CloserContacts = closerContacts;
            FartherContacts = fartherContacts;
#endif

            StopRemainingWork();

            // Spec (sort of): Return max(k) closer nodes, sorted by distance.
            // For unit testing, giveMeAll can be true so that we can match against our alternate way of getting closer contacts.
            lock (locker)
            {
                // Clone the returning closer contact list so any threads still to return don't affect the collection at this point.
                return (false, new List<Contact>(giveMeAll ? ret : ret.Take(Constants.K).OrderBy(c => c.ID ^ key).ToList()), null, null);
            }
        }

        /// <summary>
        /// Sets the time of the query to now.
        /// </summary>
        protected void SetQueryTime()
        {
            now = DateTime.Now;
        }

        /// <summary>
        /// Returns true if the query time has expired.
        /// </summary>
        protected bool QueryTimeExpired()
        {
            return (DateTime.Now - now).TotalMilliseconds > Constants.QUERY_TIME;
        }

        protected void InitializeThreadPool()
        {
            threads = new List<Thread>();
            Constants.MAX_THREADS.ForEach(() =>
            {
                Thread thread = new Thread(new ThreadStart(RpcCaller));
                thread.IsBackground = true;
                thread.Start();
            });
        }

        protected void RpcCaller()
        {
            while (true)
            {
                semaphore.WaitOne();
                ContactQueueItem item;

                if (contactQueue.TryDequeue(out item))
                {
                    string val;
                    Contact foundBy;

                    if (GetCloserNodes(
                        item.Key,
                        item.Contact,
                        item.RpcCall,
                        item.CloserContacts,
                        item.FartherContacts,
                        out val,
                        out foundBy))
                    {
                        if (!stopWork)
                        {
                            // Possible multiple "found"
                            lock (locker)
                            {
                                item.FindResult.Found = true;
                                item.FindResult.FoundBy = foundBy;
                                item.FindResult.FoundValue = val;
                                item.FindResult.FoundContacts = new List<Contact>(item.CloserContacts);
                            }
                        }
                    }
                }
            }
        }

        protected void QueueWork(
            ID key,
            Contact contact,
            Func<ID, Contact, (List<Contact> contacts, Contact foundBy, string val)> rpcCall,
            List<Contact> closerContacts,
            List<Contact> fartherContacts,
            FindResult findResult
            )
        {
            contactQueue.Enqueue(new ContactQueueItem()
            {
                Key = key,
                Contact = contact,
                RpcCall = rpcCall,
                CloserContacts = closerContacts,
                FartherContacts = fartherContacts,
                FindResult = findResult
            });

            semaphore.Release();
        }

        protected void StopRemainingWork()
        {
            DequeueRemainingWork();
            stopWork = true;
        }

        protected void DequeueRemainingWork()
        {
            while (contactQueue.TryDequeue(out _)) { };
        }

        protected bool ParallelFound(FindResult findResult, ref (bool found, List<Contact> contacts, Contact foundBy, string val) foundRet)
        {
            lock (locker)
            {
                if (findResult.Found)
                {
                    // Prevent found contacts from changing as we clone the found contacts list.
                    lock (findResult.FoundContacts)
                    {
                        foundRet = (true, findResult.FoundContacts.ToList(), findResult.FoundBy, findResult.FoundValue);
                    }
                }

                return findResult.Found;
            }
        }
    }
}