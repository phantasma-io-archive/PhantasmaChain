using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Cryptography.ECC;
using Phantasma.Cryptography.Hashing;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Pay.Chains
{
    public class EOSWallet: CryptoWallet
    {
        public EOSWallet(KeyPair keys, Action<string, Action<string>> urlFetcher) : base(keys, urlFetcher)
        {
        }

        public override WalletKind Kind => WalletKind.EOS;

        public override void MakePayment(string symbol, decimal amount, string targetAddress, Action<bool> callback)
        {
            throw new NotImplementedException();
        }

        public override void SyncBalances(Action<bool> callback)
        {
            callback(false);
        }

        protected override string DeriveAddress(KeyPair keys)
        {
            ECPoint pKey = ECCurve.Secp256k1.G * keys.PrivateKey;

            var publicKey = pKey.EncodePoint(true);
            
            var data = publicKey.ToArray();

            byte[] checksum = data.RIPEMD160();

            byte[] buffer = new byte[data.Length + 4];
            Array.Copy(data, 0, buffer, 0, data.Length);
            ByteArrayUtils.CopyBytes(checksum, 0, buffer, data.Length, 4);
            return "EOS" + Base58.Encode(buffer);
        }

        public override IEnumerable<CryptoCurrencyInfo> GetCryptoCurrencyInfos()
        {
            yield return new CryptoCurrencyInfo("EOS", "EOS", 18, WalletKind.EOS);
            yield break;
        }

    }
}
