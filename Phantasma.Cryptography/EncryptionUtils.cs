using Phantasma.Storage;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace Phantasma.Cryptography
{
    public static class EncryptionUtils
    {
        private const string initVector = "pemgail9uzpgzl88";
        private const int keysize = 256;
        public static readonly ECC.ECCurve Curve = ECC.ECCurve.Secp256r1;

        public static byte[] GetSharedSecret(KeyPair local, ECC.ECPoint remote)
        {
            var secret = (remote * local.PrivateKey).EncodePoint(true);
            return secret.Sha256();
        }

        public static byte[] Encrypt<T>(T message, KeyPair local, ECC.ECPoint remote)
        {
            var secret = GetSharedSecret(local, remote);
            return Encrypt(message, secret);
        }

        public static T Decrypt<T>(byte[] input, KeyPair local, ECC.ECPoint remote)
        {
            var secret = GetSharedSecret(local, remote);
            return Decrypt<T>(input, secret);
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
            
            /*
            byte[] iv = new byte[16];
            AesEngine engine = new AesEngine();
            CbcBlockCipher blockCipher = new CbcBlockCipher(engine); //CBC
            PaddedBufferedBlockCipher cipher = new PaddedBufferedBlockCipher(blockCipher); //Default scheme is PKCS5/PKCS7
            KeyParameter keyParam = new KeyParameter(key);
            ParametersWithIV keyParamWithIV = new ParametersWithIV(keyParam, iv, 0, 16);

            var inputBytes = Serialization.Serialize(message);

            cipher.Init(true, keyParamWithIV);
            byte[] outputBytes = new byte[cipher.GetOutputSize(inputBytes.Length)];
            int length = cipher.ProcessBytes(inputBytes, outputBytes, 0);
            cipher.DoFinal(outputBytes, length); //Do the final block
            return outputBytes;
            */
            throw new System.NotImplementedException();
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
            /*
            byte[] iv = new byte[16];
            AesEngine engine = new AesEngine();
            CbcBlockCipher blockCipher = new CbcBlockCipher(engine); //CBC
            PaddedBufferedBlockCipher cipher = new PaddedBufferedBlockCipher(blockCipher); //Default scheme is PKCS5/PKCS7
            KeyParameter keyParam = new KeyParameter(key);
            ParametersWithIV keyParamWithIV = new ParametersWithIV(keyParam, iv, 0, 16);

            cipher.Init(false, keyParamWithIV);
            byte[] comparisonBytes = new byte[cipher.GetOutputSize(input.Length)];
            var length = cipher.ProcessBytes(input, comparisonBytes, 0);
            cipher.DoFinal(comparisonBytes, length); //Do the final block       
            */
            throw new System.NotImplementedException();
        }

    }

}