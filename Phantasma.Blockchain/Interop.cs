using Phantasma.Cryptography;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct InteropBlock
    {
        public string ChainName;
        public Address ChainAddress;
        public Hash Hash;
        public Hash[] Transactions;
    }

    public struct InteropTransaction
    {
        public string ChainName;
        public Address ChainAddress;
        public Hash Hash;
        public Event[] Events;
    }

    public static class InteropUtils
    {
        private static string[] _supportedChains = new string[] { "NEO" };
        public static IEnumerable<string> SupportedChains => _supportedChains;

        private static Dictionary<string, Address> _chainMap = null;
        private static Dictionary<Address, string> _nameMap = null;

        public static bool IsChainSupported(string name)
        {
            return _supportedChains.Contains(name);
        }

        private static void InitMaps()
        {
            foreach (var chainName in _supportedChains)
            {
                var chainAddress = GenerateInteropAddress(chainName);
                _chainMap[chainName] = chainAddress;
                _nameMap[chainAddress] = chainName;
            }
        }

        private static Address GenerateInteropAddress(string blockchainName)
        {
            var temp = "interop." + blockchainName;
            var bytes = temp.Sha256();
            return new Address(bytes);
        }

        public static Address GetInteropAddress(string blockchainName)
        {
            if (_chainMap == null)
            {
                InitMaps();
            }

            if (_chainMap.ContainsKey(blockchainName))
            {
                return _chainMap[blockchainName];
            }

            return Address.Null;
        }

        public static string GetInteropName(Address address)
        {
            if (_nameMap == null)
            {
                InitMaps();
            }

            if (_nameMap.ContainsKey(address))
            {
                return _nameMap[address];
            }

            return null;
        }
    }
}
