using Phantasma.Utils;
using System.IO;

namespace Phantasma.VM.Types
{
    public abstract class Signature: IInteropObject
    {
        public byte[] bytes { get; private set; }

        public Signature(byte[] bytes)
        {
            this.bytes = bytes;
        }

        public int GetSize()
        {
            return bytes.Length;
        }

        public abstract bool Verify(byte[] message, Address address);

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteByteArray(this.bytes);
        }

        public void Unserialize(BinaryReader reader)
        {
            this.bytes = reader.ReadByteArray();
        }
    }
}
