using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Blockchain;
using Phantasma.Domain;

namespace Phantasma.Simulator
{
    public struct SimulatorChainSwap
    {
        public Hash hash;
        public string platformName;
        public Address sourceAddress;
        public string symbol;
        public decimal amount;
    }

    public class OracleSimulator : OracleReader
    {
        private static List<SimulatorChainSwap> _swaps = new List<SimulatorChainSwap>();

        public OracleSimulator(Nexus nexus) : base(nexus)
        {

        }

        public static void CleanUp()
        {
            _swaps.Clear();
        }

        public static Hash SimulateExternalTransaction(string platformName, byte platformID, byte[] publicKey, string symbol, decimal amount)
        {
            var sourceAddress = Address.FromInterop(platformID, publicKey);
            return SimulateExternalTransaction(platformName, sourceAddress, symbol, amount);
        }

        public static Hash SimulateExternalTransaction(string platformName, Address sourceAddress, string symbol, decimal amount)
        {
            var swap = new SimulatorChainSwap()
            {
                hash = Hash.FromString(platformName + sourceAddress.Text + symbol + amount),
                symbol = symbol,
                platformName = platformName,
                sourceAddress = sourceAddress,
                amount = amount
            };

            _swaps.Add(swap);
            return swap.hash;
        }

        protected override byte[] PullData(string url)
        {
            throw new OracleException("invalid oracle url: " + url);
        }

        protected override InteropBlock PullPlatformBlock(string platformName, string chainName, Hash hash)
        {
            throw new OracleException($"unknown block for {platformName}.{chainName} : {hash}");
        }

        protected override InteropTransaction PullPlatformTransaction(string platformName, string chainName, Hash hash)
        {
            foreach (var swap in _swaps)
            if (swap.platformName == platformName && chainName == DomainSettings.RootChainName && swap.hash == hash)
            {
                var info = Nexus.GetPlatformInfo(platformName);
                var platformAddress = info.InteropAddress;

                var token = Nexus.GetTokenInfo(swap.symbol);
                var amount = UnitConversion.ToBigInteger(swap.amount, token.Decimals);

                return new InteropTransaction()
                {
                    Platform = platformName,
                    Hash = hash,
                    Events = new Event[]
                    {
                        new Event(EventKind.TokenSend, swap.sourceAddress, "swap", Serialization.Serialize(new TokenEventData(swap.symbol, amount, platformAddress))),
                        new Event(EventKind.TokenReceive, platformAddress, "swap", Serialization.Serialize(new TokenEventData(swap.symbol, amount, platformAddress)))
                    }
                };
            }

            throw new OracleException($"unknown transaction for {platformName}.{chainName} : {hash}");
        }

        protected override decimal PullPrice(string baseSymbol)
        {
            // some dummy values, only really used in the test suite ...
            decimal price;
            switch (baseSymbol)
            {
                case "SOUL": price = 100; break;
                case "KCAL": price = 20; break;
                case "NEO": price = 2000; break;
                case "GAS": price = 500; break;
                case "ETH": price = 40000; break;
                case "BTC": price = 800000; break;
                default: throw new OracleException("Unknown token: "+baseSymbol);
            }

            price /= 1000m;
            return price;
        }
    }

}
