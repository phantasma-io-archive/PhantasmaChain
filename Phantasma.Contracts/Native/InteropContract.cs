using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System;
using System.Linq;

namespace Phantasma.Contracts.Native
{
    public enum InteropTransferStatus
    {
        Unknown,
        Queued,
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
        public BigInteger collateralAmount;
        public Address broker;
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

        public static BigInteger InteropFeeRacio => 50;

        public InteropContract() : base()
        {
        }

        public void SettleTransaction(Address from, string platform, string chain, Hash hash)
        {
            Runtime.Expect(platform != DomainSettings.PlatformName, "must be external platform");
            Runtime.Expect(Runtime.PlatformExists(platform), "unsupported platform");
            var platformInfo = Runtime.GetPlatformByName(platform);

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "must be user address");

            var chainHashes = _hashes.Get<string, StorageMap>(platform);
            Runtime.Expect(!chainHashes.ContainsKey<Hash>(hash), "hash already seen");

            var interopTx = Runtime.ReadTransactionFromOracle(platform, chain, hash);

            Runtime.Expect(interopTx.Hash == hash, "unxpected hash");

            int swapCount = 0;

            foreach (var transfer in interopTx.Transfers)
            {
                if (transfer.destinationAddress == platformInfo.InteropAddress)
                {
                    Runtime.Expect(!transfer.sourceAddress.IsNull, "invalid source address");

                    Runtime.Expect(transfer.Value > 0, "amount must be positive and greater than zero");

                    Runtime.Expect(Runtime.TokenExists(transfer.Symbol), "invalid token");
                    var token = this.Runtime.GetToken(transfer.Symbol);

                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.External), "token must be external");

                    Runtime.Expect(transfer.interopAddress.IsUser, "invalid destination address");

                    Runtime.SwapTokens(platformInfo.Name, platformInfo.ChainAddress, Runtime.Chain.Name, transfer.interopAddress, transfer.Symbol, transfer.Value);

