using System;
using System.IO;
using System.Linq;
using Phantasma.Numerics;

namespace Phantasma.Cryptography.ECC
{
    public class ECDsa
    {
        private readonly byte[] privateKey;
        private readonly ECPoint publicKey;
        private readonly ECCurve curve;

        public ECDsa(byte[] privateKey, ECCurve curve)
            : this(curve.G * privateKey)
        {
            this.privateKey = privateKey;
        }

        public ECDsa(ECPoint publicKey)
        {
            this.publicKey = publicKey;
            this.curve = publicKey.Curve;
        }

        private static BigInteger CalculateE(BigInteger n, byte[] message)
        {
            int messageBitLength = message.Length * 8;
            BigInteger trunc = BigInteger.FromSignedArray(message.Reverse().Concat(new byte[1]).ToArray());
            if (n.GetBitLength() < messageBitLength)
            {
                trunc >>= messageBitLength - n.GetBitLength();
            }
            return trunc;
        }

        public byte[] GenerateSignature(byte[] message)
        {
            if (privateKey == null) throw new InvalidOperationException();
            BigInteger e = CalculateE(curve.N, message);
            BigInteger d = BigInteger.FromSignedArray(privateKey.Reverse().Concat(new byte[1]).ToArray());
            BigInteger r, s;
            var rng = new Random();
            do
            {
                BigInteger k;
                do
                {
                    do
                    {
                        k = rng.NextBigInteger(curve.N.GetBitLength());
                    }
                    while (k.Sign== 0 || k.CompareTo(curve.N) >= 0);
                    ECPoint p = ECPoint.Multiply(curve.G, k);
                    BigInteger x = p.X.Value;
                    r = x.Mod(curve.N);
                }
                while (r.Sign== 0);
                s = (k.ModInverse(curve.N) * (e + d * r)).Mod(curve.N);
                if (s > curve.N / 2)
                {
                    s = curve.N - s;
                }
            }
            while (s.Sign== 0);
            return EncodeSignatureDER(r, s);
        }

        private byte[] EncodeSignatureDER(BigInteger r, BigInteger s)
        {
            if (r.Sign< 1 || s.Sign< 1 || r.CompareTo(curve.N) >= 0 || s.CompareTo(curve.N) >= 0)
            {
                return null;
            }

            var rBytes = r.ToSignedByteArray().Reverse().ToArray();
            var sBytes = s.ToSignedByteArray().Reverse().ToArray();

            using (var stream = new MemoryStream()) {
                using (var writer = new BinaryWriter(stream))
                {
                    var lenZ = (byte)(rBytes.Length + sBytes.Length + 4);
                    writer.Write((byte)0x30);
                    writer.Write((byte)lenZ); // len(z)
                    writer.Write((byte)2);
                    writer.Write((byte)rBytes.Length); // len(r)
                    writer.Write(rBytes);
                    writer.Write((byte)2);
                    writer.Write((byte)sBytes.Length); // len(s)
                    writer.Write(sBytes);
                    writer.Write((byte)1); // hashtype
                }

                return stream.ToArray();
            }
        }

        private static ECPoint SumOfTwoMultiplies(ECPoint P, BigInteger k, ECPoint Q, BigInteger l)
        {
            int m = Math.Max(k.GetBitLength(), l.GetBitLength());
            ECPoint Z = P + Q;
            ECPoint R = P.Curve.Infinity;
            for (int i = m - 1; i >= 0; --i)
            {
                R = R.Twice();
                if (k.TestBit(i))
                {
                    if (l.TestBit(i))
                        R = R + Z;
                    else
                        R = R + P;
                }
                else
                {
                    if (l.TestBit(i))
                        R = R + Q;
                }
            }
            return R;
        }

        public static bool VerifySignature(byte[] message, byte[] sig, ECCurve curve, ECPoint publicKey)
        {
            using (var stream = new MemoryStream(sig))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var header = reader.ReadByte(); //0x30
                    reader.ReadByte(); // lenz

                    var typeR = reader.ReadByte(); // int type
                    if (typeR != 2)
                    {
                        return false;
                    }

                    var lenR = reader.ReadByte();
                    var bytesR = reader.ReadBytes(lenR).Reverse().ToArray();

                    var typeS =reader.ReadByte(); // int type
                    if (typeS != 2)
                    {
                        return false;
                    }

                    var lenS = reader.ReadByte();
                    var bytesS = reader.ReadBytes(lenS).Reverse().ToArray();

                    var R = BigInteger.FromSignedArray(bytesR);
                    var S = BigInteger.FromSignedArray(bytesS);
                    return VerifySignature(message, R, S, curve, publicKey);
                }
            }
        }

        public static bool VerifySignature(byte[] message, BigInteger r, BigInteger s, ECCurve curve, ECPoint publicKey)
        {
            if (r.Sign< 1 || s.Sign< 1 || r.CompareTo(curve.N) >= 0 || s.CompareTo(curve.N) >= 0)
                return false;
            BigInteger e = CalculateE(curve.N, message);
            BigInteger c = s.ModInverse(curve.N);
            BigInteger u1 = (e * c).Mod(curve.N);
            BigInteger u2 = (r * c).Mod(curve.N);
            ECPoint point = SumOfTwoMultiplies(curve.G, u1, publicKey, u2);
            BigInteger v = point.X.Value.Mod(curve.N);
            return v.Equals(r);
        }
    }
}
