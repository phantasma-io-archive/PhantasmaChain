using Phantasma.Cryptography;
using Phantasma.Domain;
using System;
using System.IO;

namespace Phantasma.Blockchain.Storage
{
    public class PrivateArchiveEncryption : IArchiveEncryption
    {
        public Address Address { get; private set; }

        public PrivateArchiveEncryption(Address publicKey)
        {
            this.Address = publicKey;
        }

        public PrivateArchiveEncryption()
        {
        }

        public ArchiveEncryptionMode Mode => ArchiveEncryptionMode.Private;
       
        public byte[] Encrypt(byte[] chunk, PhantasmaKeys keys)
        {
            if (keys.Address != this.Address)
            {
                throw new ChainException("encryption public address does not match");
            }

            return CryptoExtensions.AES256Encrypt(chunk, keys.PrivateKey);
        }

        public byte[] Decrypt(byte[] chunk, PhantasmaKeys keys)
        {
            if (keys.Address != this.Address)
            {
                throw new ChainException("decryption public address does not match");
            }

            return CryptoExtensions.AES256Decrypt(chunk, keys.PrivateKey);
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteAddress(Address);
        }

        public void UnserializeData(BinaryReader reader)
        {
            this.Address = reader.ReadAddress();
        }
    }
}
