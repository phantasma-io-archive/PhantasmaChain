using Phantasma.Core;
using Phantasma.Cryptography.Hashing;
using System;
using System.Text;

// Original author: Josip Medved 'Medo'
namespace Phantasma.Cryptography
{
    public class PBKDF2
    {

        /// <summary>
        /// Creates new instance.
        /// </summary>
        /// <param name="password">The password used to derive the key.</param>
        /// <param name="salt">The key salt used to derive the key.</param>
        /// <param name="iterations">The number of iterations for the operation.</param>
        /// <exception cref="System.ArgumentNullException">Algorithm cannot be null - Password cannot be null. -or- Salt cannot be null.</exception>
        public PBKDF2(byte[] password, byte[] salt, int iterations = 1000)
        {
            Throw.IfNull(password, nameof(password));
            Throw.IfNull(salt, nameof(salt));
            Throw.If(iterations < 2, "minimum number of iterations required");

            this.Salt = salt;
            this.IterationCount = iterations;
            this.BlockSize = 256 / 8;
            this.BufferBytes = new byte[this.BlockSize];
        }

        public PBKDF2(string password, string salt, int iterations = 1000) :
            this(UTF8Encoding.UTF8.GetBytes(password), UTF8Encoding.UTF8.GetBytes(salt), iterations)
        {
        }

        private readonly int BlockSize;
        private uint BlockIndex = 1;

        private byte[] BufferBytes;
        private int BufferStartIndex = 0;
        private int BufferEndIndex = 0;


        /// <summary>
        /// Gets salt bytes.
        /// </summary>
        public byte[] Salt { get; private set; }

        /// <summary>
        /// Gets iteration count.
        /// </summary>
        public int IterationCount { get; private set; }

        /// <summary>
        /// Returns a pseudo-random key from a password, salt and iteration count.
        /// </summary>
        /// <param name="count">Number of bytes to return.</param>
        /// <returns>Byte array.</returns>
        public byte[] GetBytes(int count)
        {
            byte[] result = new byte[count];
            int resultOffset = 0;
            int bufferCount = this.BufferEndIndex - this.BufferStartIndex;

            if (bufferCount > 0) //if there is some data in buffer
            { 
                if (count < bufferCount) //if there is enough data in buffer
                {
                    Array.Copy(this.BufferBytes, this.BufferStartIndex, result, 0, count); // TODO Buffer.BlockCopy
                    this.BufferStartIndex += count;
                    return result;
                }
                Array.Copy(this.BufferBytes, this.BufferStartIndex, result, 0, bufferCount); // TODO Buffer.BlockCopy
                this.BufferStartIndex = this.BufferEndIndex = 0;
                resultOffset += bufferCount;
            }

            while (resultOffset < count)
            {
                int needCount = count - resultOffset;
                this.BufferBytes = this.Func();
                if (needCount > this.BlockSize) //we one (or more) additional passes
                { 
                    Array.Copy(this.BufferBytes, 0, result, resultOffset, this.BlockSize); //TODO Buffer.BlockCopy
                    resultOffset += this.BlockSize;
                }
                else
                {
                    Array.Copy(this.BufferBytes, 0, result, resultOffset, needCount); // TODO Buffer.BlockCopy
                    this.BufferStartIndex = needCount;
                    this.BufferEndIndex = this.BlockSize;
                    return result;
                }
            }
            return result;
        }


        private byte[] Func()
        {
            var hash1Input = new byte[this.Salt.Length + 4];
            Array.Copy(this.Salt, 0, hash1Input, 0, this.Salt.Length); // TODO Buffer.BlockCopy
            Array.Copy(GetBytesFromInt(this.BlockIndex), 0, hash1Input, this.Salt.Length, 4); // TODO Buffer.BlockCopy

            var sha256 = new SHA256();
            var hash1 = sha256.ComputeHash(hash1Input);

            byte[] finalHash = hash1;
            for (int i = 2; i <= this.IterationCount; i++)
            {
                hash1 = sha256.ComputeHash(hash1, 0, (uint)hash1.Length);
                for (int j = 0; j < this.BlockSize; j++)
                {
                    finalHash[j] = (byte)(finalHash[j] ^ hash1[j]);
                }
            }

            Throw.If(this.BlockIndex == uint.MaxValue, "Derived key too long.");

            this.BlockIndex += 1;

            return finalHash;
        }

        private static byte[] GetBytesFromInt(uint i)
        {
            var bytes = BitConverter.GetBytes(i);
            if (BitConverter.IsLittleEndian)
            {
                return new byte[] { bytes[3], bytes[2], bytes[1], bytes[0] };
            }
            else
            {
                return bytes;
            }
        }

    }
}
