using System;
using System.Security.Cryptography;
using System.Text;
using System.Net.NetworkInformation;
using System.Threading;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Linq;
using Phantasma.Utils;
using System.Collections.Generic;

namespace Phantasma.Network.Kademlia
{
	/// <summary>
	/// Represents a 160-bit number which is used both as a nodeID and as a key for the DHT.
	/// The number is stored big-endian (most-significant-byte first).
	/// IDs are immutable.
	/// </summary>
	[Serializable]
	public class ID : IComparable
	{
        /// This is how long IDs should be, in bytes.
		public const int ID_LENGTH = 20;
		private byte[] data;
		
		// We want to be able to generate random IDs without timing issues.
		private static Random rnd = new Random();
		
		/// <summary>
		/// Make a new ID from a byte array.
		/// </summary>
		/// <param name="data">An array of exactly 20 bytes.</param>
		public ID(byte[] data)
		{
            IDInit(data);
		}

        /// <summary>
        /// Make a new ID starting from a string. Pay attention, the string is not hashed, it is
        /// simply translated to an ID object.
        /// </summary>
        /// <param name="new_id">The string to translate</param>
        public ID(string new_id)
        {
            BinaryFormatter bf = new BinaryFormatter();
            byte[] data;
            MemoryStream ms = new MemoryStream();
            bf.Serialize(ms, new_id);
            ms.Seek(0, 0);
            data = ms.ToArray();
            IDInit(data);
        }

        /// <summary>
        /// This method initialize an id starting from a byte array.
        /// </summary>
        /// <param name="data">Data converted</param>
        private void IDInit(byte[] data)
        {
            if (data.Length == ID_LENGTH)
            {
                this.data = new byte[ID_LENGTH];
                data.CopyTo(this.data, 0); // Copy the array into us.
            }
            else
            {
                throw new Exception("An ID must be exactly " + ID_LENGTH + " bytes.");
            }
        }
		
		/// <summary>
		/// Hash a string to produce an ID
		/// </summary>
		/// <param name="key">Key string to convert</param>
		/// <returns>An ID that represents the hash of the input string</returns>
		public static ID Hash(string key)
		{
			HashAlgorithm hasher = new SHA1CryptoServiceProvider(); // Keeping this around results in exceptions
			return new ID(hasher.ComputeHash(Encoding.UTF8.GetBytes(key)));
		}

        /// <summary>
        /// Method that generates an ID starting from a generic string. This is not hashing.
        /// </summary>
        /// <param name="hash">The hash string originating an ID</param>
        /// <returns>The ID generated</returns>
        public static ID FromString(string hash)
        {
            return new ID(
                    Enumerable.Range(0, hash.Length)
                     .Where(x => x % 2 == 0)
                     .Select(x => Convert.ToByte(hash.Substring(x, 2), 16))
                     .ToArray()
                     );
        }

        public static ID FromBytes(IEnumerable<byte> bytes)
        {
            var hash = CryptoUtils.RIPEMD160(bytes);
            return new ID(hash);
        }

        public static ID FromHash(byte[] hash)
        {
            if (hash == null || hash.Length != 20)
            {
                throw new ArgumentException("Invalid hash length");
            }

            return new ID(hash);
        }

        /// <summary>
        /// XOR operator.
        /// This is our distance metric in the DHT.
        /// </summary>
        /// <param name="a">The first ID to make xor</param>
        /// <param name="b">The second ID to make xor</param>
        /// <returns></returns>
        public static ID operator^(ID a, ID b)
		{
			byte[] xoredData = new byte[ID_LENGTH];
			// Do each byte in turn
			for(int i = 0; i < ID_LENGTH; i++) {
				xoredData[i] = (byte) (a.data[i] ^ b.data[i]);
			}
			return new ID(xoredData);
		}
		
		/// <summary>
        /// We need to compare these when measuring distance
		/// </summary>
		/// <param name="a">First ID to compare</param>
		/// <param name="b">Second ID to compare</param>
		/// <returns>true if a is less than b; false otherwise</returns>
		public static bool operator<(ID a, ID b)
		{
			for(int i = 0; i < ID_LENGTH; i++) {
				if(a.data[i] < b.data[i]) {
					return true; // If first mismatch is a < b, a < b
				} else if (a.data[i] > b.data[i]) {
					return false; // If first mismatch is a > b, a > b
				}
			}
			return false; // No mismatches
		}

        /// <summary>
        /// We need to compare these when measuring distance
        /// </summary>
        /// <param name="a">First ID to compare</param>
        /// <param name="b">Second ID to compare</param>
        /// <returns>true if a is greater than b; false otherwise</returns>
		public static bool operator>(ID a, ID b) {
			for(int i = 0; i < ID_LENGTH; i++) {
				if(a.data[i] < b.data[i]) {
					return false; // If first mismatch is a < b, a < b
				} else if (a.data[i] > b.data[i]) {
					return true; // If first mismatch is a > b, a > b
				}
			}
			return false; // No mismatches
		}

