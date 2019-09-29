using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Core.Log;
using Phantasma.Blockchain.Tokens;
using Phantasma.Storage.Context;
using Phantasma.Domain;
using Phantasma.Contracts.Native;

namespace Phantasma.Blockchain.Swaps
{
    public enum BrokerResult
    {
        Ready,
        Skip,
        Error
    }

    public class TokenSwapService
    {
        private const string SwapHashMapTag = ".swhash";
        private const string SwapAddrListMapTag = ".swadr";

        public readonly KeyPair Keys;
        public readonly Nexus Nexus;
        public readonly Logger Logger;
        public readonly BigInteger MinimumFee;

        private Dictionary<string, ChainInterop> interopMap = new Dictionary<string, ChainInterop>();

        private StorageContext Storage;

        public TokenSwapService(KeyPair swapKey, Nexus nexus, BigInteger minFee, Logger logger)
        {
            this.Keys = swapKey;
            this.Nexus = nexus;
            this.Logger = logger;
            this.MinimumFee = minFee;

            this.Storage = new KeyStoreStorage(nexus.CreateKeyStoreAdapter("swaps"));
        }

        public void AddInterop(ChainInterop interop)
        {
            interopMap[interop.Name] = interop;
        }

        public bool HasSwapWithSourceHash(Hash hash)
        {
            var map = new StorageMap(SwapHashMapTag, this.Storage);
            return map.ContainsKey<Hash>(hash);
        }

        public ChainSwap GetSwapForSourceHash(Hash hash)
        {
            var map = new StorageMap(SwapHashMapTag, this.Storage);
            if (!map.ContainsKey<Hash>(hash))
            {
                throw new SwapException("swap does not exist: " + hash);
            }
            return map.Get<Hash, ChainSwap>(hash);
        }

        public Hash[] GetSwapHashesForAddress(Address address)
        {
            var map = new StorageMap(SwapAddrListMapTag, this.Storage);
            var list = map.Get<Address, StorageList>(address);
            return list.All<Hash>();
        }

        public ChainSwap[] GetPendingSwaps(Address target, ChainSwapStatus status)
        {
            return GetSwapHashesForAddress(target).Select(x => GetSwapForSourceHash(x)).Where(x => x.status == status).ToArray();
        }

