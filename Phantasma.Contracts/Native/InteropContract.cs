using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System.Linq;

namespace Phantasma.Contracts.Native
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

    public sealed class InteropContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Interop;

        private StorageList _platforms;

        private StorageMap _hashes;
        private StorageList _withdraws;

        private StorageMap _links;
        private StorageMap _reverseMap;

        public InteropContract() : base()
        {
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

            var chainHashes = _hashes.Get<string, StorageMap>(platform);
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
                    Runtime.Expect(transfer.Value > 0, "amount must be positive and greater than zero");

                    Runtime.Expect(Runtime.TokenExists(transfer.Symbol), "invalid token");
                    var token = this.Runtime.GetToken(transfer.Symbol);
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");

                    var withdraw = _withdraws.Get<InteropWithdraw>(index);
                    _withdraws.RemoveAt<InteropWithdraw>(index);

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

                            Runtime.Expect(transfer.Value > 0, "amount must be positive and greater than zero");

                            Runtime.Expect(Runtime.TokenExists(transfer.Symbol), "invalid token");
                            var token = this.Runtime.GetToken(transfer.Symbol);

                            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
                            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");

                            Runtime.Expect(transfer.interopAddress.IsUser, "invalid destination address");

                            // TODO support NFT
                            Runtime.SwapTokens(platform, transfer.sourceAddress, Runtime.Chain.Name, transfer.interopAddress, transfer.Symbol, transfer.Value, null, null);
                            //Runtime.Notify(EventKind.TokenSwap, destination, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));

                            swapCount++;
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
        public void WithdrawTokens(Address from, Address to, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(from.IsUser, "source must be user address");
            Runtime.Expect(to.IsInterop, "destination must be interop address");

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");

            var transferTokenInfo = this.Runtime.GetToken(symbol);
            Runtime.Expect(transferTokenInfo.Flags.HasFlag(TokenFlags.Transferable), "transfer token must be transferable");
            Runtime.Expect(transferTokenInfo.Flags.HasFlag(TokenFlags.Fungible), "transfer token must be fungible");

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
                Runtime.CallContext("swap", "SwapReverse", from, DomainSettings.FuelTokenSymbol, feeSymbol, feeAmount);

                feeBalance = Runtime.GetBalance(feeSymbol, from);
                Runtime.Expect(feeBalance >= feeAmount, $"missing {feeSymbol} for interop swap");
            }

            Runtime.TransferTokens(feeSymbol, from, this.Address, feeAmount);

            // TODO support NFT
            Runtime.SwapTokens(Runtime.Chain.Name, from, platform.Name, to, symbol, amount, null, null);

            var withdraw = new InteropWithdraw()
            {
                destination = to,
                transferAmount = amount,
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
            var chainHashes = _hashes.Get<string, StorageMap>(platformName);
            if (chainHashes.ContainsKey<Hash>(hash))
            {
                return chainHashes.Get<Hash, Hash>(hash);
            }

            return Hash.Null;
        }

        public InteropTransferStatus GetStatus(string platformName, Hash hash)
        {
            var chainHashes = _hashes.Get<string, StorageMap>(platformName);
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
    }
}