        /// <summary>
        /// We need to compare these when measuring distance
        /// </summary>
        /// <param name="a">First ID to compare</param>
        /// <param name="b">Second ID to compare</param>
        /// <returns>true if a is equals to b; false otherwise</returns>
		public static bool operator==(ID a, ID b) {
			// Handle null
			if(ValueType.ReferenceEquals(a, null)) {
				ValueType.ReferenceEquals(b, null);
			}
			if(ValueType.ReferenceEquals(b, null)) {
				return false;
			}
			
			// Actually check
			for(int i = 0; i < ID_LENGTH; i++) {
				if(a.data[i] != b.data[i]) { // Find the first difference
					return false;
				}
			}
			return true; // Must match
		}

        /// <summary>
        /// We need to compare these when measuring distance
        /// </summary>
        /// <param name="a">First ID to compare</param>
        /// <param name="b">Second ID to compare</param>
        /// <returns>true if a is different from b; false otherwise</returns>
		public static bool operator!=(ID a, ID b) {
			return !(a == b); // Already have that
		}
		
        /// <summary>
        /// Method used to get the hash code according to the algorithm: 
        /// http://stackoverflow.com/questions/16340/how-do-i-generate-a-hashcode-from-a-byte-array-in-c/425184#425184
        /// </summary>
        /// <returns>integer representing the hashcode</returns>
		public override int GetHashCode()
		{
			int hash = 0;
			for(int i = 0; i < ID_LENGTH; i++) {
				unchecked {
					hash *= 31;
				}
				hash ^= data[i];
			}
			return hash;
		}
		
        /// <summary>
        /// Method used to verify if two objects are equals.
        /// </summary>
        /// <param name="obj">The object to compare to</param>
        /// <returns>true if the objects are equals.</returns>
		public override bool Equals(object obj)
		{
			if(obj is ID) {
				return this == (ID) obj;
			} else {
				return false;
			}
		}
		
		/// <summary>
		/// Determines the least significant bit at which the given ID differs from this one, from 0 through 8 * ID_LENGTH - 1.
		/// PRECONDITION: IDs do not match.
		/// </summary>
		/// <param name="other">The ID to compare to</param>
		/// <returns>The least significant bit where can be found the difference</returns>
		public int DifferingBit(ID other)
		{
			ID differingBits = this ^ other;
			int differAt = 8 * ID_LENGTH - 1;
			
			// Subtract 8 for every zero byte from the right
			int i = ID_LENGTH - 1;
			while(i >= 0 && differingBits.data[i] == 0) {
				differAt -= 8;
				i--;
			}
			
			// Subtract 1 for every zero bit from the right
			int j = 0;
			// 1 << j = pow(2, j)
			while(j < 8 && (differingBits.data[i] & (1 << j)) == 0) {
				j++;
				differAt--;
			}
			
			return differAt;
		}
		
		/// <summary>
		/// Return a copy of ourselves that differs from us at the given bit and is random beyond that.
		/// </summary>
		/// <param name="bit">the bit to start to launch random bits</param>
		/// <returns>The new ID obtained</returns>
		public ID RandomizeBeyond(int bit)
		{
			byte[] randomized = new byte[ID_LENGTH];
			this.data.CopyTo(randomized, 0);
			
			FlipBit(randomized, bit); // Invert pivot bit
			
			// And randomly flip the rest
			for(int i = bit + 1; i < 8 * ID_LENGTH; i++) {
				if(rnd.NextDouble() < 0.5) {
					FlipBit(randomized, i);
				}
			}
			return new ID(randomized);
		}
		
		/// <summary>
		/// Flips the given bit in the byte array.
		/// Byte array must be ID_LENGTH long.
		/// </summary>
		/// <param name="data">Data to work on</param>
		/// <param name="bit">Bit used to generate the mask</param>
		private static void FlipBit(byte[] data, int bit)
		{
			int byteIndex = bit / 8;
			int byteBit = bit % 8;
			byte mask = (byte) (1 << byteBit);
			
			data[byteIndex] = (byte) (data[byteIndex] ^ mask); // Use a mask to flip the bit
		}
		
		/// <summary>
		/// Produce a random ID.
		/// </summary>
		/// <returns>random ID generated</returns>
		public static ID RandomID()
		{
			byte[] data = new byte[ID_LENGTH];
			rnd.NextBytes(data);
			return new ID(data);
		}
				
		/// <summary>
		/// Turn this ID into a string.
		/// </summary>
		/// <returns>A string representation for the ID</returns>
		public override string ToString()
		{
			return data.ByteToHex();
		}
		
		/// <summary>
		/// Compare ourselves to an object
		/// </summary>
		/// <param name="obj">An obect to compare to</param>
		/// <returns>
        /// 1 if the ID is greater than the object, 0 if the object are equals and -1 if object is greater
        /// than this.
        /// </returns>
		public int CompareTo(object obj)
		{
			if(obj is ID) {
				// Compare as ID.
				if(this < (ID) obj) {
					return -1;
				} else if(this == (ID) obj) {
					return 0;
				} else {
					return 1;
				}
			} else {
				return 1; // We're bigger than random crap
			}
		}
	}
}
