using Phantasma.Cryptography;
using Phantasma.Utils;
using System.Collections.Generic;
using System.IO;

namespace Phantasma.VM.Types
{
    [VMType]
    public abstract class Signature
    {
        public byte[] Bytes { get; private set; }
        public abstract SignatureKind Kind { get; }

        public Signature(byte[] bytes)
        {
            this.Bytes = bytes;
        }

        public int GetSize()
        {
            return Bytes.Length;
        }

        /// <summary>
        /// Checks if this transaction was signed by at least one of the addresses
        /// </summary>
        public abstract bool Verify(byte[] message, IEnumerable<Address> addresses);

        public bool Verify(byte[] message, Address address)
        {
            return Verify(message, new Address[] { address });
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteByteArray(this.Bytes);
        }

        public void Unserialize(BinaryReader reader)
        {
            this.Bytes = reader.ReadByteArray();
        }
    }
}
