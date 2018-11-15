using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.Core.Utils;

namespace Phantasma.Cryptography.Ring
{
    // This scheme is the "realization 2" from "Linkable Ring Signatures: Security Models and New Schemes"
    // by Joseph K. Liu and Duncan S. Wong - Computational Science and Its Applications–ICCSA 2005

    public class RingSignature : Signature, ILinkableSignature
    {
        static readonly byte[] hash1String = Encoding.UTF8.GetBytes("~~~Hash one");
        static readonly byte[] hash2String = Encoding.UTF8.GetBytes("+++Hash two");

        public static GroupParameters GroupParameters = KnownGroupParameters.RFC5114_2_3_256;
        private static HMACDRBG rng = new HMACDRBG();
        private static HMACDRBG drbg = new HMACDRBG();
        private static Modular mod = new Modular(GroupParameters.Prime);

        public readonly BigInteger Y0, S;
        public readonly BigInteger[] C;

        public override SignatureKind Kind => SignatureKind.Ring;

        public RingSignature(BigInteger Y0, BigInteger S, BigInteger[] C) 
        {
            this.Y0 = Y0;
            this.S = S;
            this.C = C;
        }

        public bool IsLinked(ILinkableSignature other)
        {
            return Y0.Equals(((RingSignature)other).Y0);
        }

        public override bool Verify(byte[] message, IEnumerable<Address> addresses)
        {
            var publicKeys = addresses.Select(x => new BigInteger(x.PublicKey)).ToArray();
            return this.VerifySignature(message, publicKeys);
        }

        /// <summary>
        /// I'm not 100% sure that this is the best way to get this hash
        /// </summary>
        private static BigInteger Hash2(byte[] data)
        {
            drbg.Reseed(data, hash2String);
            var x = drbg.GenerateInteger(GroupParameters.SubgroupSize);
            return mod.Pow(GroupParameters.Generator, x);
        }

        private static BigInteger Hash1(byte[] data)
        {
            drbg.Reseed(data, hash1String);
            return drbg.GenerateInteger(GroupParameters.SubgroupSize);
        }

        private static byte[] ConcatInts(byte[] prefix = null, params BigInteger[] ints)
        {
            var L = new List<byte>();
            if (prefix != null)
                L.AddRange(prefix);

            foreach (var key in ints)
                L.AddRange(key.ToByteArray());

            return L.ToArray();
        }

        public static RingSignature GenerateSignature(byte[] message, BigInteger[] publicKeys, BigInteger privateKey, int identity)
        {
            var r = rng.GenerateInteger(GroupParameters.SubgroupSize);
            var c = new BigInteger[publicKeys.Length];

            var b = BigInteger.Zero;

            for (int i = 0; i < publicKeys.Length; ++i)
                if (i != identity)
                {
                    c[i] = rng.GenerateInteger(GroupParameters.SubgroupSize);
                    b = (b + c[i]).Mod(GroupParameters.SubgroupSize);
                }

            var x = (BigInteger[])publicKeys.Clone();
            x[identity] = GroupParameters.Generator;
            c[identity] = r;

            var a = mod.Pow(x, c);

            var L = ConcatInts(null, publicKeys);
            var h = Hash2(L);
            var y0 = mod.Pow(h, privateKey);
            var prefix = ByteArrayUtils.ConcatBytes(ConcatInts(L, y0), message);

            var h1 = Hash1(ConcatInts(prefix, a, mod.Pow(new[] { h, y0 }, new[] { r, b })));
            c[identity] = (h1 - b).Mod(GroupParameters.SubgroupSize);

            var s = (r - c[identity] * privateKey).Mod(GroupParameters.SubgroupSize);

            return new RingSignature(y0, s, c);
        }

        public static RingKeyPair GenerateKeyPair(KeyPair keyPair)
        {
            var mod = new Modular(GroupParameters.Prime);
            var privateKey = new BigInteger(keyPair.PrivateKey);
            var publicKey = mod.Pow(GroupParameters.Generator, privateKey);
            return new RingKeyPair(privateKey, publicKey);
        }

        public bool VerifySignature(byte[] message, BigInteger[] publicKeys)
        {
            int[,][] cache = null;
            var a = (mod.Pow(publicKeys, this.C, ref cache) * mod.Pow(GroupParameters.Generator, this.S)).Mod(GroupParameters.Prime);

            return VerifyA(message, publicKeys, a);
        }


        /// <summary>
        /// This is a cached version of the verifier, designed for mass verification of signatures coming from the same list of keys.
        /// If you keep the cache, subsequent verifications will be faster. This only matters when the number of keys is high (>50).
        /// </summary>
        /// <param name="message">The message which was signed</param>
        /// <param name="signature">The signature</param>
        /// <param name="keyCache">The cache, containing the list of public keys for which the signature was generated</param>
        /// <returns></returns>
        public bool VerifySignature(byte[] message, MultiExponentiation keyCache)
        {
            var a = (keyCache.Pow(this.C) * mod.Pow(GroupParameters.Generator, this.S)).Mod(GroupParameters.Prime);

            return VerifyA(message, keyCache.Bases, a);
        }

        private bool VerifyA(byte[] message, BigInteger[] publicKeys, BigInteger a)
        {
            var b = BigInteger.Zero;
            for (int i = 0; i < this.C.Length; ++i)
                b = (b + this.C[i]).Mod(GroupParameters.SubgroupSize);

            var L = ConcatInts(null, publicKeys);
            var h = Hash2(L);
            var prefix = ByteArrayUtils.ConcatBytes(ConcatInts(L, this.Y0), message);

            var h1 = Hash1(ConcatInts(prefix, a, mod.Pow(new[] { h, this.Y0 }, new[] { this.S, b })));

            return h1.Equals(b);
        }

    }

}
