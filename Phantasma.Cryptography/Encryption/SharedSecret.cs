using Phantasma.Storage;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using Phantasma.Cryptography.ECC;

namespace Phantasma.Cryptography
{
    public struct SharedSecret : ISerializable
    {
        public ECDsaCurve Curve { get; private set; }
        public Address Address { get; private set; }
        public byte[] Payload { get; private set; }

        private const string initVector = "pemgail9uzpgzl88";
        private const int keysize = 256;

        public SharedSecret(ECDsaCurve curve, Address address, byte[] payload)
        {
            this.Curve = curve;
            this.Address = address;
            this.Payload = payload;
        }

        public static byte[] GetSharedSecret(PhantasmaKeys local, ECC.ECPoint remote)
        {
            var secret = (remote * local.PrivateKey).EncodePoint(true);
            return secret.Sha256();
        }

        public static SharedSecret Encrypt<T>(T message, PhantasmaKeys privateKey, Address publicAddress, ECDsaCurve curve)
        {
            var ecdCurve = curve.GetCurve();
            var pubBytes = ECDsaSignature.ExtractPublicKeyFromAddress(publicAddress);
            var publicKey = ECC.ECPoint.DecodePoint(pubBytes, ecdCurve);
            var secret = GetSharedSecret(privateKey, publicKey);
            var payload = Encrypt(message, secret);
            return new SharedSecret(curve, publicAddress, payload);
        }

        public static byte[] Encrypt<T>(T message, PhantasmaKeys privateKey, ECC.ECPoint publicKey)
        {
            var secret = GetSharedSecret(privateKey, publicKey);
            return Encrypt(message, secret);
        }

        public static byte[] Encrypt<T>(T message, byte[] key)
        {
            var inputBytes = Serialization.Serialize(message);

            byte[] initVectorBytes = Encoding.UTF8.GetBytes(initVector);

            PasswordDeriveBytes password = new PasswordDeriveBytes(key, null);
            byte[] keyBytes = password.GetBytes(keysize / 8);
            RijndaelManaged symmetricKey = new RijndaelManaged();
            symmetricKey.Mode = CipherMode.CBC;
            ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes);
            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            cryptoStream.Write(inputBytes, 0, inputBytes.Length);
            cryptoStream.FlushFinalBlock();
            byte[] cipherTextBytes = memoryStream.ToArray();
            memoryStream.Close();
            cryptoStream.Close();
            return cipherTextBytes;            
        }

        public T Decrypt<T>(PhantasmaKeys privateKey)
        {
            var curve = this.Curve.GetCurve();
            var pubBytes = ECDsaSignature.ExtractPublicKeyFromAddress(this.Address);
            var publicKey = ECC.ECPoint.DecodePoint(pubBytes, curve);
            return Decrypt<T>(this.Payload, privateKey, publicKey);
        }

        public static T Decrypt<T>(byte[] message, PhantasmaKeys privateKey, ECC.ECPoint publicKey)
        {
            var secret = GetSharedSecret(privateKey, publicKey);
            return Decrypt<T>(message, secret);
        }

        public static T Decrypt<T>(byte[] input, byte[] key)
        {
            byte[] initVectorBytes = Encoding.UTF8.GetBytes(initVector);
            PasswordDeriveBytes password = new PasswordDeriveBytes(key, null);
            byte[] keyBytes = password.GetBytes(keysize / 8);
            RijndaelManaged symmetricKey = new RijndaelManaged();
            symmetricKey.Mode = CipherMode.CBC;
            ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes);
            MemoryStream memoryStream = new MemoryStream(input);
            CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            byte[] plainTextBytes = new byte[input.Length];
            int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
            memoryStream.Close();
            cryptoStream.Close();

            return Serialization.Unserialize<T>(plainTextBytes);
        }

        public void SerializeData(BinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }

        public void UnserializeData(BinaryReader reader)
        {
            throw new System.NotImplementedException();
        }
    }

}