        public void Run()
        {
            Thread.Sleep(2000);

            try
            {
                var map = new StorageMap(SwapHashMapTag, this.Storage);

                foreach (var interop in interopMap.Values)
                {
                    var swaps = interop.Update();

                    foreach (var temp in swaps)
                    {
                        var swap = temp;

                        ChainSwapStatus prevStatus;
                        if (map.ContainsKey<Hash>(swap.sourceHash))
                        {
                            var prevSwap = GetSwapForSourceHash(swap.sourceHash);
                            prevStatus = prevSwap.status;

                            switch (prevStatus)
                            {
                                case ChainSwapStatus.Finished:
                                case ChainSwapStatus.Invalid:
                                    swap.status = prevStatus;
                                    break;
                            }
                        }
                        else
                        {
                            prevStatus = ChainSwapStatus.Invalid;
                        }

                        if (swap.status != ChainSwapStatus.Finished)
                        {
                            try
                            {
                                ProcessSwap(map, ref swap);
                            }
                            catch (InteropException e)
                            {
                                swap.status = e.SwapStatus;
                            }
                        }

                        // the first time we see this swap, add it to the address list
                        if (prevStatus == ChainSwapStatus.Invalid)
                        {
                            var addressMap = new StorageMap(SwapAddrListMapTag, this.Storage);
                            var list = addressMap.Get<Address, StorageList>(swap.sourceAddress);
                            list.Add<Hash>(swap.sourceHash);

                            if (!swap.destinationAddress.IsNull)
                            {
                                list = addressMap.Get<Address, StorageList>(swap.destinationAddress);
                                list.Add<Hash>(swap.sourceHash);
                            }
                        }

                        if (prevStatus != swap.status)
                        {
                            map.Set<Hash, ChainSwap>(swap.sourceHash, swap);

                            if (swap.status == ChainSwapStatus.Finished)
                            {
                                Logger.Success($"Swap finished: {swap}");
                            }
                            else
                            if (swap.status != ChainSwapStatus.Invalid)
                            {
                                Logger.Warning($"Swap is waiting for {swap.status.ToString().ToLower()}: {swap}");
                            }
                            else
                            {
                                Logger.Error($"Swap failed: {swap}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Swapper exception: " + e.Message);
                Thread.Sleep(5000);
            }            
        }

        // finds which blockchain interop address matches the supplied address
        public string FindInteropByAddress(Address address)
        {
            foreach (var interop in interopMap.Values)
            {
                if (interop.Name == DomainSettings.PlatformName)
                {
                    continue;
                }

                if (interop.ExternalAddress == address)
                {
                    return interop.Name;
                }
            }

            return null;
        }

        public bool FindTokenByHash(string hashText, out TokenInfo token)
        {
            var hash = Hash.FromUnpaddedHex(hashText);

            foreach (var symbol in Nexus.Tokens)
            {
                var info = Nexus.GetTokenInfo(symbol);
                if (hash.Equals(info.Hash))
                {
                    token = info;
                    return true;
                }
            }

            token = new TokenInfo();
            return false;
        }

        public bool FindTokenBySymbol(string symbol, out TokenInfo token)
        {
            if (Nexus.TokenExists(symbol))
            {
                token = Nexus.GetTokenInfo(symbol);
                return true;
            }

            token = new TokenInfo();
            return false;
        }

        public ChainInterop FindInterop(string platformName)
        {
            if (interopMap.ContainsKey(platformName))
            {
                return interopMap[platformName];
            }

            throw new InteropException("Could not find interop for " + platformName, ChainSwapStatus.Platform);
        }


        private void ProcessSwap(StorageMap map, ref ChainSwap swap)
        {
            if (swap.status == ChainSwapStatus.Finished)
            {
                return;
            }

            ChainInterop sourceInterop = FindInterop(swap.sourcePlatform);

            if (!interopMap.ContainsKey(swap.destinationPlatform))
            {
                throw new InteropException("Unknown interop: " + swap.destinationPlatform, ChainSwapStatus.Platform);
            }

            var destinationInterop = FindInterop(swap.destinationPlatform);

            switch (swap.status)
            {
                case ChainSwapStatus.Link:
                    swap.destinationAddress = LookUpAddress(swap.sourceAddress, DomainSettings.PlatformName);

                    if (swap.destinationAddress.IsNull)
                    {
                        return;
                    }
                    else
                    {
                        // we did not add it before because was unknown, so we add now
                        var addressMap = new StorageMap(SwapAddrListMapTag, this.Storage);
                        var list = addressMap.Get<Address, StorageList>(swap.destinationAddress);
                        list.Add<Hash>(swap.sourceHash);

                        swap.status = ChainSwapStatus.Resettle;
                    }
                    break;

                case ChainSwapStatus.Broker:
                    {
                        Hash brokerHash;

                        var brokerResult = sourceInterop.PrepareBroker(swap, out brokerHash);
                        if (brokerResult == BrokerResult.Error || brokerHash == Hash.Null)
                        {
                            throw new InteropException("Failed broker transaction for swap with hash " + swap.sourceHash, ChainSwapStatus.Broker);
                        }

                        if (brokerResult == BrokerResult.Skip)
                        {
                            swap.status = ChainSwapStatus.Broker;
                        }
                        else
                        {
                            swap.status = ChainSwapStatus.Sending;
                        }
                    }
                    break;

                case ChainSwapStatus.Pending:
                    {
                        if (swap.destinationAddress.IsNull)
                        {
                            swap.status = ChainSwapStatus.Link;
                            return;
                        }

                        var tokenInfo = Nexus.GetTokenInfo(swap.symbol);
                        Logger.Message($"Detected {swap.sourcePlatform} swap: {swap.sourceAddress} sent {UnitConversion.ToDecimal(swap.amount, tokenInfo.Decimals)} {swap.symbol}");

                        if (sourceInterop.Name == DomainSettings.PlatformName)
                        {
                            swap.status = ChainSwapStatus.Broker;
                        }
                        else
                        {
                            swap.status = ChainSwapStatus.Sending;
                        }
                        break;
                    }

                case ChainSwapStatus.Sending:
                    {
                        swap.destinationHash = destinationInterop.ReceiveFunds(swap);
                        if (swap.destinationHash == Hash.Null)
                        {
                            throw new InteropException("Failed destination transaction for swap with hash " + swap.sourceHash, ChainSwapStatus.Receive);
                        }

                        if (sourceInterop.Name == DomainSettings.PlatformName)
                        {
                            swap.status = ChainSwapStatus.Settle;
                        }
                        else
                        {
                            swap.status = ChainSwapStatus.Finished;
                        }

                        break;
                    }

                case ChainSwapStatus.Settle:
                    {
                        Logger.Message($"Waiting for {swap.destinationPlatform} transaction confirmation: " + swap.destinationHash);
                        Thread.Sleep(30 * 1000);
                        
                        var settleHash = sourceInterop.SettleTransaction(swap.destinationHash, swap.destinationPlatform);
                        if (settleHash == Hash.Null)
                        {
                            throw new InteropException("Failed settle transaction for swap with hash " + swap.sourceHash, ChainSwapStatus.Settle);
                        }

                        swap.status = ChainSwapStatus.Finished;
                        break;
                    }

                case ChainSwapStatus.Resettle:
                    {
                        var settleHash = destinationInterop.SettleTransaction(swap.sourceHash, swap.sourcePlatform);
                        if (settleHash == Hash.Null)
                        {
                            throw new InteropException("Failed settle transaction for swap with hash " + swap.sourceHash, ChainSwapStatus.Link);
                        }

                        swap.status = ChainSwapStatus.Finished;
                        break;
                    }
            }

            ProcessSwap(map, ref swap);
        }

        public Address LookUpAddress(Address sourceAddress, string sourcePlatform)
        {
            return Nexus.RootChain.InvokeContract(Nexus.RootStorage, Nexus.InteropContractName, nameof(InteropContract.GetLink), sourceAddress, sourcePlatform).AsAddress();
        }
    }
}
