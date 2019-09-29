using System;
using Phantasma.Cryptography;
using Phantasma.Pay.Chains;

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
    }
}
