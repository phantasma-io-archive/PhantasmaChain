using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Cryptography.ECC;
using Phantasma.Numerics;

namespace Phantasma.Cryptography
{
    public static class CryptoExtensions
    {
        public static byte[] AESGenerateIV(int vectorSize)
        {
            var ivBytes = new byte[vectorSize];
            var secRandom = new Org.BouncyCastle.Security.SecureRandom();
            secRandom.NextBytes(ivBytes);

            return ivBytes;
        }

        public static byte[] AESGCMDecrypt(byte[] data, byte[] key, byte[] iv)
        {
            var keyParamWithIV = new Org.BouncyCastle.Crypto.Parameters.ParametersWithIV(new Org.BouncyCastle.Crypto.Parameters.KeyParameter(key), iv, 0, 16);

            var cipher = Org.BouncyCastle.Security.CipherUtilities.GetCipher("AES/GCM/NoPadding");
            cipher.Init(false, keyParamWithIV);

            return cipher.DoFinal(data);
        }

        public static byte[] AESGCMEncrypt(byte[] data, byte[] key, byte[] iv)
        {
            var keyParamWithIV = new Org.BouncyCastle.Crypto.Parameters.ParametersWithIV(new Org.BouncyCastle.Crypto.Parameters.KeyParameter(key), iv, 0, 16);

            var cipher = Org.BouncyCastle.Security.CipherUtilities.GetCipher("AES/GCM/NoPadding");
            cipher.Init(true, keyParamWithIV);

            return cipher.DoFinal(data);
        }

        public static byte[] AESGCMDecrypt(byte[] data, byte[] key)
        {
            byte[] iv;
            byte[] encryptedData;

            using (var stream = new System.IO.MemoryStream(data))
            {
                using (var reader = new System.IO.BinaryReader(stream))
                {
                    iv = reader.ReadBytes(16);
                    encryptedData = reader.ReadBytes(data.Length - 16);
                }
            }
            
            var keyParamWithIV = new Org.BouncyCastle.Crypto.Parameters.ParametersWithIV(new Org.BouncyCastle.Crypto.Parameters.KeyParameter(key), iv, 0, 16);

            var cipher = Org.BouncyCastle.Security.CipherUtilities.GetCipher("AES/GCM/NoPadding");
            cipher.Init(false, keyParamWithIV);

            return cipher.DoFinal(encryptedData);
        }

        public static byte[] AESGCMEncrypt(byte[] data, byte[] key)
        {
            byte[] iv = AESGenerateIV(16);
            var keyParamWithIV = new Org.BouncyCastle.Crypto.Parameters.ParametersWithIV(new Org.BouncyCastle.Crypto.Parameters.KeyParameter(key), iv, 0, 16);

            var cipher = Org.BouncyCastle.Security.CipherUtilities.GetCipher("AES/GCM/NoPadding");
            cipher.Init(true, keyParamWithIV);

            var encryptedData = cipher.DoFinal(data);

            using (var stream = new System.IO.MemoryStream())
            {
                using (var writer = new System.IO.BinaryWriter(stream))
                {
                    writer.Write(iv);
                    writer.Write(encryptedData);
                }

                return stream.ToArray();
            }
        }

        public static byte[] Base58CheckDecode(this string input)
        {
            byte[] buffer = Base58.Decode(input);
            if (buffer.Length < 4) throw new FormatException();
            byte[] expected_checksum = buffer.Sha256(0, (uint)(buffer.Length - 4)).SHA256();
            expected_checksum = expected_checksum.Take(4).ToArray();
            var src_checksum = buffer.Skip(buffer.Length - 4).ToArray();

            Throw.If(!src_checksum.SequenceEqual(expected_checksum), "WIF checksum failed");
            return buffer.Take(buffer.Length - 4).ToArray();
        }

        public static string Base58CheckEncode(this byte[] data)
        {
            byte[] checksum = data.SHA256().SHA256();
            byte[] buffer = new byte[data.Length + 4];
            Array.Copy(data, 0, buffer, 0, data.Length);
            ByteArrayUtils.CopyBytes(checksum, 0, buffer, data.Length, 4); 
            return Base58.Encode(buffer);
        }

        public static byte[] RIPEMD160(this IEnumerable<byte> value)
        {
            return new RIPEMD160().ComputeHash(value.ToArray());
        }

        public static byte[] SHA256(this IEnumerable<byte> value)
        {
            return new Hashing.SHA256().ComputeHash(value.ToArray());
        }

        public static byte[] Sha256(this string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            return bytes.SHA256();
        }

        public static byte[] Sha256(this byte[] value)
        {
            return new Hashing.SHA256().ComputeHash(value, 0, (uint)value.Length);
        }

        public static byte[] Sha256(this byte[] value, uint offset, uint count)
        {
            return new Hashing.SHA256().ComputeHash(value, offset, count);
        }

