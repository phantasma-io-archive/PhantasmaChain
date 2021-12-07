using System;
using System.IO;
using Phantasma.Cryptography;
using Phantasma.Pay.Chains;
using Phantasma.Core.Utils;
using Phantasma.Numerics;

namespace Phantasma.Pay
{
    public static class WalletUtils
    {
        /*
        public static void DecodePlatformAndAddress(Address source, out byte platformID, out string address)
        {
            byte[] bytes;

            source.DecodeInterop(out platform, out bytes, 0);

            switch (platform)
            {
                case NeoWallet.NeoPlatform:
                    address = NeoWallet.DecodeAddress(source);
                    break;

                case EthereumWallet.EthereumPlatform:
                    address = EthereumWallet.DecodeAddress(source);
                    break;
                    
                default:
                    throw new NotImplementedException($"cannot decode addresses for {platform} chain");
            }
        }*/

        /* public static Address EncodeAddress(string source, string chainName)
         {
             switch (chainName)
             {
                 case NeoWallet.NeoPlatform:
                     return NeoWallet.EncodeAddress(source);

                 case EthereumWallet.EthereumPlatform:
                     return NeoWallet.EncodeAddress(source);

                 default:
                     throw new NotImplementedException($"cannot encode addresses for {chainName} chain");
             }
         }*/

        public static string GetPlatformByID(byte platformID)
        {
            switch (platformID)
            {
                case 0: return PhantasmaWallet.PhantasmaPlatform;
                case 1: return NeoWallet.NeoPlatform;
                case 2: return EthereumWallet.EthereumPlatform;
                default:  throw new NotImplementedException();
            }
        }

        public static bool ValidateSignedData(Address addr, string signedData, string random, string data)
        {
            var msgData = Base16.Decode(data);
            var randomBytes = Base16.Decode(random);
            var signedDataBytes = Base16.Decode(signedData);
            var msgBytes = ByteArrayUtils.ConcatBytes(randomBytes, msgData);
            using (var stream = new MemoryStream(signedDataBytes))
            using (var reader = new BinaryReader(stream))
            {
                var signature = reader.ReadSignature();
                return signature.Verify(msgBytes, addr);
            }
        }
    }
}

