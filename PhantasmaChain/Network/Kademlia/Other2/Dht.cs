using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Timers;

using Newtonsoft.Json;

using Phantasma.Kademlia.Common;
using Phantasma.Utils;

namespace Phantasma.Kademlia
{
    public class DHT : IDht
    {
        public BaseRouter Router { get { return router; } set { router = value; } }

        [JsonIgnore]
        public ConcurrentDictionary<BigInteger, int> EvictionCount { get { return evictionCount; } }

        [JsonIgnore]
        public List<Contact> PendingContacts { get { return pendingContacts; } }

        [JsonIgnore]
        public Node Node { get { return node; } set { node = value; } }

        public IStorage RepublishStorage { get { return republishStorage; } set { republishStorage = value; } }
        public IStorage OriginatorStorage { get { return originatorStorage; } set { originatorStorage = value; } }
        public Contact Contact { get { return ourContact; } set { ourContact = value; } }

        [JsonIgnore]
        public ID ID { get { return ourId; } set { ourId = value; } }
        [JsonIgnore]
        public IProtocol Protocol { get { return protocol; } set { protocol = value; } }

        protected BaseRouter router;
        protected IStorage originatorStorage;
        protected IStorage republishStorage;
        protected IStorage cacheStorage;
        protected IProtocol protocol;
        protected Node node;
        protected Contact ourContact;
        protected ID ourId;
        protected Timer bucketRefreshTimer;
        protected Timer keyValueRepublishTimer;
        protected Timer originatorRepublishTimer;
        protected Timer expireKeysTimer;

        protected ConcurrentDictionary<BigInteger, int> evictionCount;
        protected List<Contact> pendingContacts;

        // For serializer, empty constructor needed.
        public DHT()
        {
        }

        /// <summary>
        /// Use this constructor to initialize the stores to the same instance.
        /// </summary>
        public DHT(ID id, IProtocol protocol, Func<IStorage> storageFactory, BaseRouter router)
        {
            originatorStorage = storageFactory();
            republishStorage = storageFactory();
            cacheStorage = storageFactory();
            FinishInitialization(id, protocol, router);
            SetupTimers();
        }

        /// <summary>
        /// Supports different concrete storage types.  For example, you may want the cacheStorage
        /// to be an in memory store, the originatorStorage to be a SQL database, and the republish store
        /// to be a key-value database.
        /// </summary>
        public DHT(ID id, IProtocol protocol, BaseRouter router, IStorage originatorStorage, IStorage republishStorage, IStorage cacheStorage)
        {
            this.originatorStorage = originatorStorage;
            this.republishStorage = republishStorage;
            this.cacheStorage = cacheStorage;
            FinishInitialization(id, protocol, router);
            SetupTimers();
        }

        /// <summary>
        /// Returns a JSON string of the serialized DHT.
        /// </summary>
        public string Save()
        {
            var settings = new JsonSerializerSettings();
            settings.TypeNameHandling = TypeNameHandling.Auto;
            string json = JsonConvert.SerializeObject(this, Formatting.Indented, settings);

            return json;
        }

        public static DHT Load(string json)
        {
            var settings = new JsonSerializerSettings();
            settings.TypeNameHandling = TypeNameHandling.Auto;

            DHT dht = JsonConvert.DeserializeObject<DHT>(json, settings);
            dht.DeserializationFixups();
            dht.SetupTimers();

            return dht;
        }

        protected void DeserializationFixups()
        {
            ID = ourContact.ID;
            protocol = ourContact.Protocol;
            node = router.Node;
            node.OurContact = ourContact;
            node.BucketList.OurID = ID;
            node.BucketList.OurContact = ourContact;
            router.Dht = this;
            node.Dht = this;
        }

/// <summary>
        /// Bootstrap our peer by contacting another peer, adding its contacts
        /// to our list, then getting the contacts for other peers not in the
        /// bucket range of our known peer we're joining.
        /// </summary>
        public RpcError Bootstrap(Contact knownPeer)
        {
            node.BucketList.AddContact(knownPeer);
            var (contacts, error) = knownPeer.Protocol.FindNode(ourContact, ourId);
            HandleError(error, knownPeer);

            if (!error.HasError)
            {
                contacts.ForEach(c => node.BucketList.AddContact(c));
                KBucket knownPeerBucket = node.BucketList.GetKBucket(knownPeer.ID);
                // Resolve the list now, so we don't include additional contacts as we add to our bucket additional contacts.
                var otherBuckets = node.BucketList.Buckets.Where(b => b != knownPeerBucket).ToList();
                otherBuckets.ForEach(b => RefreshBucket(b));

                foreach (KBucket otherBucket in otherBuckets)
                {
                    RefreshBucket(otherBucket);
                }
            }

            return error;
        }