        public static byte[] FromDER(byte[] sig)
        {
            var decoder = new Org.BouncyCastle.Asn1.Asn1InputStream(sig);
            var seq = decoder.ReadObject() as Org.BouncyCastle.Asn1.DerSequence;
            if (seq == null || seq.Count != 2)
                throw new FormatException("Invalid DER Signature");
            var R = ((Org.BouncyCastle.Asn1.DerInteger)seq[0]).Value.ToByteArrayUnsigned();
            var S = ((Org.BouncyCastle.Asn1.DerInteger)seq[1]).Value.ToByteArrayUnsigned();

            byte[] concatenated = new byte[R.Length + S.Length];
            System.Buffer.BlockCopy(R, 0, concatenated, 0, R.Length);
            System.Buffer.BlockCopy(S, 0, concatenated, R.Length, S.Length);

            return concatenated;
        }

        public static byte[] SignECDsa(byte[] message, byte[] prikey, byte[] pubkey, ECDsaCurve curve)
        {
            var signer = Org.BouncyCastle.Security.SignerUtilities.GetSigner("SHA256withECDSA");
            Org.BouncyCastle.Asn1.X9.X9ECParameters ecCurve;
            switch (curve)
            {
                case Phantasma.Cryptography.ECC.ECDsaCurve.Secp256k1:
                    ecCurve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                    break;
                default:
                    ecCurve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256r1");
                    break;
            }
            var dom = new Org.BouncyCastle.Crypto.Parameters.ECDomainParameters(ecCurve.Curve, ecCurve.G, ecCurve.N, ecCurve.H);
            var privateKeyParameters = new Org.BouncyCastle.Crypto.Parameters.ECPrivateKeyParameters(new Org.BouncyCastle.Math.BigInteger(1, prikey), dom);

            signer.Init(true, privateKeyParameters);
            signer.BlockUpdate(message, 0, message.Length);
            var sig = signer.GenerateSignature();

            return FromDER(sig);
        }

        public static bool VerifySignatureECDsa(byte[] message, byte[] signature, byte[] pubkey, ECDsaCurve curve)
        {
            var signer = Org.BouncyCastle.Security.SignerUtilities.GetSigner("SHA256withECDSA");
            Org.BouncyCastle.Asn1.X9.X9ECParameters ecCurve;
            switch (curve)
            {
                case Phantasma.Cryptography.ECC.ECDsaCurve.Secp256k1:
                    ecCurve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
                    break;
                default:
                    ecCurve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256r1");
                    break;
            }
            var dom = new Org.BouncyCastle.Crypto.Parameters.ECDomainParameters(ecCurve.Curve, ecCurve.G, ecCurve.N, ecCurve.H);

            Org.BouncyCastle.Crypto.Parameters.ECPublicKeyParameters publicKeyParameters;
            if (pubkey.Length == 33)
                publicKeyParameters = new Org.BouncyCastle.Crypto.Parameters.ECPublicKeyParameters(dom.Curve.DecodePoint(pubkey), dom);
            else
                publicKeyParameters = new Org.BouncyCastle.Crypto.Parameters.ECPublicKeyParameters(dom.Curve.CreatePoint(new Org.BouncyCastle.Math.BigInteger(1, pubkey.Take(pubkey.Length / 2).ToArray()), new Org.BouncyCastle.Math.BigInteger(1, pubkey.Skip(pubkey.Length / 2).ToArray())), dom);

            signer.Init(false, publicKeyParameters);
            signer.BlockUpdate(message, 0, message.Length);

            // We convert from concatenated "raw" R + S format to DER format that Bouncy Castle uses.
            signature = new Org.BouncyCastle.Asn1.DerSequence(
                // first 32 bytes is "R" number
                new Org.BouncyCastle.Asn1.DerInteger(new Org.BouncyCastle.Math.BigInteger(1, signature.Take(32).ToArray())),
                // last 32 bytes is "S" number
                new Org.BouncyCastle.Asn1.DerInteger(new Org.BouncyCastle.Math.BigInteger(1, signature.Skip(32).ToArray())))
                .GetDerEncoded();

            return signer.VerifySignature(signature);
        }

        internal static BigInteger NextBigInteger(int sizeInBits)
        {
            if (sizeInBits < 0)
                throw new ArgumentException("sizeInBits must be non-negative");
            if (sizeInBits == 0)
                return 0;

            var b = Entropy.GetRandomBytes(sizeInBits / 8 + 1);

            if (sizeInBits % 8 == 0)
                b[b.Length - 1] = 0;
            else
                b[b.Length - 1] &= (byte)((1 << sizeInBits % 8) - 1);

            return BigInteger.FromUnsignedArray(b, isPositive: true);
        }

        public static BigInteger GenerateInteger(this HMACDRBG rng, BigInteger max, int securityParameter = 64)
        {
            // The simple modular method from the NIST SP800-90A recommendation
            Throw.If(securityParameter < 64, "Given security parameter, " + securityParameter + ", is too low.");

            var bytesToRepresent = max.ToSignedByteArray().Length;
            var bytes = new byte[bytesToRepresent + securityParameter / 8 + 1];
            rng.GetBytes(bytes);
            bytes[bytes.Length - 1] = 0;
            return BigInteger.FromSignedArray(bytes) % max;
        }
    }
}
