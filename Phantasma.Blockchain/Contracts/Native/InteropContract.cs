using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Context;
using System;
using System.Linq;

namespace Phantasma.Blockchain.Contracts.Native
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

    public sealed class InteropContract : SmartContract
    {
        public override string Name => Nexus.InteropContractName;

        private StorageList _platforms;

        private StorageMap _hashes;
        private StorageList _withdraws;

        private StorageMap _links;
        private StorageMap _reverseMap;

        public static BigInteger InteropFeeRacio => 2;

        public InteropContract() : base()
        {
        }

        public void RegisterLink(Address from, Address target)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "source address must be user address");
            Runtime.Expect(target.IsInterop, "target address must be interop address");
            Runtime.Expect(!Runtime.Nexus.IsKnownValidator(from), "source address cant be chain validator");

            string platformName;
            byte[] data;
            target.DecodeInterop(out platformName, out data, 0);
            Runtime.Expect(Runtime.Nexus.PlatformExists(platformName), "unsupported chain");

            var list = _links.Get<Address, StorageList>(from);

            var count = list.Count();
            int index = -1;

            for (int i = 0; i < count; i++)
            {
                var address = list.Get<Address>(i);

                string otherChainName;
                address.DecodeInterop(out otherChainName, out data, 0);

                if (otherChainName ==  platformName)
                {
                    index = i;
                    break;
                }
            }

            if (index >= 0)
            {
                var previous = list.Get<Address>(index);
                _reverseMap.Remove<Address>(previous);
                list.Replace<Address>(index, target);
            }
            else
            {
                list.Add(target);
            }

            _reverseMap.Set<Address, Address>(target, from);

            Runtime.Notify(EventKind.AddressLink, from, target);
        }

        public Address[] GetLinks(Address from)
        {
            var list = _links.Get<Address, StorageList>(from);

            return list.All<Address>();
        }

        public Address GetLink(Address from, string platformName)
        {
            if (DomainSettings.PlatformName.Equals(platformName, StringComparison.OrdinalIgnoreCase))
            {
                Runtime.Expect(from.IsInterop, "must be interop address");
                if (_reverseMap.ContainsKey<Address>(from))
                {
                    return _reverseMap.Get<Address, Address>(from);
                }

                return Address.Null;
            }

            Runtime.Expect(from.IsUser, "must be user address");
            Runtime.Expect(Runtime.Nexus.PlatformExists(platformName), "unsupported chain");

            var list = _links.Get<Address, StorageList>(from);
            var count = list.Count();

            for (int i = 0; i < count; i++)
            {
                var address = list.Get<Address>(i);

                string otherPlatform;
                byte[] data;
                address.DecodeInterop(out otherPlatform, out data, 0);

                if (otherPlatform == platformName)
                {
                    return address;
                }
            }

            return Address.Null;
        }

        public void SettleTransaction(Address from, string platform, Hash hash)
        {
            Runtime.Expect(platform != DomainSettings.PlatformName, "must be external platform");
            Runtime.Expect(Runtime.Nexus.PlatformExists(platform), "unsupported platform");
            var platformInfo = Runtime.Nexus.GetPlatformInfo(platform);

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "must be user address");

            var chainHashes = _hashes.Get<string, StorageSet>(platform);
            Runtime.Expect(!chainHashes.Contains<Hash>(hash), "hash already seen");

            var interopTx = Runtime.ReadTransactionFromOracle(platform, DomainSettings.RootChainName, hash);

            Runtime.Expect(interopTx.Platform == platform, "unxpected platform name");
            Runtime.Expect(interopTx.Hash == hash, "unxpected hash");

            int swapCount = 0;

            foreach (var evt in interopTx.Events)
            {
                if (evt.Kind == EventKind.TokenReceive && evt.Address == platformInfo.Address)
                {
                    Runtime.Expect(!evt.Address.IsNull, "invalid source address");

                    var transfer = evt.GetContent<TokenEventData>();
                    Runtime.Expect(transfer.value > 0, "amount must be positive and greater than zero");

                    Runtime.Expect(Runtime.Nexus.TokenExists(transfer.symbol), "invalid token");
                    var token = this.Runtime.Nexus.GetTokenInfo(transfer.symbol);

                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.External), "token must be external");

                    Address destination = Address.Null;
                    foreach (var otherEvt in interopTx.Events)
                    {
                        if (otherEvt.Kind == EventKind.TokenSend)
                        {
                            var otherTransfer = otherEvt.GetContent<TokenEventData>();
                            if (otherTransfer.chainAddress == transfer.chainAddress && otherTransfer.symbol == transfer.symbol)
                            {
                                destination = otherEvt.Address;
                                break;
                            }
                        }
                    }

                    if (destination.IsInterop)
                    {
                        destination = GetLink(destination, DomainSettings.PlatformName);
                    }
                    else
                    {
                        Runtime.Expect(destination.IsUser, "invalid destination address");
                    }

                    Runtime.Expect(Runtime.Nexus.MintTokens(Runtime, transfer.symbol, destination, transfer.value, true), "mint failed");
                    Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = platformInfo.Address, value = transfer.value, symbol = transfer.symbol });

                    swapCount++;
                    break;
                }

                if (evt.Kind == EventKind.TokenClaim)
                {
                    var destination = evt.Address;
                    Runtime.Expect(!destination.IsNull, "invalid destination");

                    var transfer = evt.GetContent<TokenEventData>();
                    Runtime.Expect(transfer.value > 0, "amount must be positive and greater than zero");

                    Runtime.Expect(Runtime.Nexus.TokenExists(transfer.symbol), "invalid token");
                    var token = this.Runtime.Nexus.GetTokenInfo(transfer.symbol);
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.External), "token must be external");

                    var count = _withdraws.Count();
                    var index = -1;
                    for (int i=0; i<count; i++)
                    {
                        var entry = _withdraws.Get<InteropWithdraw>(i);
                        if (entry.destination == destination && entry.transferAmount == transfer.value && entry.transferSymbol == transfer.symbol)
                        {
                            index = i;
                            break;
                        }
                    }

                    Runtime.Expect(index >= 0, "invalid withdraw, possible leak found");

                    var withdraw = _withdraws.Get<InteropWithdraw>(index);
                    Runtime.Expect(withdraw.broker == from, "invalid broker");

                    _withdraws.RemoveAt<InteropWithdraw>(index);

                    Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, withdraw.feeSymbol, this.Address, from, withdraw.feeAmount), "fee payment failed");

                    Runtime.Notify(EventKind.TokenReceive, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = withdraw.feeAmount, symbol = withdraw.feeSymbol });
                    Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = platformInfo.Address, value = withdraw.transferAmount, symbol = withdraw.transferSymbol});

                    swapCount++;
                    break;
                }
            }

            Runtime.Expect(swapCount > 0, "nothing to settle");
            chainHashes.Add<Hash>(hash);
        }

        // send to external chain
        public void WithdrawTokens(Address from, Address to, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(from.IsUser, "source must be user address");
            Runtime.Expect(to.IsInterop, "destination must be interop address");

            Runtime.Expect(Runtime.Nexus.TokenExists(symbol), "invalid token");

            var transferTokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(transferTokenInfo.Flags.HasFlag(TokenFlags.Transferable), "transfer token must be transferable");
            Runtime.Expect(transferTokenInfo.Flags.HasFlag(TokenFlags.External), "transfer token must be external");
            Runtime.Expect(transferTokenInfo.Flags.HasFlag(TokenFlags.Fungible), "transfer token must be fungible");

            string platform;
            byte[] dummy;
            to.DecodeInterop(out platform, out dummy, 0);
            Runtime.Expect(platform != DomainSettings.PlatformName, "must be external platform");
            Runtime.Expect(Runtime.Nexus.PlatformExists(platform), "invalid platform");
            var platformInfo = Runtime.Nexus.GetPlatformInfo(platform);
            Runtime.Expect(to != platformInfo.Address, "invalid target address");

            var feeSymbol = platformInfo.Symbol;
            Runtime.Expect(Runtime.Nexus.TokenExists(feeSymbol), "invalid fee token");

            var feeTokenInfo = this.Runtime.Nexus.GetTokenInfo(feeSymbol);
            Runtime.Expect(feeTokenInfo.Flags.HasFlag(TokenFlags.Fungible), "fee token must be fungible");
            Runtime.Expect(feeTokenInfo.Flags.HasFlag(TokenFlags.Transferable), "fee token must be transferable");

            var basePrice = UnitConversion.GetUnitValue(DomainSettings.FiatTokenDecimals) / InteropFeeRacio; // 50cents
            var feeAmount = Runtime.GetTokenQuote(DomainSettings.FiatTokenSymbol, feeSymbol, basePrice);
            Runtime.Expect(feeAmount > 0, "fee is too small");

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, feeSymbol, from, this.Address, feeAmount), "fee transfer failed");

            Runtime.Expect(Runtime.Nexus.BurnTokens(Runtime, symbol, from, amount, true), "burn failed");

            var collateralAmount = Runtime.GetTokenQuote(DomainSettings.FiatTokenSymbol, DomainSettings.FuelTokenSymbol, basePrice);

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

            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = this.Address, value = amount, symbol = symbol });
            Runtime.Notify(EventKind.TokenEscrow, from, new TokenEventData() { chainAddress = this.Address, value = feeAmount, symbol = symbol });
            Runtime.Notify(EventKind.BrokerRequest, from, to);
        }

        public void SetBroker(Address from, Hash hash)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(Runtime.Nexus.IsKnownValidator(from), "invalid validator");

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

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, DomainSettings.FuelTokenSymbol, from, this.Address, withdraw.collateralAmount), "collateral payment failed");
            Runtime.Notify(EventKind.TokenEscrow, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = withdraw.feeAmount, symbol = withdraw.feeSymbol });

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

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, DomainSettings.FuelTokenSymbol, this.Address, from, withdraw.collateralAmount), "fee payment failed");

            withdraw.broker = Address.Null;
            withdraw.timestamp = Runtime.Time;
            _withdraws.Replace<InteropWithdraw>(index, withdraw);

            Runtime.Notify(EventKind.TokenReceive, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = withdraw.collateralAmount, symbol = withdraw.feeSymbol });
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

        public InteropTransferStatus GetStatus(string chainName, Hash hash)
        {
            var chainHashes = _hashes.Get<string, StorageSet>(chainName);
            if (chainHashes.Contains<Hash>(hash))
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
