using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain
{
    public struct InteropChainInfo
    {
        public string Name;
        public string Symbol; // for fuel
        public Address Address;
        //public flags;
    }

    public struct InteropBlock
    {
        public string ChainName;
        public Hash Hash;
        public Hash[] Transactions;
    }

    public struct InteropTransaction
    {
        public string ChainName;
        public Hash Hash;
        public Event[] Events;
    }

    public static class InteropUtils
    {
        public static KeyPair GenerateInteropKeys(KeyPair genesisKeys, string chainName)
        {
            var temp = chainName + "!" + genesisKeys.ToWIF();
            var privateKey = CryptoExtensions.Sha256(temp);
            var key = new KeyPair(privateKey);
            return key;
        }
    }
}
