using System;
using Phantasma.Cryptography;
using Phantasma.Pay.Chains;

namespace Phantasma.Pay
{
    public static class WalletUtils
    {
        public static void DecodeChainAndAddress(Address source, out string chainName, out string address)
        {
            byte[] bytes;

            source.DecodeInterop(out chainName, out bytes, 0);

            switch (chainName)
            {
                case "NEO":
                    address = NeoWallet.DecodeAddress(source);
                    break;

                default:
                    throw new NotImplementedException($"cannot decode addresses for {chainName} chain");
            }
        }

        public static Address EncodeAddress(string source, string chainName)
        {
            switch (chainName)
            {
                case "NEO":
                    return NeoWallet.EncodeAddress(source);

                default:
                    throw new NotImplementedException($"cannot encode addresses for {chainName} chain");
            }
        }
    }
}