        public void Store(ID key, string val)
        {
            TouchBucketWithKey(key);

            // We're storing to k closer contacts.
            originatorStorage.Set(key, val);
			StoreOnCloserContacts(key, val);
        }

        public (bool found, List<Contact> contacts, string val) FindValue(ID key)
        {
            TouchBucketWithKey(key);

            string ourVal;
            List<Contact> contactsQueried = new List<Contact>();
            (bool found, List<Contact> contacts, string val) ret = (false, null, null);

            if (originatorStorage.TryGetValue(key, out ourVal))
            {
                // Sort of odd that we are using the key-value store to find something the key-value that we originate.
                ret = (true, null, ourVal);
            }
            else if (republishStorage.TryGetValue(key, out ourVal))
            {
                // If we have it from another peer.
                ret = (true, null, ourVal);
            }
            else if (cacheStorage.TryGetValue(key, out ourVal))
            {
                // If we have it because it was cached.
                ret = (true, null, ourVal);
            }
            else
            {
                var lookup = router.Lookup(key, router.RpcFindValue);

                if (lookup.found)
                {
                    ret = (true, null, lookup.val);
                    // Find the first close contact (other than the one the value was found by) in which to *cache* the key-value.
                    var storeTo = lookup.contacts.Where(c => c != lookup.foundBy).OrderBy(c => c.ID ^ key).FirstOrDefault();

                    if (storeTo != null)
                    {
                        int separatingNodes = GetSeparatingNodesCount(ourContact, storeTo);
                        int expTimeSec = (int)(Constants.EXPIRATION_TIME_SECONDS / Math.Pow(2, separatingNodes));
                        RpcError error = storeTo.Protocol.Store(node.OurContact, key, lookup.val, true, expTimeSec);
                        HandleError(error, storeTo);
                    }
                }
            }

            return ret;
        }

#if DEBUG       // For demo and unit testing.
        public void PerformBucketRefresh()
        {
            // Get current bucket list in a separate collection because the bucket list might be modified
            // as the result of a bucket split.
            List<KBucket> currentBuckets = new List<KBucket>(node.BucketList.Buckets);
            currentBuckets.ForEach(b => RefreshBucket(b));
        }

        public void PerformStoreRepublish()
        {
            republishStorage.Keys.ForEach(k =>
            {
                ID key = new ID(k);
                StoreOnCloserContacts(key, republishStorage.Get(key));
                republishStorage.Touch(k);
            });
        }
#endif

        /// <summary>
        /// Put the timed out contact into a collection and increment the number of times it has timed out.
        /// If it has timed out a certain amount, remove it from the bucket and replace it with the most
        /// recent pending contact that are queued for that bucket.
        /// </summary>
        public void HandleError(RpcError error, Contact contact)
        {
            // For all errors:
            int count = AddContactToEvict(contact.ID.Value);

            if (count == Constants.EVICTION_LIMIT)
            {
                ReplaceContact(contact);
            }
        }

        public void AddToPending(Contact pending)
        {
            lock (pendingContacts)
            {
                pendingContacts.AddDistinctBy(pending, c => c.ID);
            }
        }

        /// <summary>
        ///  The contact that did not respond (or had an error) gets n tries before being evicted
        ///  and replaced with the most recently contact that wants to go into the non-responding contact's kbucket.
        /// </summary>
        /// <param name="toEvict">The contact that didn't respond.</param>
        /// <param name="toReplace">The contact that can replace the non-responding contact.</param>
        public void DelayEviction(Contact toEvict, Contact toReplace)
        {
            // Non-concurrent list needs locking.
            lock (pendingContacts)
            {
                // Add only if it's a new pending contact.
                pendingContacts.AddDistinctBy(toReplace, c=>c.ID);
            }

            BigInteger key = toEvict.ID.Value;
            int count = AddContactToEvict(key);

            if (count == Constants.EVICTION_LIMIT)
            {
                ReplaceContact(toEvict);
            }
        }

