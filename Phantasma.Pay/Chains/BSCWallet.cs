using Phantasma.Core;
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
    public class BSCWallet : CryptoWallet
    {
        public const string BSCPlatform = "bsc";
        public const byte BSCID = 3;
        public BSCWallet(PhantasmaKeys keys) : base(keys)
        {
        }

        public override string Platform => BSCPlatform;

        public override void MakePayment(string symbol, decimal amount, string targetAddress, Action<bool> callback)
        {
            throw new NotImplementedException();
        }

        public override void SyncBalances(Action<bool> callback)
        {
            throw new NotImplementedException();
        }

        protected override string DeriveAddress(PhantasmaKeys keys)
        {
            ECPoint pKey = ECCurve.Secp256k1.G * keys.PrivateKey;

            var publicKey = pKey.EncodePoint(false).Skip(1).ToArray();

            var kak = SHA3Keccak.CalculateHash(publicKey);
            return "0x" + Base16.Encode(kak.Skip(12).ToArray());
        }

        public static Address EncodeAddress(string addressText)
        {
            Throw.If(!IsValidAddress(addressText), "invalid bsc address");
            var input = addressText.Substring(2);
            var bytes = Base16.Decode(input);

            var pubKey = new byte[33];
            ByteArrayUtils.CopyBytes(bytes, 0, pubKey, 0, bytes.Length);
            return Cryptography.Address.FromInterop(BSCID, pubKey);
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

            if (platformID != BSCID)
            {
                throw new Exception("not a BSC interop address");
            }

            return $"0x{Base16.Encode(data.Take(20).ToArray())}";
        }

        public override IEnumerable<CryptoCurrencyInfo> GetCryptoCurrencyInfos()
        {
            yield return new CryptoCurrencyInfo("BNB", "BNB", 8, BSCPlatform, CryptoCurrencyCaps.Balance);
            yield break;
        }
    }
}
