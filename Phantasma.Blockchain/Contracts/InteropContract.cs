using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Context;
using System.Diagnostics.Tracing;
using System.Linq;

namespace Phantasma.Blockchain.Contracts
{
    public enum InteropTransferStatus
    {
        Unknown,
        Pending,
        Confirmed
    }

    public struct InteropWithdraw
    {
        public Hash hash;
        public Address destination;
        public string transferSymbol;
        public BigInteger transferAmount;
        public string feeSymbol;
        public BigInteger feeAmount;
        public Timestamp timestamp;
    }

    public struct InteropHistory
    {
        public Hash sourceHash;
        public string sourcePlatform;
        public string sourceChain;
        public Address sourceAddress;

        public Hash destHash;
        public string destPlatform;
        public string destChain;
        public Address destAddress;

        public string symbol;
        public BigInteger value;
    }

    public sealed class InteropContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Interop;

        private StorageMap _platformHashes;
        private StorageList _withdraws;

        internal StorageMap _swapMap; //<Hash, Collection<InteropHistory>>
        internal StorageMap _historyMap; //<Address, Collection<Hash>>

        public InteropContract() : base()
        {
        }


        // This contract call associates a new swap address to a specific platform. 
        // Existing swap addresses will still be considered valid for receiving funds
        // However nodes should start sending assets from this new address when doing swaps going from Phantasma to this platform
        // For all purposes, any transfer coming from another swap address of same platform into this one shall not being considered a "swap"        
        public void RegisterAddress(Address from, string platform, Address localAddress, string externalAddress)
        {
            Runtime.Expect(from == Runtime.GenesisAddress, "only genesis allowed");
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");
            Runtime.Expect(localAddress.IsInterop, "swap target must be interop address");

            Runtime.RegisterPlatformAddress(platform, localAddress, externalAddress);
        }

        public void SettleTransaction(Address from, string platform, string chain, Hash hash)
        {
            PlatformSwapAddress[] swapAddresses;

            if (platform != DomainSettings.PlatformName)
            {
                Runtime.Expect(Runtime.PlatformExists(platform), "unsupported platform");
                var platformInfo = Runtime.GetPlatformByName(platform);
                swapAddresses = platformInfo.InteropAddresses;
            }
            else
            {
                swapAddresses = null;
            }

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "must be user address");

            var chainHashes = _platformHashes.Get<string, StorageMap>(platform);
            Runtime.Expect(!chainHashes.ContainsKey<Hash>(hash), "hash already seen");

            var interopTx = Runtime.ReadTransactionFromOracle(platform, chain, hash);

            Runtime.Expect(interopTx.Hash == hash, "unxpected hash");

            int swapCount = 0;

            foreach (var transfer in interopTx.Transfers)
            {
                var count = _withdraws.Count();
                var index = -1;
                for (int i = 0; i < count; i++)
                {
                    var entry = _withdraws.Get<InteropWithdraw>(i);
                    if (entry.destination == transfer.destinationAddress && entry.transferAmount == transfer.Value && entry.transferSymbol == transfer.Symbol)
                    {
                        index = i;
                        break;
                    }
                }

                if (index >= 0)
                {
                    Runtime.Expect(Runtime.TokenExists(transfer.Symbol), "invalid token");
                    var token = this.Runtime.GetToken(transfer.Symbol);

                    if (Runtime.ProtocolVersion >= 4)
                    {
                        if (token.Flags.HasFlag(TokenFlags.Fungible))
                        {
                            Runtime.Expect(transfer.Value > 0, "amount must be positive and greater than zero");
                        }
                        else
                        {
                            Runtime.Expect(Runtime.NFTExists(transfer.Symbol, transfer.Value), $"nft {transfer.Value} must exist");
                        }
                    }
                    else
                    {
                        Runtime.Expect(transfer.Value > 0, "amount must be positive and greater than zero");
                        Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
                    }

                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Swappable), "transfer token must be swappable");

                    var withdraw = _withdraws.Get<InteropWithdraw>(index);
                    _withdraws.RemoveAt(index);

                    if (Runtime.ProtocolVersion >= 3)
                    {
                        var org = Runtime.GetOrganization(DomainSettings.ValidatorsOrganizationName);
                        Runtime.Expect(org.IsMember(from), $"{from.Text} is not a validator node");
                        Runtime.TransferTokens(withdraw.feeSymbol, this.Address, from, withdraw.feeAmount);
                    }
                    else
                    {
                        Runtime.TransferTokens(withdraw.feeSymbol, this.Address, transfer.sourceAddress, withdraw.feeAmount);
                    }

