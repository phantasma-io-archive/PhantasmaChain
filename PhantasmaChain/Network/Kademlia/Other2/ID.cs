using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Newtonsoft.Json;
using Phantasma.Utils;

namespace Phantasma.Kademlia
{
    public class ID : IComparable
    {
        public BigInteger Value { get { return id; } set { id = value; } }

        /// <summary>
        /// The array returned is in little-endian order (lsb at index 0)
        /// </summary>
        [JsonIgnore]
        public byte[] Bytes
        {
            get
            {
                // Zero-pad msb's if ToByteArray length != Constants.LENGTH_BYTES
                byte[] bytes = new byte[Constants.ID_LENGTH_BYTES];
                byte[] partial = id.ToByteArray().Take(Constants.ID_LENGTH_BYTES).ToArray();    // remove msb 0 at index 20.
                partial.CopyTo(bytes, 0);

                return bytes;
            }
        }

        [JsonIgnore]
        public string AsBigEndianString
        {
            get { return String.Join("", Bytes.Bits().Reverse().Select(b => b ? "1" : "0")); }
        }

        [JsonIgnore]
        public bool[] AsBigEndianBool { get { return Bytes.Bits().Reverse().ToArray(); } }

        public static ID FromBytes(IEnumerable<byte> bytes)
        {
            var hash = CryptoUtils.RIPEMD160(bytes);
            return new ID(hash);
        }

        /// <summary>
        /// Produce a random ID distributed evenly across the 160 bit space.
        /// </summary>
        [JsonIgnore]
        public static ID RandomIDInKeySpace
        {
            get
            {
                byte[] data = new byte[Constants.ID_LENGTH_BYTES];
                ID id = new ID(data);
                // Uniform random bucket index.
                int idx = rnd.Next(Constants.ID_LENGTH_BITS);
                // 0 <= idx <= 159
                // Remaining bits are randomized to get unique ID.
                id.SetBit(idx);
                id = id.RandomizeBeyond(idx);

                return id;
            }
        }

        /// <summary>
        /// Produce a random ID.
        /// </summary>
        [JsonIgnore]
        public static ID RandomID
        {
            get
            {
                byte[] buffer = new byte[Constants.ID_LENGTH_BYTES];
                rnd.NextBytes(buffer);

                return new ID(buffer);
            }
        }

        [JsonIgnore]
        public static ID Zero { get { return new ID(new byte[Constants.ID_LENGTH_BYTES]); } }

        [JsonIgnore]
        public static ID One
        {
            get
            {
                byte[] data = new byte[Constants.ID_LENGTH_BYTES];
                data[0] = 0x01;

                return new ID(data);
            }
        }

        [JsonIgnore]
        public static ID Mid
        {
            get
            {
                byte[] data = new byte[Constants.ID_LENGTH_BYTES];
                data[Constants.ID_LENGTH_BYTES - 1] = 0x80;

                return new ID(data);
            }
        }

        [JsonIgnore]
        public static ID Max { get { return new ID(Enumerable.Repeat((byte)0xFF, Constants.ID_LENGTH_BYTES).ToArray()); } }

        protected BigInteger id;

#if DEBUG
        public static Random rnd = new Random(1);
#else
        private static Random rnd = new Random();
#endif

        /// <summary>
        /// For serialization.
        /// </summary>
        public ID()
        {
        }

        /// <summary>
        /// Construct the ID from a byte array.
        /// </summary>
        public ID(byte[] data)
        {
            IDInit(data);
        }

        /// <summary>
        /// Construct the ID from another BigInteger value.
        /// </summary>
        public ID(BigInteger bi)
        {
            id = bi;
        }

        public ID(string strid)
        {
            bool ok = BigInteger.TryParse(strid, out id);
            Validate.IsTrue<BadIDException>(ok, "ID string is not valid.");
        }

        /// <summary>
        /// Returns an ID within the range of the bucket's Low and High range.
        /// The optional parameter forceBit1 is for our unit tests.
        /// This works because the bucket low-high range will always be a power of 2!
        /// </summary>
        public static ID RandomIDWithinBucket(KBucket bucket, bool forceBit1 = false)
        {
            // Simple case:
            // High = 1000
            // Low  = 0010
            // We want random values between 0010 and 1000

            // Low and High will always be powers of 2.
            var lowBits = new ID(bucket.Low).Bytes.Bits().Reverse();
            var highBits = new ID(bucket.High).Bytes.Bits().Reverse();

            // We randomize "below" this High prefix range.
            int highPrefix = highBits.TakeWhile(b => !b).Count() + 1;
            // Up to the prefix of the Low range.
            // This sets up a mask of 0's for the LSB's in the Low prefix.
            int lowPrefix = lowBits.TakeWhile(b => !b).Count();
            // RandomizeBeyond is little endian for "bits after" so reverse high/low prefixes.
            ID id = Zero.RandomizeBeyond(Constants.ID_LENGTH_BITS - highPrefix, Constants.ID_LENGTH_BITS - lowPrefix, forceBit1);

            // The we add the low range.
            id = new ID(bucket.Low + id.Value);

            return id;
        }

