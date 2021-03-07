using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Phantasma.Numerics;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Cryptography.ECC;

namespace Phantasma.Pay.Chains
{
    public class BitcoinWallet: CryptoWallet
    {
        private List<Unspent> _unspents = new List<Unspent>();

        private const byte OP_HASH160 = 0xa9;
        private const byte OP_EQUAL = 0x87;

        public struct Unspent
        {
            public BigInteger index;
            public decimal amount;
            public string script;
        }

        public BitcoinWallet(PhantasmaKeys keys) : base(keys)
        {
        }

        public const string BitcoinPlatform = "bitcoin";
        public override string Platform => BitcoinPlatform;

        public override void MakePayment(string symbol, decimal amount, string targetAddress, Action<bool> callback)
        {
            if (_unspents.Count <= 0)
            {
                callback(false);
                return;
            }

            decimal totalMoving = 0;
            var unspentList = new List<Unspent>();

            foreach (var unspent in _unspents)
            {
                totalMoving += unspent.amount;
                if (totalMoving >= amount)
                {
                    break;
                }
            }

            // not enough funds
            if (totalMoving < amount)
            {
                callback(false);
                return;
            }

            var number = UnitConversion.ToBigInteger(amount, 8);


            decimal change = totalMoving - amount;
        }

        private byte[] CalculatePublicKeyScript(string address)
        {
            var temp = address.Base58CheckDecode().Skip(1).ToArray();
            return ByteArrayUtils.ConcatBytes(new byte[] { OP_HASH160, 0x14 }, ByteArrayUtils.ConcatBytes(temp, new byte[] { OP_EQUAL }));
        }

        public override void SyncBalances(Action<bool> callback)
        {
            _balances.Clear();
            _unspents.Clear();

            var url = "https://blockchain.info/rawaddr/" + this.Address;
            JSONRequest(url, (root) =>
            {
                if (root == null)
                {
                    callback(false);
                    return;
                }

                decimal btcBalance = 0;

                var txNode = root["txs"];
                foreach (var node in txNode.Children)
                {
                    var outputsNode = node["out"];
                    foreach (var outputNode in outputsNode.Children)
                    {
                        var addr = outputNode.GetString("addr");
                        if (addr != this.Address)
                        {
                            continue;
                        }

                        bool spent = outputNode.GetBool("spent");
                        if (spent == false)
                        {
                            var unspent = new Unspent();
                            unspent.index = BigInteger.Parse(outputNode.GetString("tx_index"));
                            unspent.script = outputNode.GetString("script");
                            var temp = BigInteger.Parse(outputNode.GetString("value"));
                            unspent.amount = UnitConversion.ToDecimal(temp, 8);
                            btcBalance += unspent.amount;
                            _unspents.Add(unspent);
                        }
                    }
                }

                _balances.Add(new WalletBalance("BTC", btcBalance));

                callback(true);
            });            
        }

        protected override string DeriveAddress(PhantasmaKeys keys)
        {
            var publicKey = ECDsa.GetPublicKey(keys.PrivateKey, true, ECDsaCurve.Secp256k1);

            var bytes = ByteArrayUtils.ConcatBytes(new byte[] { 0 }, publicKey.Sha256().RIPEMD160());

            return bytes.Base58CheckEncode();
        }

        public override IEnumerable<CryptoCurrencyInfo> GetCryptoCurrencyInfos()
        {
            yield return new CryptoCurrencyInfo("BTC", "Bitcoin", 8, BitcoinPlatform, CryptoCurrencyCaps.Balance);
            yield break;
        }
    }
}
