using Phantasma.Cryptography.ECC;
using Phantasma.Cryptography.EdDSA;
using Phantasma.Storage.Utils;
using System;
using System.IO;

namespace Phantasma.Cryptography
{
    public static class IOExtensions
    {
        public static void WritePublicKey(this BinaryWriter writer, byte[] publicKey)
        {
            writer.WriteByteArray(publicKey);
        }

        public static void WriteAddress(this BinaryWriter writer, Address address)
        {
            address.SerializeData(writer);
        }

        public static void WriteHash(this BinaryWriter writer, Hash hash)
        {
            hash.SerializeData(writer);
        }

        public static void WriteSignature(this BinaryWriter writer, Signature signature)
        {
            if (signature == null)
            {
                writer.Write((byte)SignatureKind.None);
                return;
            }

            writer.Write((byte)signature.Kind);
            signature.SerializeData(writer);
        }

        public static byte[] ReadPublicKey(this BinaryReader reader)
        {
            var bytes = reader.ReadByteArray();
            return bytes;
        }

        public static Address ReadAddress(this BinaryReader reader)
        {
            var address = new Address();
            address.UnserializeData(reader);
            return address;
        }

        public static Hash ReadHash(this BinaryReader reader)
        {
            var hash = new Hash();
            hash.UnserializeData(reader);
            return hash;
        }

        public static Signature ReadSignature(this BinaryReader reader)
        {
            var kind = (SignatureKind)reader.ReadByte();

            Signature signature;
            switch (kind)
            {
                case SignatureKind.None: return null;

                case SignatureKind.Ed25519: signature = new Ed25519Signature(); break;
                case SignatureKind.ECDSA: signature = new ECDsaSignature(); break;

                default:
                    throw new NotImplementedException("read signature: " + kind);
            }

            signature.UnserializeData(reader);
            return signature;
        }
    }
}