                    RegisterHistory(hash, withdraw.hash, DomainSettings.PlatformName, Runtime.Chain.Name, transfer.sourceAddress, hash, platform, chain, withdraw.destination, transfer.Symbol, transfer.Value);
                    swapCount++;
                }
                else
                if (swapAddresses != null)
                {
                    foreach (var entry in swapAddresses)
                    {
                        if (transfer.destinationAddress == entry.LocalAddress)
                        {
                            Runtime.Expect(!transfer.sourceAddress.IsNull, "invalid source address");

                            // Here we detect if this transfer occurs between two swap addresses
                            var isInternalTransfer = Runtime.IsPlatformAddress(transfer.sourceAddress);

                            if (!isInternalTransfer)
                            {
                                Runtime.Expect(Runtime.TokenExists(transfer.Symbol), "invalid token");
                                var token = this.Runtime.GetToken(transfer.Symbol);

                                if (Runtime.ProtocolVersion >= 4)
                                {
                                    if (token.Flags.HasFlag(TokenFlags.Fungible))
                                    {
                                        Runtime.Expect(transfer.Value > 0, "amount must be positive and greater than zero");
                                    }
                                    else
                                    {
                                        Runtime.Expect(Runtime.NFTExists(transfer.Symbol, transfer.Value), $"nft {transfer.Value} must exist");
                                    }
                                }
                                else
                                {
                                    Runtime.Expect(transfer.Value > 0, "amount must be positive and greater than zero");
                                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
                                }

                                
                                Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
                                Runtime.Expect(token.Flags.HasFlag(TokenFlags.Swappable), "transfer token must be swappable");

                                Runtime.Expect(transfer.interopAddress.IsUser, "invalid destination address");

                                Runtime.SwapTokens(platform, transfer.sourceAddress, Runtime.Chain.Name, transfer.interopAddress, transfer.Symbol, transfer.Value);


                                if (Runtime.ProtocolVersion >= 4 && !token.Flags.HasFlag(TokenFlags.Fungible))
                                {
                                    var externalNft = Runtime.ReadNFTFromOracle(platform, transfer.Symbol, transfer.Value);
                                    var ram = Serialization.Serialize(externalNft);

                                    var localNft = Runtime.ReadToken(transfer.Symbol, transfer.Value);
                                    Runtime.WriteToken(from, transfer.Symbol, transfer.Value, ram); // TODO "from" here might fail due to contract triggers, review this later
                                }

                                var settleHash = Runtime.Transaction.Hash;
                                RegisterHistory(settleHash, hash, platform, chain, transfer.sourceAddress, settleHash, DomainSettings.PlatformName, Runtime.Chain.Name, transfer.interopAddress, transfer.Symbol, transfer.Value);

                                swapCount++;
                            }

                            break;
                        }
                    }
                }
            }

            Runtime.Expect(swapCount > 0, "nothing to settle");
            chainHashes.Set<Hash, Hash>(hash, Runtime.Transaction.Hash);
            Runtime.Notify(EventKind.ChainSwap, from, new TransactionSettleEventData(hash, platform, chain));
        }

        // send to external chain
        public void WithdrawTokens(Address from, Address to, string symbol, BigInteger value)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(from.IsUser, "source must be user address");
            Runtime.Expect(to.IsInterop, "destination must be interop address");

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");

            var transferTokenInfo = this.Runtime.GetToken(symbol);
            Runtime.Expect(transferTokenInfo.Flags.HasFlag(TokenFlags.Transferable), "transfer token must be transferable");
            Runtime.Expect(transferTokenInfo.Flags.HasFlag(TokenFlags.Swappable), "transfer token must be swappable");

            if (Runtime.ProtocolVersion >= 4)
            {
                if (transferTokenInfo.Flags.HasFlag(TokenFlags.Fungible))
                {
                    Runtime.Expect(value > 0, "amount must be positive and greater than zero");
                }
                else
                {
                    Runtime.Expect(Runtime.NFTExists(symbol, value), $"nft {value} must be exist");
                }
            }
            else
            {
                Runtime.Expect(value > 0, "amount must be positive and greater than zero");
                Runtime.Expect(transferTokenInfo.Flags.HasFlag(TokenFlags.Fungible), "transfer token must be fungible");
            }

            byte platformID;
            byte[] dummy;
            to.DecodeInterop(out platformID, out dummy);
            Runtime.Expect(platformID > 0, "invalid platform ID");
            var platform = Runtime.GetPlatformByIndex(platformID);
            Runtime.Expect(platform != null, "invalid platform");

            int interopIndex = -1;
            for (int i=0; i<platform.InteropAddresses.Length; i++)
            {
                if (platform.InteropAddresses[i].LocalAddress == to)
                {
                    interopIndex = i;
                    break;
                }
            }

            var platformTokenHash = Runtime.GetTokenPlatformHash(symbol, platform);
            Runtime.Expect(platformTokenHash != Hash.Null, $"invalid foreign token hash {platformTokenHash}");

            Runtime.Expect(interopIndex == -1, "invalid target address");

            var feeSymbol = platform.Symbol;
            Runtime.Expect(Runtime.TokenExists(feeSymbol), "invalid fee token");

            var feeTokenInfo = this.Runtime.GetToken(feeSymbol);
            Runtime.Expect(feeTokenInfo.Flags.HasFlag(TokenFlags.Fungible), "fee token must be fungible");
            Runtime.Expect(feeTokenInfo.Flags.HasFlag(TokenFlags.Transferable), "fee token must be transferable");

            BigInteger feeAmount;
            if (Runtime.ProtocolVersion >= 3)
            {
                feeAmount = Runtime.ReadFeeFromOracle(platform.Name); // fee is in fee currency (gwei for eth, gas for neo)
            }
            else
            {
                var basePrice = Runtime.ReadFeeFromOracle(platform.Name);
                feeAmount = Runtime.GetTokenQuote(DomainSettings.FiatTokenSymbol, feeSymbol, basePrice);
            }

            Runtime.Expect(feeAmount > 0, "fee is too small");

            var feeBalance = Runtime.GetBalance(feeSymbol, from);
            if (feeBalance < feeAmount)
            {
                Runtime.CallNativeContext(NativeContractKind.Swap, "SwapReverse", from, DomainSettings.FuelTokenSymbol, feeSymbol, feeAmount);

                feeBalance = Runtime.GetBalance(feeSymbol, from);
                Runtime.Expect(feeBalance >= feeAmount, $"missing {feeSymbol} for interop swap");
            }

            Runtime.TransferTokens(feeSymbol, from, this.Address, feeAmount);

            Runtime.SwapTokens(Runtime.Chain.Name, from, platform.Name, to, symbol, value);

            var withdraw = new InteropWithdraw()
            {
                destination = to,
                transferAmount = value,
                transferSymbol = symbol,
                feeAmount = feeAmount,
                feeSymbol = feeSymbol,
                hash = Runtime.Transaction.Hash,
                timestamp = Runtime.Time
            };
            _withdraws.Add<InteropWithdraw>(withdraw);
        }

        public Hash GetSettlement(string platformName, Hash hash)
        {
            var chainHashes = _platformHashes.Get<string, StorageMap>(platformName);
            if (chainHashes.ContainsKey<Hash>(hash))
            {
                return chainHashes.Get<Hash, Hash>(hash);
            }

            return Hash.Null;
        }

        public InteropTransferStatus GetStatus(string platformName, Hash hash)
        {
            var chainHashes = _platformHashes.Get<string, StorageMap>(platformName);
            if (chainHashes.ContainsKey<Hash>(hash))
            {
                return InteropTransferStatus.Confirmed;
            }

            var count = _withdraws.Count();
            for (int i = 0; i < count; i++)
            {
                var entry = _withdraws.Get<InteropWithdraw>(i);
                if (entry.hash == hash)
                {
                    return InteropTransferStatus.Pending;
                }
            }


            return InteropTransferStatus.Unknown;
        }

        #region SWAP HISTORY
        private void RegisterHistory(Hash swapHash, Hash sourceHash, string sourcePlatform, string sourceChain, Address sourceAddress,  Hash destHash,  string destPlatform, string destChain,  Address destAddress, string symbol, BigInteger value) 
        {
            var entry = new InteropHistory()
            {
                sourceAddress = sourceAddress,
                sourceHash = sourceHash,
                sourcePlatform = sourcePlatform,
                sourceChain = sourceChain,
                destAddress = destAddress,
                destHash = destHash,
                destPlatform = destPlatform,
                destChain = destChain,
                symbol = symbol,
                value = value,
            };

            _swapMap.Set<Hash, InteropHistory>(swapHash, entry);

            AppendToHistoryMap(swapHash, sourceAddress);
            AppendToHistoryMap(swapHash, destAddress);
        }

        private void AppendToHistoryMap(Hash swapHash, Address target)
        {
            var list = _historyMap.Get<Address, StorageList>(target);
            list.Add<Hash>(swapHash);
        }

        public InteropHistory[] GetSwapsForAddress(Address address)
        {
            var list = _historyMap.Get<Address, StorageList>(address);
            var count = (int)list.Count();

            var result = new InteropHistory[count];
            for (int i=0; i<count; i++)
            {
                var hash = list.Get<Hash>(i);
                result[i] = _swapMap.Get<Hash, InteropHistory>(hash);
            }

            return result;
        }
        #endregion
    }
}
