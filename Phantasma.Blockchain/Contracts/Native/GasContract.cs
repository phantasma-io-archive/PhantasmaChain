using Phantasma.Blockchain.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct GasEventData
    {
        public BigInteger price;
        public BigInteger amount;
    }

    public class GasContract : SmartContract
    {
        public override string Name => "gas";

        internal StorageMap _allowanceMap; //<Address, BigInteger>

        public void AllowGas(Address from, BigInteger price, BigInteger limit)
        {
            if (Runtime.readOnlyMode)
            {
                return;
            }

            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(price > 0, "price must be positive amount");
            Runtime.Expect(limit > 0, "limit must be positive amount");

            var token = this.Runtime.Nexus.NativeToken;
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "must be fungible token");

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            var maxAmount = price * limit;

            Runtime.Expect(balances.Subtract(from, maxAmount), "gas escrow withdraw failed");
            Runtime.Expect(balances.Add(Runtime.Chain.Address, maxAmount), "gas escrow deposit failed");

            var allowance = _allowanceMap.ContainsKey(from) ? _allowanceMap.Get<Address, BigInteger>(from) : 0;
            Runtime.Expect(allowance == 0, "unexpected pending allowance");

            allowance += maxAmount;
            _allowanceMap.Set(from, allowance);

            Runtime.Notify(EventKind.GasEscrow, from, new GasEventData() { price = price, amount = limit });
        }

        public void SpendGas(Address from) {
            if (Runtime.readOnlyMode)
            {
                return;
            }

            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(_allowanceMap.ContainsKey(from), "no gas allowance found");

            var availableAmount = _allowanceMap.Get<Address, BigInteger>(from);

            var spentGas = Runtime.UsedGas;
            var requiredAmount = spentGas * Runtime.GasPrice;

            Runtime.Expect(availableAmount >= requiredAmount, "gas allowance is not enough");

            var token = this.Runtime.Nexus.NativeToken;
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "must be fungible token");

            var balances = this.Runtime.Chain.GetTokenBalances(token);

            var leftoverAmount = availableAmount - requiredAmount;

            if (leftoverAmount > 0)
            {
                Runtime.Expect(balances.Subtract(Runtime.Chain.Address, leftoverAmount), "gas leftover deposit failed");
                Runtime.Expect(balances.Add(from, leftoverAmount), "gas leftover withdraw failed");
            }

            _allowanceMap.Remove(from);

            Runtime.Notify(EventKind.GasPayment, from, new GasEventData() { price = Runtime.GasPrice, amount = spentGas});
        }

    }
}
