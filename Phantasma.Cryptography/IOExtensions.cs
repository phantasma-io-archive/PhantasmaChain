using Phantasma.Cryptography.EdDSA;
using Phantasma.Cryptography.Ring;
using Phantasma.Storage.Utils;
using System;
using System.IO;

namespace Phantasma.Cryptography
{
    public static class IOExtensions
    {
        public static void WritePublicKey(this BinaryWriter writer, ECC.ECPoint publicKey)
        {
            var bytes = publicKey.EncodePoint(true);
            writer.WriteByteArray(bytes);
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

        public static ECC.ECPoint ReadPublicKey(this BinaryReader reader)
        {
            var bytes = reader.ReadByteArray();
            var publicKey = ECC.ECPoint.DecodePoint(bytes, ECC.ECDsaSignature.Curve);
            return publicKey;
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

            switch (kind)
            {
                case SignatureKind.None:
                    return null;

                case SignatureKind.Ed25519:
                    {
                        var signature = new Ed25519Signature();
                        signature.UnserializeData(reader);
                        return signature;
                    }

                case SignatureKind.Ring:
                    {
                        var signature = new RingSignature();
                        signature.UnserializeData(reader);
                        return signature;
                    }

                default:
                    throw new NotImplementedException("read signature: " + kind);
            }
        }
    }
}
