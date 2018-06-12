using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Newtonsoft.Json;
using Phantasma.Utils;

namespace Phantasma.Kademlia
{
    public class KBucket 
    {
        public DateTime TimeStamp { get; set; }
        public List<Contact> Contacts { get { return contacts; } set { contacts = value; } }
        public BigInteger Low { get { return low; } set { low = value; } }
        public BigInteger High { get { return high; } set { high = value; } }

        /// <summary>
        /// We are going to assume that the "key" for this bucket is it's high range.
        /// </summary>
        [JsonIgnore]
        public BigInteger Key { get { return high; } }

        [JsonIgnore]
        public bool IsBucketFull { get { return contacts.Count == Constants.K; } }

        protected List<Contact> contacts;
        protected BigInteger low;
        protected BigInteger high;

        /// <summary>
        /// Initializes a k-bucket with the default range of 0 - 2^160
        /// </summary>
        public KBucket()
        {
            contacts = new List<Contact>();
            low = 0;
            high = BigInteger.Pow(new BigInteger(2), 160);
        }

        /// <summary>
        /// Initializes a k-bucket with a specific ID range.
        /// </summary>
        public KBucket(BigInteger low, BigInteger high)
        {
            contacts = new List<Contact>();
            this.low = low;
            this.high = high;
        }

        public void Touch()
        {
            TimeStamp = DateTime.Now;
        }

		/// <summary>
		/// True if ID is in range of this bucket.
		/// </summary>
		public bool HasInRange(ID id)
		{
			return Low <= id && id < High;
		}

        public bool HasInRange(BigInteger id)
        {
            return Low <= id && id < High;
        }

        /// <summary>
        /// True if a contact matches this ID.
        /// </summary>
        public bool Contains(ID id)
		{
			return contacts.Any(c => c.ID == id);
		}

		/// <summary>
		/// Add a contact to the bucket, at the end, as this is the most recently seen contact.
		/// A full bucket throws an exception.
		/// </summary>
		public void AddContact(Contact contact)
        {
            Validate.IsTrue<TooManyContactsException>(contacts.Count < Constants.K, "Bucket is full");
            contacts.Add(contact);
        }

        public void EvictContact(Contact contact)
        {
            contacts.Remove(contact);
        }

		/// <summary>
		/// Replaces the contact with the new contact, thus updating the LastSeen and network addressinfo. 
		/// </summary>
		public void ReplaceContact(Contact contact)
		{
			contacts.Remove(contacts.Single(c => c.ID == contact.ID));
			contacts.Add(contact);
		}

		/// <summary>
		/// Splits the kbucket into returning two new kbuckets filled with contacts separated by the new midpoint
		/// </summary>
		public (KBucket, KBucket) Split()
		{
			BigInteger midpoint = (Low + High) / 2;
			KBucket k1 = new KBucket(Low, midpoint);
			KBucket k2 = new KBucket(midpoint, High);

			Contacts.ForEach(c =>
			{
				// <, because the High value is exclusive in the HasInRange test.
				KBucket k = c.ID < midpoint ? k1 : k2;
				k.AddContact(c);
			});

			return (k1, k2);
		}

		/// <summary>
		/// Returns number of bits that are in common across all contacts.
		/// If there are no contacts, or no shared bits, the return is 0.
		/// </summary>
		public int Depth()
		{
			bool[] bits = new bool[0];

			if (contacts.Count > 0)
			{
				// Start with the first contact.
				bits = contacts[0].ID.Bytes.Bits().ToArray();

				contacts.Skip(1).ForEach(c => bits = SharedBits(bits, c.ID));
			}

			return bits.Length;
		}

		/// <summary>
		/// Returns a new bit array of just the shared bits.
		/// </summary>
		protected bool[] SharedBits(bool[] bits, ID id)
		{
			bool[] idbits = id.Bytes.Bits().ToArray();

			// Useful for viewing the bit arrays.
			//string sbits1 = System.String.Join("", bits.Select(b => b ? "1" : "0"));
			//string sbits2 = System.String.Join("", idbits.Select(b => b ? "1" : "0"));

			int q = Constants.ID_LENGTH_BITS - 1;
			int n = bits.Length - 1;
			List<bool> sharedBits = new List<bool>();

			while (n >= 0 && bits[n] == idbits[q])
			{
				sharedBits.Insert(0, (bits[n]));
				--n;
				--q;
			}

			return sharedBits.ToArray();
		}
	}
}
