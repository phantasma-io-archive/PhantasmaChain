using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Context;
using System.Linq;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct InteropWithdraw
    {
        public Hash hash;
        public Address destination;
        public string symbol;
        public BigInteger amount;
        public BigInteger fee;
    }

    public sealed class InteropContract : SmartContract
    {
        public override string Name => "interop";

        private StorageMap _hashes;
        private StorageList _withdraws;

        public static BigInteger InteropFeeRate => 10;

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
                    Runtime.Expect(IsValidator(from), "invalid validator");

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
                        if (entry.destination == destination && entry.amount == transfer.value && entry.symbol == transfer.symbol)
                        {
                            index = i;
                            break;
                        }
                    }

                    Runtime.Expect(index >= 0, "invalid withdraw, possible leak found");

                    var withdraw = _withdraws.Get<InteropWithdraw>(index);
                    _withdraws.RemoveAt<InteropWithdraw>(index);

                    Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, transfer.symbol, this.Address, from, withdraw.fee), "fee payment failed");
                    Runtime.Notify(EventKind.TokenReceive, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = withdraw.fee, symbol = transfer.symbol });
                    Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = expectedChainAddress, value = withdraw.amount, symbol = transfer.symbol });
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
            var token = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.External), "token must be external");

            var minimumAmount = UnitConversion.GetUnitValue(token.Decimals);
            Runtime.Expect(amount >= minimumAmount, "amount is too small");

            var feeAmount = amount / InteropFeeRate;
            Runtime.Expect(feeAmount > 0, "fee is too small");
            var withdrawAmount = amount - feeAmount;
            Runtime.Expect(withdrawAmount > 0, "invalid withdraw amount");

            Runtime.Expect(Runtime.Nexus.BurnTokens(Runtime, symbol, from, withdrawAmount), "burn failed");
            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, symbol, from, this.Address, feeAmount), "fee transfer failed");

            var withdraw = new InteropWithdraw()
            {
                destination = to,
                amount = withdrawAmount,
                fee = feeAmount,
                symbol = symbol,
                hash = Runtime.Transaction.Hash,
            };
            _withdraws.Add<InteropWithdraw>(withdraw);

            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = withdrawAmount, symbol = symbol });
            Runtime.Notify(EventKind.TokenEscrow, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = feeAmount, symbol = symbol });
        }
    }
}