                    swapCount++;
                }
                else
                if (Runtime.IsPlatformAddress(transfer.sourceAddress))
                {
                    Runtime.Expect(transfer.Value > 0, "amount must be positive and greater than zero");

                    Runtime.Expect(Runtime.TokenExists(transfer.Symbol), "invalid token");
                    var token = this.Runtime.GetToken(transfer.Symbol);
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.External), "token must be external");

                    var count = _withdraws.Count();
                    var index = -1;
                    for (int i=0; i<count; i++)
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
                        var withdraw = _withdraws.Get<InteropWithdraw>(index);
                        Runtime.Expect(withdraw.broker == from, "invalid broker");

                        _withdraws.RemoveAt<InteropWithdraw>(index);

                        Runtime.TransferTokens(withdraw.feeSymbol, this.Address, from, withdraw.feeAmount);

                        swapCount++;
                        break;
                    }
                }
            }

            Runtime.Expect(swapCount > 0, "nothing to settle");
            chainHashes.Set<Hash, Hash>(hash, Runtime.Transaction.Hash);
            Runtime.Notify(EventKind.TransactionSettle, from, new TransactionSettleEventData(hash, platformInfo.Name));
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
            Runtime.Expect(transferTokenInfo.Flags.HasFlag(TokenFlags.External), "transfer token must be external");
            Runtime.Expect(transferTokenInfo.Flags.HasFlag(TokenFlags.Fungible), "transfer token must be fungible");

            byte platformID;
            byte[] dummy;
            to.DecodeInterop(out platformID, out dummy);
            Runtime.Expect(platformID > 0, "invalid platform ID");
            var platform = Runtime.GetPlatformByIndex(platformID);
            Runtime.Expect(platform != null, "invalid platform");
            Runtime.Expect(to != platform.InteropAddress, "invalid target address");

            var feeSymbol = platform.Symbol;
            Runtime.Expect(Runtime.TokenExists(feeSymbol), "invalid fee token");

            var feeTokenInfo = this.Runtime.GetToken(feeSymbol);
            Runtime.Expect(feeTokenInfo.Flags.HasFlag(TokenFlags.Fungible), "fee token must be fungible");
            Runtime.Expect(feeTokenInfo.Flags.HasFlag(TokenFlags.Transferable), "fee token must be transferable");

            var basePrice = UnitConversion.GetUnitValue(DomainSettings.FiatTokenDecimals) / InteropFeeRacio; 
            var feeAmount = Runtime.GetTokenQuote(DomainSettings.FiatTokenSymbol, feeSymbol, basePrice);
            Runtime.Expect(feeAmount > 0, "fee is too small");

            Runtime.TransferTokens(feeSymbol, from, this.Address, feeAmount);
            Runtime.SwapTokens(Runtime.Chain.Name, from, platform.Name, platform.ChainAddress, symbol, amount);

            var collateralAmount = Runtime.GetTokenQuote(symbol, DomainSettings.FuelTokenSymbol, feeAmount);

            var withdraw = new InteropWithdraw()
            {
                destination = to,
                transferAmount = amount,
                transferSymbol = symbol,
                feeAmount = feeAmount,
                feeSymbol = feeSymbol,
                hash = Runtime.Transaction.Hash,
                broker = Address.Null,
                collateralAmount = collateralAmount,
                timestamp = Runtime.Time
            };
            _withdraws.Add<InteropWithdraw>(withdraw);

            Runtime.Notify(EventKind.BrokerRequest, from, to);
        }

        public void SetBroker(Address from, Hash hash)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(Runtime.IsKnownValidator(from), "invalid validator");

            var count = _withdraws.Count();
            var index = -1;
            for (int i = 0; i < count; i++)
            {
                var entry = _withdraws.Get<InteropWithdraw>(i);
                if (entry.hash == hash)
                {
                    index = i;
                    break;
                }
            }

            Runtime.Expect(index >= 0, "invalid hash");

            var withdraw = _withdraws.Get<InteropWithdraw>(index);
            Runtime.Expect(withdraw.broker.IsNull, "broker already set");

            Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, this.Address, withdraw.collateralAmount);

            withdraw.broker = from;
            withdraw.timestamp = Runtime.Time;
            _withdraws.Replace<InteropWithdraw>(index, withdraw);

            var expireDate = new Timestamp(Runtime.Time.Value + 86400); // 24 hours from now
            Runtime.Notify(EventKind.RolePromote, from, new RoleEventData() { role = "broker", date = expireDate });
        }

        // NOTE we dont allow cancelling an withdraw due to being possible to steal tokens that way
        // we do however allow to cancel a broker if too long has passed
        public void CancelBroker(Address from, Hash hash)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var count = _withdraws.Count();
            var index = -1;
            for (int i = 0; i < count; i++)
            {
                var entry = _withdraws.Get<InteropWithdraw>(i);
                if (entry.hash == hash)
                {
                    index = i;
                    break;
                }
            }

            Runtime.Expect(index >= 0, "invalid hash");

            var withdraw = _withdraws.Get<InteropWithdraw>(index);

            var brokerAddress = withdraw.broker;
            Runtime.Expect(!brokerAddress.IsNull, "no broker set");

            var diff = Runtime.Time - withdraw.timestamp;
            var days = diff / 86400; // convert seconds to days
            Runtime.Expect(days >= 1, "still waiting for broker");

            Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, from, withdraw.collateralAmount);

            withdraw.broker = Address.Null;
            withdraw.timestamp = Runtime.Time;
            _withdraws.Replace<InteropWithdraw>(index, withdraw);

            Runtime.Notify(EventKind.RoleDemote, brokerAddress, new RoleEventData() { role = "broker", date = Runtime.Time});
        }

        public Address GetBroker(string chainName, Hash hash)
        {
            var count = _withdraws.Count();
            for (int i = 0; i < count; i++)
            {
                var entry = _withdraws.Get<InteropWithdraw>(i);
                if (entry.hash == hash)
                {
                    return entry.broker;
                }
            }

            return Address.Null;
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
                    if (!entry.broker.IsNull)
                    {
                        return InteropTransferStatus.Pending;
                    }

                    return InteropTransferStatus.Queued;
                }
            }


            return InteropTransferStatus.Unknown;
        }
    }
}