        protected int AddContactToEvict(BigInteger key)
        {
            if (!evictionCount.ContainsKey(key))
            {
                evictionCount[key] = 0;
            }

            int count = evictionCount[key] + 1;
            evictionCount[key] = count;

            return count;
        }

        protected void ReplaceContact(Contact toEvict)
        {
            KBucket bucket = node.BucketList.GetKBucket(toEvict.ID);

            // Prevent other threads from manipulating the bucket list or buckets.
            lock (node.BucketList)
            {
                EvictContact(bucket, toEvict);
                ReplaceWithPendingContact(bucket);
            }
        }

        protected void EvictContact(KBucket bucket, Contact toEvict)
        {
            evictionCount.TryRemove(toEvict.ID.Value, out _);
            Validate.IsTrue<BucketDoesNotContainContactToEvict>(bucket.Contains(toEvict.ID), "Bucket doesn't contain the contact to be evicted.");
            bucket.EvictContact(toEvict);
        }

        /// <summary>
        /// Find a pending contact that goes into the bucket that now has room.
        /// </summary>
        protected void ReplaceWithPendingContact(KBucket bucket)
        {
            Contact contact;

            // Non-concurrent list needs locking while we query it.
            lock (pendingContacts)
            {
                contact = pendingContacts.Where(c => node.BucketList.GetKBucket(c.ID) == bucket).OrderBy(c => c.LastSeen).LastOrDefault();

                if (contact != null)
                {
                    pendingContacts.Remove(contact);
                    bucket.AddContact(contact);
                }
            }
        }

        /// <summary>
        /// Return the number of nodes between the two contacts, where the contact list is sorted by the integer ID values (not XOR distance.)
        /// </summary>
        protected int GetSeparatingNodesCount(Contact a, Contact b)
        {
            // Sort of brutish way to do this.
            // Get all the contacts, ordered by their ID.
            List<Contact> allContacts = node.BucketList.Buckets.SelectMany(c => c.Contacts).OrderBy(c => c.ID.Value).ToList();

            int idxa = allContacts.IndexOf(a);
            int idxb = allContacts.IndexOf(b);

            return Math.Abs(idxa - idxb);
        }

        protected void FinishInitialization(ID id, IProtocol protocol, BaseRouter router)
        {
            evictionCount = new ConcurrentDictionary<BigInteger, int>();
            pendingContacts = new List<Contact>();
            ourId = id;
            ourContact = new Contact(protocol, id);
            node = new Node(ourContact, republishStorage, cacheStorage);
            node.Dht = this;
            node.BucketList.Dht = this;
            this.protocol = protocol;
            this.router = router;
            this.router.Node = node;
            this.router.Dht = this;
        }

        protected void SetupTimers()
        {
            SetupBucketRefreshTimer();
            SetupKeyValueRepublishTimer();
            SetupOriginatorRepublishTimer();
            SetupExpireKeysTimer();
        }

        protected void TouchBucketWithKey(ID key)
        {
            node.BucketList.GetKBucket(key).Touch();
        }

        protected void SetupBucketRefreshTimer()
        {
            bucketRefreshTimer = new Timer(Constants.BUCKET_REFRESH_INTERVAL);
            bucketRefreshTimer.AutoReset = true;
            bucketRefreshTimer.Elapsed += BucketRefreshTimerElapsed;
            bucketRefreshTimer.Start();
        }

        protected void SetupKeyValueRepublishTimer()
        {
            keyValueRepublishTimer = new Timer(Constants.KEY_VALUE_REPUBLISH_INTERVAL);
            keyValueRepublishTimer.AutoReset = true;
            keyValueRepublishTimer.Elapsed += KeyValueRepublishElapsed;
            keyValueRepublishTimer.Start();
        }

        protected void SetupOriginatorRepublishTimer()
        {
            originatorRepublishTimer = new Timer(Constants.ORIGINATOR_REPUBLISH_INTERVAL);
            originatorRepublishTimer.AutoReset = true;
            originatorRepublishTimer.Elapsed += OriginatorRepublishElapsed;
            originatorRepublishTimer.Start();
        }

        protected void SetupExpireKeysTimer()
        {
            expireKeysTimer = new Timer(Constants.KEY_VALUE_EXPIRE_INTERVAL);
            expireKeysTimer.AutoReset = true;
            expireKeysTimer.Elapsed += ExpireKeysElapsed;
            expireKeysTimer.Start();
        }

