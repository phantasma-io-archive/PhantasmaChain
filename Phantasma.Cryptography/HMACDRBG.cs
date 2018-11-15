using System;
using Phantasma.Core.Utils;

namespace Phantasma.Cryptography
{
    // Deterministic random bit generator (DRBG) based on a keyed-hash message authentification code (HMAC)
    // This is based on NIST SP800-90A recommendation, using HMAC_DRBG scheme with HMACSHA512 from .Net framework
    public class HMACDRBG
    {
        public const int HighestSecurityStrength = 256;
        public const int SeedLengthBytes = 888 / 8;
        const int OutlenBytes = 512 / 8;

        byte[] v = new byte[OutlenBytes];
        byte[] key = new byte[OutlenBytes];
        int reseedCounter, securityStrength;

        // providing the entropy in the constructor is against the recommendation, but this is what I need,
        // since this is to be used as a basis of a hash function, and not as an actual random number generator
        public HMACDRBG(byte[] seed = null, int securityStrength = HighestSecurityStrength, byte[] personalizationString = null)
        {
            if (securityStrength > HighestSecurityStrength)
                throw new ArgumentException("Requested security strength " + securityStrength + " is not supported");

            if (securityStrength <= 112)
                securityStrength = 112;
            else if (securityStrength <= 128)
                securityStrength = 128;
            else if (securityStrength <= 192)
                securityStrength = 192;
            else
                securityStrength = 256;

            this.securityStrength = securityStrength;

            if (seed != null)
                Reseed(seed, personalizationString);
        }

        public void Reseed(byte[] seed, byte[] personalizationString = null)
        {
            if (personalizationString != null && personalizationString.Length > 512)
                throw new ArgumentException("Personalization string is too long");

            var minEntropyBytes = securityStrength * 3 / 2 / 8;
            if (seed.Length < minEntropyBytes)
                throw new ArgumentException("Provided seed of " + seed.Length + " bytes is not long enough for requested security strength of "
                  + securityStrength);

            Instantiate(seed, personalizationString);
        }

        private void Instantiate(byte[] seed, byte[] personalizationString)
        {
            reseedCounter = 1;
            var seedMaterial = personalizationString == null ? seed : seed.ConcatBytes(personalizationString); 
            for (int i = 0; i < OutlenBytes; ++i)
            {
                key[i] = 0;
                v[i] = 0x01;
            }

            Update(seedMaterial);
        }

        private void Update(byte[] providedData)
        {
            var data = Concat(v, 0, providedData);
            key = HMAC512.ComputeHash(key, data);
            v = HMAC512.ComputeHash(key, v);
            if (providedData != null)
            {
                data = Concat(v, 0x01, providedData);
                key = HMAC512.ComputeHash(key, data);
                v = HMAC512.ComputeHash(key, v);
            }
        }

        private byte[] Concat(byte[] v, byte p, byte[] providedData)
        {
            byte[] res = new byte[v.Length + 1 + (providedData == null ? 0 : providedData.Length)];
            Array.Copy(v, 0, res, 0, v.Length); // TODO Buffer.BlockCopy
            res[v.Length] = p;
            if (providedData != null)
                Array.Copy(providedData, 0, res, v.Length + 1, providedData.Length); // TODO Buffer.BlockCopy

            return res;
        }

        public int SecurityStrength { get { return securityStrength; } }

        public void GetBytes(byte[] data)
        {
            if (data.Length * 8 > 7500)
                throw new ArgumentException("Too many bytes requested: " + data.Length);

            if (reseedCounter >= 10000)
                throw new ArgumentException("A reseed is required");

            int idx = 0;
            while (idx < data.Length)
            {
                v = HMAC512.ComputeHash(key, v);
                Array.Copy(v, 0, data, idx, Math.Min(v.Length, data.Length - idx)); // TODO Buffer.BlockCopy
                idx += v.Length;
            }

            Update(null);
            ++reseedCounter;
        }
    }
}
