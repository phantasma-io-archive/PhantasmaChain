using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Cryptography.ECC;
using Phantasma.Cryptography.Hashing;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Pay.Chains
{
    public class EthereumWallet: CryptoWallet
    {
        public static readonly string EthereumPlatform = "ethereum";

        public EthereumWallet(KeyPair keys, Action<string, Action<string>> urlFetcher) : base(keys, urlFetcher)
        {
        }

        public override WalletKind Kind => WalletKind.Ethereum;

        public override void MakePayment(string symbol, decimal amount, string targetAddress, Action<bool> callback)
        {
            throw new NotImplementedException();
        }

        public override void SyncBalances(Action<bool> callback)
        {
            _balances.Clear();

            var url = $"https://api.blockcypher.com/v1/eth/main/addrs/{this.Address}/balance";
            JSONRequest(url, (root) =>
            {
                if (root == null)
                {
                    callback(false);
                    return;
                }

                var temp = root.GetString("balance");
                var n = BigInteger.Parse(temp);
                var amount = UnitConversion.ToDecimal(n, 18);
                _balances.Add(new WalletBalance("ETH", amount));
                callback(true);
            });
        }

        protected override string DeriveAddress(KeyPair keys)
        {
            ECPoint pKey = ECCurve.Secp256k1.G * keys.PrivateKey;

            var publicKey = pKey.EncodePoint(false).Skip(1).ToArray();

            var kak = SHA3Keccak.CalculateHash(publicKey);
            return "0x" + Base16.Encode(kak.Skip(12).ToArray());
        }

        public static Address EncodeAddress(string addressText)
        {
            Throw.If(!IsValidAddress(addressText), "invalid ethereum address");
            var input = addressText.Substring(2);
            var bytes = Base16.Decode(input);
            return Cryptography.Address.EncodeInterop(EthereumPlatform, bytes);
        }

        private static bool IsValidAddress(string addressText)
        {
            return addressText.StartsWith("0x") && addressText.Length == 42;
        }

        public static string DecodeAddress(Address address)
        {
            if (!address.IsInterop)
            {
                throw new Exception("not an interop address");
            }

            string platformName;
            byte[] data;
            address.DecodeInterop(out platformName, out data, 20);

            if (platformName != EthereumPlatform)
            {
                throw new Exception("not a Ethereum interop address");
            }

            return $"0x{Base16.Encode(data)}";
        }

        public override IEnumerable<CryptoCurrencyInfo> GetCryptoCurrencyInfos()
        {
            yield return new CryptoCurrencyInfo("ETH", "Ether", 8, WalletKind.Ethereum, CryptoCurrencyCaps.Balance);
            yield break;
        }
    }
}