        protected void BucketRefreshTimerElapsed(object sender, ElapsedEventArgs e)
        {
            DateTime now = DateTime.Now;

            // Put into a separate list as bucket collections may be modified.
            List<KBucket> currentBuckets = new List<KBucket>(node.BucketList.Buckets.
                Where(b => (now - b.TimeStamp).TotalMilliseconds >= Constants.BUCKET_REFRESH_INTERVAL));

            currentBuckets.ForEach(b => RefreshBucket(b));
        }

        /// <summary>
        /// Replicate key values if the key-value hasn't been touched within the republish interval.
        /// Also don't do a FindNode lookup if the bucket containing the key has been refreshed within the refresh interval.
        /// </summary>
        protected void KeyValueRepublishElapsed(object sender, ElapsedEventArgs e)
        {
            DateTime now = DateTime.Now;

			republishStorage.Keys.Where(k => (now - republishStorage.GetTimeStamp(k)).TotalMilliseconds >= Constants.KEY_VALUE_REPUBLISH_INTERVAL).ForEach(k=>
			{
                ID key = new ID(k);
				StoreOnCloserContacts(key, republishStorage.Get(key));
				republishStorage.Touch(k);			
			});
        }

        protected void OriginatorRepublishElapsed(object sender, ElapsedEventArgs e)
        {
            DateTime now = DateTime.Now;

            originatorStorage.Keys.Where(k => (now - originatorStorage.GetTimeStamp(k)).TotalMilliseconds >= Constants.ORIGINATOR_REPUBLISH_INTERVAL).ForEach(k =>
            {
                ID key = new ID(k);
                // Just use close contacts, don't do a lookup.
                var contacts = node.BucketList.GetCloseContacts(key, node.OurContact.ID);

                contacts.ForEach(c =>
                {
                    RpcError error = c.Protocol.Store(ourContact, key, originatorStorage.Get(key));
                    HandleError(error, c);
                });

                originatorStorage.Touch(k);
            });
        }

        /// <summary>
        /// Any expired keys in the republish or node's cache are removed.
        /// </summary>
        protected virtual void ExpireKeysElapsed(object sender, ElapsedEventArgs e)
        {
            RemoveExpiredData(cacheStorage);
            RemoveExpiredData(republishStorage);
        }

        protected void RemoveExpiredData(IStorage store)
        {
            DateTime now = DateTime.Now;
            // ToList so our key list is resolved now as we remove keys.
            store.Keys.Where(k => (now - store.GetTimeStamp(k)).TotalSeconds >= store.GetExpirationTimeSec(k)).ToList().ForEach(k =>
            {
                store.Remove(k);
            });
        }

        /// <summary>
        /// Perform a lookup if the bucket containing the key has not been refreshed, 
        /// otherwise just get the contacts the k closest contacts we know about.
        /// </summary>
        protected void StoreOnCloserContacts(ID key, string val)
		{
			DateTime now = DateTime.Now;

			KBucket kbucket = node.BucketList.GetKBucket(key);
			List<Contact> contacts;

			if ((now - kbucket.TimeStamp).TotalMilliseconds < Constants.BUCKET_REFRESH_INTERVAL)
			{
				// Bucket has been refreshed recently, so don't do a lookup as we have the k closes contacts.
				contacts = node.BucketList.GetCloseContacts(key, node.OurContact.ID);
			}
			else
			{
				contacts = router.Lookup(key, router.RpcFindNodes).contacts;
			}

            contacts.ForEach(c =>
            {
                RpcError error = c.Protocol.Store(node.OurContact, key, val);
                HandleError(error, c);
            });
		}

		protected void RefreshBucket(KBucket bucket)
        {
            bucket.Touch();
            ID rndId = ID.RandomIDWithinBucket(bucket);
            // Isolate in a separate list as contacts collection for this bucket might change.
            List<Contact> contacts = bucket.Contacts.ToList();

            contacts.ForEach(c =>
            {
                var (newContacts, timeoutError) = c.Protocol.FindNode(ourContact, rndId);
                HandleError(timeoutError, c);
                newContacts?.ForEach(otherContact => node.BucketList.AddContact(otherContact));
            });
        }
    }
}
