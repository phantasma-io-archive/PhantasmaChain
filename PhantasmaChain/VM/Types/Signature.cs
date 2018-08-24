using Phantasma.Cryptography;
using Phantasma.Utils;
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

        public abstract bool Verify(byte[] message, Address address);

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
