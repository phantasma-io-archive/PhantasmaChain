using System;
using Phantasma.Cryptography.Hashing;
using Phantasma.Numerics;
using Phantasma.Core;

namespace Phantasma.Cryptography.EdDSA
{
    internal class Ed25519Operations
    {
        public static void crypto_sign_keypair(byte[] pk, int pkoffset, byte[] sk, int skoffset, byte[] seed, int seedoffset)
        {
            GroupElementP3 A;
            int i;

            Array.Copy(seed, seedoffset, sk, skoffset, 32);
            byte[] h = SHA512.Hash(sk, skoffset, 32);//ToDo: Remove alloc
            ScalarOperations.sc_clamp(h, 0);

            GroupOperations.ge_scalarmult_base(out A, h, 0);
            GroupOperations.ge_p3_tobytes(pk, pkoffset, ref A);

            for (i = 0; i < 32; ++i)
            {
                sk[skoffset + 32 + i] = pk[pkoffset + i];
            }

            CryptoExtensions.Wipe(h);
        }

        public static bool VerifySignature(
            byte[] sig, int sigoffset,
            byte[] m, int moffset, int mlen,
            byte[] pk, int pkoffset)
        {
            byte[] h;
            byte[] checkr = new byte[32];
            GroupElementP3 A;
            GroupElementP2 R;

            if ((sig[sigoffset + 63] & 224) != 0) return false;
            if (GroupOperations.ge_frombytes_negate_vartime(out A, pk, pkoffset) != 0)
                return false;

            var hasher = new SHA512();
            hasher.Update(sig, sigoffset, 32);
            hasher.Update(pk, pkoffset, 32);
            hasher.Update(m, moffset, mlen);
            h = hasher.Finish();

            ScalarOperations.sc_reduce(h);

            var sm32 = new byte[32];//todo: remove allocation
            Array.Copy(sig, sigoffset + 32, sm32, 0, 32);
            GroupOperations.ge_double_scalarmult_vartime(out R, h, ref A, sm32);
            GroupOperations.ge_tobytes(checkr, 0, ref R);
            var result = CryptoExtensions.ConstantTimeEquals(checkr, 0, sig, sigoffset, 32);
            CryptoExtensions.Wipe(h);
            CryptoExtensions.Wipe(checkr);
            return result;
        }

        public static void GenerateSignature(
            byte[] sig, int sigoffset,
            byte[] m, int moffset, int mlen,
            byte[] sk, int skoffset)
        {
            byte[] az;
            byte[] r;
            byte[] hram;
            GroupElementP3 R;
            var hasher = new SHA512();
            {
                hasher.Update(sk, skoffset, 32);
                az = hasher.Finish();
                ScalarOperations.sc_clamp(az, 0);

                hasher.Init();
                hasher.Update(az, 32, 32);
                hasher.Update(m, moffset, mlen);
                r = hasher.Finish();

                ScalarOperations.sc_reduce(r);
                GroupOperations.ge_scalarmult_base(out R, r, 0);
                GroupOperations.ge_p3_tobytes(sig, sigoffset, ref R);

                hasher.Init();
                hasher.Update(sig, sigoffset, 32);
                hasher.Update(sk, skoffset + 32, 32);
                hasher.Update(m, moffset, mlen);
                hram = hasher.Finish();

                ScalarOperations.sc_reduce(hram);
                var s = new byte[32];//todo: remove allocation
                Array.Copy(sig, sigoffset + 32, s, 0, 32);
                ScalarOperations.sc_muladd(s, hram, az, r);
                Array.Copy(s, 0, sig, sigoffset + 32, 32);
                CryptoExtensions.Wipe(s);
            }
        }
    }
}
