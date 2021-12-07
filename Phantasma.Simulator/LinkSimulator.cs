using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Phantasma.Blockchain;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;

namespace Phantasma.Simulator
{
    public class LinkSimulator : WalletLink
    {
        [Flags]
        public enum PlatformKind
        {
            None = 0x0,
            Phantasma = 0x1,
            Neo = 0x2,
            Ethereum = 0x4,
            BSC = 0x8,
        }

        private Nexus _nexus;
        private string _name;
        private MyAccount _account;
        private int LinkProtocol = 2;

        public LinkSimulator(Nexus Nexus, string name, MyAccount account)
        {
            this._nexus = Nexus;
            this._name = name;
            this._account = account;
        }

        public override string Nexus => _name;

        public override string Name => _nexus.Name;

        protected override WalletStatus Status => WalletStatus.Ready;

        private PlatformKind RequestPlatform(string platform)
        {
            PlatformKind targetPlatform;

            if (!Enum.TryParse<PlatformKind>(platform, true, out targetPlatform))
            {
                return PlatformKind.None;
            }

            if (!_account.CurrentPlatform.HasFlag(targetPlatform))
            {
                return PlatformKind.None;
            }

            if (_account.CurrentPlatform != targetPlatform)
            {
                _account.CurrentPlatform = targetPlatform;
            }

            return targetPlatform;
        }

        protected override void Authorize(string dapp, string token, int version, Action<bool, string> callback)
        {            
            if (version > LinkProtocol)
            {
                callback(false, "unknown Phantasma Link version " + version);
                return;
            }
            
            if (_account.CurrentPlatform != PlatformKind.Phantasma)
                _account.CurrentPlatform = PlatformKind.Phantasma;
            
            var state = Status;
            if (state == null)
            {
                callback(false, "not logged in");
                return;
            }

            var result = true;
                
            //state.RegisterDappToken(dapp, token);
            callback(result, result ? null : "rejected");
        }

        protected override void GetAccount(string platform, Action<Account, string> callback)
        {
            throw new NotImplementedException();
        }

        protected override void InvokeScript(string chain, byte[] script, int id, Action<byte[], string> callback)
        {
            throw new NotImplementedException();
        }

        protected override void SignData(string platform, SignatureKind kind, byte[] data, int id, Action<string, string, string> callback)
        {
            var targetPlatform = RequestPlatform(platform);
            if (targetPlatform == PlatformKind.None)
            {
                callback(null, null, "Unsupported platform: " + platform);
                return;
            }

            var state = Status;
            if (state != WalletStatus.Ready)
            {
                callback(null, null, "not logged in");
                return;
            }

            var account = _account;

            var description = System.Text.Encoding.UTF8.GetString(data);

            var randomValue = new Random().Next(0, int.MaxValue);
            var randomBytes = BitConverter.GetBytes(randomValue);

            var msg = ByteArrayUtils.ConcatBytes(randomBytes, data);
            Cryptography.Signature signature;
            
            switch (kind)
            {
                case SignatureKind.Ed25519:
                    var phantasmaKeys = account.keys;
                    signature = phantasmaKeys.Sign(msg);
                    break;

                case SignatureKind.ECDSA:
                    var ethKeys = new Ethereum.EthereumKey(account.keys.PrivateKey);
                    signature = ethKeys.Sign(msg);
                    break;

                default:
                    callback(null, null, kind + " signatures unsupported");
                    return;
            }

            byte[] sigBytes = null;

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.WriteSignature(signature);
                }

                sigBytes = stream.ToArray();
            }

            var hexSig = Base16.Encode(sigBytes);
            var hexRand = Base16.Encode(randomBytes);

            callback(hexSig, hexRand, null);
        }


        public void forceSignData(string platform, SignatureKind kind, byte[] data, int id, Action<string, string, string> callback)
        {
            SignData(platform, kind, data, id, callback);
        }

        protected override void SignTransaction(string platform, SignatureKind kind, string chain, byte[] script, byte[] payload, int id, Action<Hash, string> callback)
        {
            throw new NotImplementedException();
        }

        protected override void WriteArchive(Hash hash, int blockIndex, byte[] data, Action<bool, string> callback)
        {
            throw new NotImplementedException();
        }
        
        public class MyAccount
        {
            public MyAccount Instance { get; private set; }
            public PhantasmaKeys keys;
            public PlatformKind CurrentPlatform;
            public string token;

            public MyAccount(PhantasmaKeys keys, PlatformKind platform)
            {
                Instance = this;
                this.keys = keys;
                CurrentPlatform = platform;
            }
        }
    }
}
