using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Context;
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
        public Address broker;
        public Timestamp timestamp;
    }

    public sealed class InteropContract : SmartContract
    {
        public override string Name => "interop";

        private StorageMap _hashes;
        private StorageList _withdraws;

        private StorageMap _externalAddresses;

        private StorageMap _links;
        private StorageMap _reverseMap;

        public static BigInteger InteropFeeRate => 2;

        public InteropContract() : base()
        {
        }

        public bool IsChainSupported(string name)
        {
            return _externalAddresses.ContainsKey<string>(name);
        }

        public InteropChainInfo GetChainInfo(string name)
        {
            var extAddr = _externalAddresses.Get<string, Address>(name);
            Runtime.Expect(extAddr != Address.Null, "external chain not initialized");

            switch (name)
            {
                case "NEO":
                    {
                        return new InteropChainInfo() { Name = name, Symbol = "GAS", Address = extAddr };
                    }

                default:
                    Runtime.Expect(false, "unknown chain");
                    return new InteropChainInfo();
            }
        }

        public InteropChainInfo[] GetAvailableChains()
        {
            if (IsChainSupported("NEO"))
            {
                return new InteropChainInfo[] { GetChainInfo("NEO")};
            }

            return new InteropChainInfo[] { };
        }

        public void RegisterChain(Address target)
        {
            Runtime.Expect(IsWitness(Runtime.Nexus.GenesisAddress), "must be genesis");

            Runtime.Expect(target.IsInterop, "external address must be interop");

            string chainName;
            byte[] data;
            target.DecodeInterop(out chainName, out data, 0);

            _externalAddresses.Set<string, Address>(chainName, target);

            Runtime.Notify(EventKind.AddressRegister, target, chainName);
        }

        public void RegisterLink(Address from, Address target)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(target.IsInterop, "address must be interop");

            string chainName;
            byte[] data;
            target.DecodeInterop(out chainName, out data, 0);
            Runtime.Expect(IsChainSupported(chainName), "unsupported chain");

            var list = _links.Get<Address, StorageList>(from);

            var count = list.Count();
            for (int i = 0; i < count; i++)
            {
                var address = list.Get<Address>(i);

                string otherChainName;
                address.DecodeInterop(out otherChainName, out data, 0);

                Runtime.Expect(otherChainName != chainName, "chain interop already linked");
            }

            list.Add(target);
            _reverseMap.Set<Address, Address>(target, from);

            Runtime.Notify(EventKind.AddressRegister, from, target);
        }

        public Address GetLink(Address from, string chainName)
        {
            if (chainName == "phantasma")
            {
                Runtime.Expect(from.IsInterop, "must be interop");
                if (_reverseMap.ContainsKey<Address>(from))
                {
                    return _reverseMap.Get<Address, Address>(from);
                }

                return Address.Null;
            }

            Runtime.Expect(!from.IsInterop, "cant be interop");
            Runtime.Expect(IsChainSupported(chainName), "unsupported chain");

            var list = _links.Get<Address, StorageList>(from);
            var count = list.Count();

            for (int i = 0; i < count; i++)
            {
                var address = list.Get<Address>(i);

                string otherChainName;
                byte[] data;
                address.DecodeInterop(out otherChainName, out data, 0);

                if (otherChainName == chainName)
                {
                    return address;
                }
            }

            return Address.Null;
        }

        public void SettleTransaction(Address from, string chainName, Hash hash)
        {
            Runtime.Expect(IsChainSupported(chainName), "unsupported chain");
            var chainInfo = GetChainInfo(chainName);

            Runtime.Expect(IsWitness(from), "invalid witness");

            var chainHashes = _hashes.Get<string, StorageSet>(chainName);
            Runtime.Expect(!chainHashes.Contains<Hash>(hash), "hash already seen");
            chainHashes.Add<Hash>(hash);

            var interopBytes = Runtime.Oracle.Read($"interop://{chainName}/tx/{hash}");
            var interopTx = Serialization.Unserialize<InteropTransaction>(interopBytes);

            var externalAddress = _externalAddresses.Get<string, Address>(chainName);

            Runtime.Expect(interopTx.ChainName == chainName, "unxpected chain name");
            Runtime.Expect(interopTx.Hash == hash, "unxpected hash");

            foreach (var evt in interopTx.Events)
            {
                if (evt.Kind == EventKind.TokenReceive && evt.Address == externalAddress)
                {
                    var destination = evt.Address;
                    Runtime.Expect(destination != Address.Null, "invalid destination");

                    var transfer = evt.GetContent<TokenEventData>();
                    Runtime.Expect(transfer.value > 0, "amount must be positive and greater than zero");

                    Runtime.Expect(Runtime.Nexus.TokenExists(transfer.symbol), "invalid token");
                    var token = this.Runtime.Nexus.GetTokenInfo(transfer.symbol);

                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.External), "token must be external");

                    var source = Address.Null;

                    Runtime.Expect(Runtime.Nexus.MintTokens(Runtime, transfer.symbol, destination, transfer.value), "mint failed");
                    Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = transfer.value, symbol = transfer.symbol });
                    break;
                }

                if (evt.Kind == EventKind.TokenClaim)
                {
                    var destination = evt.Address;
                    Runtime.Expect(destination != Address.Null, "invalid destination");

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
                    Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = externalAddress, value = withdraw.transferAmount, symbol = withdraw.transferSymbol});
                    break;
                }
            }
        }

        // send to external chain
        public void WithdrawTokens(Address from, Address to, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(!from.IsInterop, "source can't be interop address");
            Runtime.Expect(to.IsInterop, "destination must be interop address");

            Runtime.Expect(Runtime.Nexus.TokenExists(symbol), "invalid token");

            var transferToken = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(transferToken.Flags.HasFlag(TokenFlags.Transferable), "transfer token must be transferable");
            Runtime.Expect(transferToken.Flags.HasFlag(TokenFlags.External), "transfer token must be external");

            string feeSymbol;
            byte[] dummy;
            to.DecodeInterop(out feeSymbol, out dummy, 0);
            Runtime.Expect(Runtime.Nexus.TokenExists(feeSymbol), "invalid token");

            var feeToken = this.Runtime.Nexus.GetTokenInfo(feeSymbol);
            Runtime.Expect(feeToken.Flags.HasFlag(TokenFlags.Fungible), "fee token must be fungible");
            Runtime.Expect(feeToken.Flags.HasFlag(TokenFlags.Transferable), "fee token must be transferable");

            var basePrice = UnitConversion.GetUnitValue(Nexus.StakingTokenDecimals) / InteropFeeRate;
            var feeAmount = Runtime.GetTokenQuote(Nexus.FiatTokenSymbol, feeSymbol, basePrice);
            Runtime.Expect(feeAmount > 0, "fee is too small");

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, feeSymbol, from, this.Address, feeAmount), "fee transfer failed");

            Runtime.Expect(Runtime.Nexus.BurnTokens(Runtime, symbol, from, amount), "burn failed");

            var withdraw = new InteropWithdraw()
            {
                destination = to,
                transferAmount = amount,
                transferSymbol = symbol,
                feeAmount = feeAmount,
                feeSymbol = feeSymbol,
                hash = Runtime.Transaction.Hash,
                broker = Address.Null,
                timestamp = Runtime.Time
            };
            _withdraws.Add<InteropWithdraw>(withdraw);

            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
            Runtime.Notify(EventKind.TokenEscrow, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = feeAmount, symbol = symbol });
        }


        public void SetBroker(Address from, Hash hash)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(IsValidator(from), "invalid validator");

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
            Runtime.Expect(withdraw.broker == Address.Null, "broker already set");

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, withdraw.feeSymbol, from, this.Address, withdraw.feeAmount), "fee payment failed");
            Runtime.Notify(EventKind.TokenEscrow, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = withdraw.feeAmount, symbol = withdraw.feeSymbol });

            withdraw.broker = from;
            withdraw.timestamp = Runtime.Time;
            withdraw.feeAmount *= 2; 
            _withdraws.Replace<InteropWithdraw>(index, withdraw);

            var expireDate = new Timestamp(Runtime.Time.Value + 86400); // 24 hours from now
            Runtime.Notify(EventKind.RolePromote, from, new RoleEventData() { role = "broker", date = expireDate });
        }

        // NOTE we dont allow cancelling an withdraw due to being possible to steal tokens that way
        // we do however allow to cancel a broker if too long has passed
        public void CancelBroker(Address from, Hash hash)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

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
            Runtime.Expect(brokerAddress != Address.Null, "no broker set");

            var diff = Runtime.Time - withdraw.timestamp;
            var days = diff / 86400; // convert seconds to days
            Runtime.Expect(days >= 1, "still waiting for broker");

            var escrowAmount = withdraw.feeAmount / 2;
            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, withdraw.feeSymbol, this.Address, from, escrowAmount), "fee payment failed");

            withdraw.broker = Address.Null;
            withdraw.timestamp = Runtime.Time;
            withdraw.feeAmount -= escrowAmount;
            _withdraws.Replace<InteropWithdraw>(index, withdraw);

            Runtime.Notify(EventKind.TokenReceive, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = escrowAmount, symbol = withdraw.feeSymbol });

            Runtime.Notify(EventKind.RoleDemote, brokerAddress, new RoleEventData() { role = "broker", date = Runtime.Time});
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
                    if (entry.broker != Address.Null)
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
