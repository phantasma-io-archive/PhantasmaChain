using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Cryptography.ECC;
using Phantasma.Cryptography.Hashing;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Pay
{
    public enum WalletKind
    {
        Phantasma,
        Bitcoin,
        Ethereum,
        Neo,
    }

    public class SubWallet
    {
        public readonly WalletKind Kind;
        public readonly string Address;
        
        public SubWallet(KeyPair keys, WalletKind kind)
        {
            this.Kind = kind;

            switch (kind)
            {
                case WalletKind.Phantasma:  Address = keys.Address.Text; break;
                case WalletKind.Ethereum: Address = DeriveEthereumAddress(keys.PrivateKey); break;
                case WalletKind.Bitcoin: Address = DeriveBitcoinAddress(keys.PrivateKey); break;
                case WalletKind.Neo: Address = DeriveNeoAddress(keys.PrivateKey); break;
                default: throw new Exception("Unsupported wallet kind: " + kind);
            }
        }

        private string DeriveBitcoinAddress(byte[] privateKey)
        {
            ECPoint pKey = ECCurve.Secp256k1.G * privateKey;

            var publicKey = pKey.EncodePoint(true);

            var bytes = ByteArrayUtils.ConcatBytes( new byte[] { 0}, publicKey.Sha256().RIPEMD160());

            return bytes.Base58CheckEncode();
        }

        private string DeriveEthereumAddress(byte[] privateKey)
        {
            ECPoint pKey = ECCurve.Secp256k1.G * privateKey;

            //var bytes = pKey.EncodePoint(true).ToArray();

            var publicKey = pKey.EncodePoint(false).Skip(1).ToArray();

            var kak = SHA3Keccak.CalculateHash(publicKey);
            return "0x" + Base16.Encode(kak.Skip(12).ToArray());
        }

        private string DeriveNeoAddress(byte[] privateKey)
        {
            ECPoint pKey = ECCurve.Secp256r1.G * privateKey;

            var bytes = pKey.EncodePoint(true);

            var script = new byte[bytes.Length + 2];
            script[0] = 0x21;// OpCode.PUSHBYTES33;
            Array.Copy(bytes, 0, script, 1, bytes.Length);
            script[script.Length - 1] = 0xAC; // OpCode.CHECKSIG;

            var scriptHash = script.Sha256().RIPEMD160();

            //this.PublicKey = pKey.EncodePoint(false).Skip(1).ToArray();

            byte[] data = new byte[21];
            data[0] = 23;
            Buffer.BlockCopy(scriptHash.ToArray(), 0, data, 1, 20);
            return data.Base58CheckEncode();
        }
    }

    public class Wallet
    {
        private Dictionary<WalletKind, SubWallet> _wallets = new Dictionary<WalletKind, SubWallet>();
        private KeyPair keys;

        public Wallet(KeyPair keys)
        {
            this.keys = keys;
        }

        public string GetAddress(WalletKind kind)
        {
            if (_wallets.ContainsKey(kind))
            {
                return _wallets[kind].Address;
            }

            var wallet = new SubWallet(keys, kind);
            _wallets[kind] = wallet;
            return wallet.Address;
        }
    }
}
