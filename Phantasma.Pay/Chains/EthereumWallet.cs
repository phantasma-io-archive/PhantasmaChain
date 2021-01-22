using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Cryptography.ECC;
using Phantasma.Cryptography.Hashing;
using Phantasma.Numerics;
using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

namespace Phantasma.Pay.Chains
{
    public class EthereumWallet : CryptoWallet
    {
        public const string EthereumPlatform = "ethereum";
        public const byte EthereumID = 2;

        public EthereumWallet(PhantasmaKeys keys) : base(keys)
        {
        }

        public override string Platform => EthereumPlatform;

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

        protected override string DeriveAddress(PhantasmaKeys keys)
        {
            var publicKey = ECDsa.GetPublicKey(keys.PrivateKey, false, ECDsaCurve.Secp256k1).Skip(1).ToArray(); ;

            var kak = SHA3Keccak.CalculateHash(publicKey);
            return "0x" + Base16.Encode(kak.Skip(12).ToArray());
        }

        public static Address EncodeAddress(string addressText)
        {
            Throw.If(!IsValidAddress(addressText), "invalid ethereum address");
            var input = addressText.Substring(2);
            var bytes = Base16.Decode(input);

            var pubKey = new byte[33];
            ByteArrayUtils.CopyBytes(bytes, 0, pubKey, 0, bytes.Length);
            return Cryptography.Address.FromInterop(EthereumID, pubKey);
        }

        public static bool IsValidAddress(string addressText)
        {
            return addressText.StartsWith("0x") && addressText.Length == 42;
        }

        public static string DecodeAddress(Address address)
        {
            if (!address.IsInterop)
            {
                throw new Exception("not an interop address");
            }

            byte platformID;
            byte[] data;
            address.DecodeInterop(out platformID, out data);

            if (platformID != EthereumID)
            {
                throw new Exception("not a Ethereum interop address");
            }

            return $"0x{Base16.Encode(data.Take(20).ToArray())}";
        }

        public override IEnumerable<CryptoCurrencyInfo> GetCryptoCurrencyInfos()
        {
            yield return new CryptoCurrencyInfo("ETH", "Ether", 8, EthereumPlatform, CryptoCurrencyCaps.Balance);
            yield return new CryptoCurrencyInfo("DAI", "Dai", 8, EthereumPlatform, CryptoCurrencyCaps.Balance);
            yield break;
        }
    }
}
