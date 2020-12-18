using Phantasma.Cryptography;
using Phantasma.Domain;
using System;
using System.IO;

namespace Phantasma.Blockchain.Storage
{
    public class PrivateArchiveEncryption : IArchiveEncryption
    {
        public Address Address { get; private set; }
        private readonly int InitializationVectorSize = 16;
        public byte[] NameInitializationVector { get; private set; }
        public byte[] ContentInitializationVector { get; private set; }

        public PrivateArchiveEncryption(Address publicKey)
        {
            this.Address = publicKey;
            this.NameInitializationVector = CryptoExtensions.AESGenerateIV(InitializationVectorSize);
            this.ContentInitializationVector = CryptoExtensions.AESGenerateIV(InitializationVectorSize);
        }

        public PrivateArchiveEncryption()
        {
        }

        public ArchiveEncryptionMode Mode => ArchiveEncryptionMode.Private;

        public string EncryptName(string name, PhantasmaKeys keys)
        {
            if (keys.Address != this.Address)
            {
                throw new ChainException("encryption public address does not match");
            }

            return Numerics.Base58.Encode(CryptoExtensions.AESGCMEncrypt(System.Text.Encoding.UTF8.GetBytes(name), keys.PrivateKey, NameInitializationVector));
        }
        public string DecryptName(string name, PhantasmaKeys keys)
        {
            if (keys.Address != this.Address)
            {
                throw new ChainException("encryption public address does not match");
            }

            return System.Text.Encoding.UTF8.GetString(CryptoExtensions.AESGCMDecrypt(Numerics.Base58.Decode(name), keys.PrivateKey, NameInitializationVector));
        }
        public byte[] Encrypt(byte[] chunk, PhantasmaKeys keys)
        {
            if (keys.Address != this.Address)
            {
                throw new ChainException("encryption public address does not match");
            }

            return CryptoExtensions.AESGCMEncrypt(chunk, keys.PrivateKey, ContentInitializationVector);
        }

        public byte[] Decrypt(byte[] chunk, PhantasmaKeys keys)
        {
            if (keys.Address != this.Address)
            {
                throw new ChainException("decryption public address does not match");
            }

            return CryptoExtensions.AESGCMDecrypt(chunk, keys.PrivateKey, ContentInitializationVector);
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteAddress(Address);
            writer.Write(NameInitializationVector);
            writer.Write(ContentInitializationVector);
        }

        public void UnserializeData(BinaryReader reader)
        {
            this.Address = reader.ReadAddress();
            this.NameInitializationVector = reader.ReadBytes(InitializationVectorSize);
            this.ContentInitializationVector = reader.ReadBytes(InitializationVectorSize);
        }
    }
}
