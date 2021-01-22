namespace Phantasma.Cryptography.ECC
{
    public static class ECDsa
    {
        public static byte[] GetPublicKey(byte[] privateKey, bool compressed, ECDsaCurve curve)
        {
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

            var d = new Org.BouncyCastle.Math.BigInteger(1, privateKey);
            var q = dom.G.Multiply(d);

            var publicParams = new Org.BouncyCastle.Crypto.Parameters.ECPublicKeyParameters(q, dom);
            return publicParams.Q.GetEncoded(compressed);
        }
    }
}