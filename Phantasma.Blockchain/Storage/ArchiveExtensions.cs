using Phantasma.Domain;
using System;
using System.IO;

namespace Phantasma.Blockchain.Storage
{
    public static class ArchiveExtensions
    {
        public static readonly byte[] Uncompressed = new byte[] { (byte)ArchiveEncryptionMode.None };

        public static void WriteArchiveEncryption(this BinaryWriter writer, IArchiveEncryption encryption)
        {
            if (encryption == null)
            {
                writer.Write((byte)ArchiveEncryptionMode.None);
                return;
            }

            writer.Write((byte)encryption.Mode);
            encryption.SerializeData(writer);
        }

        public static byte[] ToBytes(this IArchiveEncryption encryption)
        {
            if (encryption == null || encryption.Mode == ArchiveEncryptionMode.None)
            {
                return new byte[0];
            }

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    WriteArchiveEncryption(writer, encryption);
                }
                return stream.ToArray();
            }
        }

        public static IArchiveEncryption ReadArchiveEncryption(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    return ReadArchiveEncryption(reader);
                }
            }
        }

        public static IArchiveEncryption ReadArchiveEncryption(this BinaryReader reader)
        {
            var mode = (ArchiveEncryptionMode)reader.ReadByte();

            IArchiveEncryption encryption;
            switch (mode)
            {
                case ArchiveEncryptionMode.None: return null;
                case ArchiveEncryptionMode.Private: encryption = new PrivateArchiveEncryption(); break;
                case ArchiveEncryptionMode.Shared: encryption = new SharedArchiveEncryption(); break;

                default:
                    throw new NotImplementedException("read archive encryption: " + mode);
            }

            encryption.UnserializeData(reader);
            return encryption;
        }
    }
}