        /// <summary>
        /// Initialize the ID from a byte array, appending a 0 to force unsigned values.
        /// </summary>
        protected void IDInit(byte[] data)
        {
            Validate.IsTrue<IDLengthException>(data.Length == Constants.ID_LENGTH_BYTES, "ID must be " + Constants.ID_LENGTH_BYTES + " bytes in length.");
            id = new BigInteger(data.Append0());
        }

        /// <summary>
        /// Little endian randomization of of an ID beyond the specified (little endian) bit number.
        /// The optional parameter forceBit1 is for our unit tests.
        /// This CLEARS bits from bit+1 to ID_LENGTH_BITS!
        /// </summary>
#if DEBUG
        public ID RandomizeBeyond(int bit, int minLsb = 0, bool forceBit1 = false)
#else
        protected ID RandomizeBeyond(int bit, int minLsb = 0, bool forceBit1 = false)
#endif
        {
            byte[] randomized = Bytes;

			ID newid = new ID(randomized);

			// TODO: Optimize
			for (int i = bit + 1; i < Constants.ID_LENGTH_BITS; i++)
			{
				newid.ClearBit(i);
			}

			// TODO: Optimize
			for (int i = minLsb; i < bit; i++)
			{
				if ((rnd.NextDouble() < 0.5) || forceBit1)
				{
					newid.SetBit(i);
				}
			}

			return newid;
		}

		/// <summary>
		/// Clears the bit n, from the little-endian LSB.
		/// </summary>
		public ID ClearBit(int n)
		{
			byte[] bytes = Bytes;
			bytes[n / 8] &= (byte)((1 << (n % 8)) ^ 0xFF);
			id = new BigInteger(bytes.Append0());

            // for continuations.
            return this;
        }

        /// <summary>
        /// Sets the bit n, from the little-endian LSB.
        /// </summary>
        public ID SetBit(int n)
		{
			byte[] bytes = Bytes;
			bytes[n / 8] |= (byte)(1 << (n % 8));
			id = new BigInteger(bytes.Append0());

            // for continuations.
            return this;
		}

        // IComparable required methods.

        /// <summary>
        /// (From zencoders implemementation)
        /// Method used to get the hash code according to the algorithm: 
        /// http://stackoverflow.com/questions/16340/how-do-i-generate-a-hashcode-from-a-byte-array-in-c/425184#425184
        /// </summary>
        /// <returns>Integer representing the hashcode</returns>
        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            Validate.IsTrue<NotAnIDException>(obj is ID, "Cannot compare non-ID objects to an ID");

            return this == (ID)obj;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        /// <summary>
        /// Compare one ID with another.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>-1 if this ID < test ID, 0 if equal, 1 if this ID > test ID.</test></returns>
        public int CompareTo(object obj)
        {
            Validate.IsTrue<NotAnIDException>(obj is ID, "Cannot compare non-ID objects to an ID");
            ID test = (ID)obj;

            return this == test ? 0 : this < test ? -1 : 1;
        }

        // Operators:

        public static ID operator ^(ID a, ID b)
        {
            return new ID(a.id ^ b.id);
        }

        public static ID operator ^(BigInteger a, ID b)
        {
            return new ID(a ^ b.id);
        }

        public static bool operator <(ID a, ID b)
        {
            return a.id < b.id;
        }

        public static bool operator >(ID a, ID b)
        {
            return a.id > b.id;
        }

        public static bool operator <=(ID a, ID b)
        {
            return a.id <= b.id;
        }

        public static bool operator >=(ID a, ID b)
        {
            return a.id >= b.id;
        }

        public static bool operator <(BigInteger a, ID b)
        {
            return a < b.id;
        }

        public static bool operator >(BigInteger a, ID b)
        {
            return a > b.id;
        }

        public static bool operator <=(BigInteger a, ID b)
        {
            return a <= b.id;
        }

        public static bool operator >=(BigInteger a, ID b)
        {
            return a >= b.id;
        }

        public static bool operator <(ID a, BigInteger b)
        {
            return a.id < b;
        }

        public static bool operator >(ID a, BigInteger b)
        {
            return a.id > b;
        }

        public static bool operator <=(ID a, BigInteger b)
        {
            return a.id <= b;
        }

        public static bool operator >=(ID a, BigInteger b)
        {
            return a.id >= b;
        }

        public static bool operator ==(ID a, ID b)
        {
            Validate.IsFalse<NullIDException>(ReferenceEquals(a, null), "ID a cannot be null.");
            Validate.IsFalse<NullIDException>(ReferenceEquals(b, null), "ID b cannot be null.");

            return a.id == b.id;
        }

        public static bool operator ==(ID a, BigInteger b)
        {
            Validate.IsFalse<NullIDException>(ReferenceEquals(a, null), "ID a cannot be null.");
            Validate.IsFalse<NullIDException>(ReferenceEquals(b, null), "ID b cannot be null.");

            return a.id == b;
        }

        public static bool operator !=(ID a, ID b)
        {
            return !(a == b); // Already have that
        }

        public static bool operator !=(ID a, BigInteger b)
        {
            return !(a == b); // Already have that
        }

        public static ID operator <<(ID idobj, int count)
        {
            return new ID(idobj.id << count);
        }

        public static ID operator >>(ID idobj, int count)
        {
            return new ID(idobj.id >> count);
        }
    }
}
