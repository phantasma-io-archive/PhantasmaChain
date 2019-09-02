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

        public static BigInteger InteropFeeRate => 2;

        public InteropContract() : base()
        {
        }

        public void SettleTransaction(Address from, string chainName, Hash hash)
        {
            Runtime.Expect(InteropUtils.IsChainSupported(chainName), "unsupported chain");

            Runtime.Expect(IsWitness(from), "invalid witness");

            var chainHashes = _hashes.Get<string, StorageSet>(chainName);
            Runtime.Expect(!chainHashes.Contains<Hash>(hash), "hash already seen");
            chainHashes.Add<Hash>(hash);

            var interopBytes = Runtime.OracleReader($"interop://{chainName}/tx/{hash}");
            var interopTx = Serialization.Unserialize<InteropTransaction>(interopBytes);

            var expectedChainAddress = InteropUtils.GetInteropAddress(chainName);

            Runtime.Expect(interopTx.ChainName == chainName, "unxpected chain name");
            Runtime.Expect(interopTx.ChainAddress == expectedChainAddress, "unxpected chain address");
            Runtime.Expect(interopTx.Hash == hash, "unxpected hash");

            foreach (var evt in interopTx.Events)
            {
                if (evt.Kind == EventKind.TokenReceive && evt.Address == expectedChainAddress)
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
                    Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = expectedChainAddress, value = withdraw.transferAmount, symbol = withdraw.transferSymbol});
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

            var feeSymbol = to.DecodeChainSymbol();
            Runtime.Expect(Runtime.Nexus.TokenExists(feeSymbol), "invalid token");

            var feeToken = this.Runtime.Nexus.GetTokenInfo(feeSymbol);
            Runtime.Expect(feeToken.Flags.HasFlag(TokenFlags.Fungible), "fee token must be fungible");
            Runtime.Expect(feeToken.Flags.HasFlag(TokenFlags.Transferable), "fee token must be transferable");

            var basePrice = UnitConversion.GetUnitValue(Nexus.StakingTokenDecimals) / InteropFeeRate;
            var feeAmount = OracleUtils.GetQuote(Runtime.OracleReader, Runtime.Nexus, Nexus.FiatTokenSymbol, feeSymbol, basePrice);
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
