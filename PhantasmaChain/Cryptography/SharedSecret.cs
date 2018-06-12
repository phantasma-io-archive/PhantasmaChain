using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace Phantasma.Cryptography
{
    public static class SharedSecret
    {
        private static X9ECParameters curve = SecNamedCurves.GetByName("secp256r1");
        private static ECDomainParameters domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

        public static byte[] GetSharedSecret(byte[] localPrivateKeyBytes, byte[] remotePublicKeyBytes)
        {
            var curve = NistNamedCurves.GetByName("P-256");
            var dom = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
            ECKeyParameters privateKeyParameters = new ECPrivateKeyParameters(new BigInteger(1, localPrivateKeyBytes), dom);

            var q = curve.Curve.DecodePoint(new byte[] { 0x04 }.Concat(remotePublicKeyBytes).ToArray());

            ECKeyParameters publicKeyParameters = new ECPublicKeyParameters(q, dom);

            var agreement = new ECDHBasicAgreement();

            agreement.Init(privateKeyParameters);

            using (var sha = SHA256.Create())
            {
                // CalculateAgreement returns a BigInteger, whose length is variable, and bits are not whitened, so hash it.
                var temp = agreement.CalculateAgreement(publicKeyParameters).ToByteArray();
                return sha.ComputeHash(temp);
            }
        }

        public static byte[] Encrypt(string text, byte[] localPrivateKeyBytes, byte[] remotePublicKeyBytes)
        {
            var secret = GetSharedSecret(localPrivateKeyBytes, remotePublicKeyBytes);
            return Encrypt(text, secret);
        }

        public static string Decrypt(byte[] input, byte[] localPrivateKeyBytes, byte[] remotePublicKeyBytes)
        {
            var secret = GetSharedSecret(localPrivateKeyBytes, remotePublicKeyBytes);
            return Decrypt(input, secret);
        }

        public static byte[] Encrypt(string text, byte[] key)
        {
            byte[] iv = new byte[16];
            AesEngine engine = new AesEngine();
            CbcBlockCipher blockCipher = new CbcBlockCipher(engine); //CBC
            PaddedBufferedBlockCipher cipher = new PaddedBufferedBlockCipher(blockCipher); //Default scheme is PKCS5/PKCS7
            KeyParameter keyParam = new KeyParameter(key);
            ParametersWithIV keyParamWithIV = new ParametersWithIV(keyParam, iv, 0, 16);

            var inputBytes = Encoding.UTF8.GetBytes(text);
            cipher.Init(true, keyParamWithIV);
            byte[] outputBytes = new byte[cipher.GetOutputSize(inputBytes.Length)];
            int length = cipher.ProcessBytes(inputBytes, outputBytes, 0);
            cipher.DoFinal(outputBytes, length); //Do the final block
            return outputBytes;
        }

        public static string Decrypt(byte[] input, byte[] key)
        {
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
            return Encoding.UTF8.GetString(comparisonBytes);
        }
    }

}
