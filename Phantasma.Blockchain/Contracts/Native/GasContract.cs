using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct GasEventData
    {
        public Address address;
        public BigInteger price;
        public BigInteger amount;
    }

    public class GasContract : SmartContract
    {
        public override string Name => "gas";

        internal StorageMap _allowanceMap; //<Address, BigInteger>
        internal StorageMap _allowanceTargets; //<Address, Address>

        public void AllowGas(Address from, Address to, BigInteger price, BigInteger limit)
        {
            if (Runtime.readOnlyMode)
            {
                return;
            }

            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(from != to, "invalid gas target");
            Runtime.Expect(Runtime.Chain.Address != to, "invalid gas target");

            Runtime.Expect(price > 0, "price must be positive amount");
            Runtime.Expect(limit > 0, "limit must be positive amount");

            //var token = this.Runtime.Nexus.FuelToken;
            //Runtime.Expect(token != null, "invalid token");
            //Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "must be fungible token");

            var balances = new BalanceSheet(Nexus.FuelTokenSymbol);
            var maxAmount = price * limit;

            var balance = balances.Get(this.Storage, from);
            Runtime.Expect(balance >= maxAmount, "not enough gas in address");

            Runtime.Expect(balances.Subtract(this.Storage, from, maxAmount), "gas escrow withdraw failed");
            Runtime.Expect(balances.Add(this.Storage, Runtime.Chain.Address, maxAmount), "gas escrow deposit failed");

            var allowance = _allowanceMap.ContainsKey(from) ? _allowanceMap.Get<Address, BigInteger>(from) : 0;
            Runtime.Expect(allowance == 0, "unexpected pending allowance");

            allowance += maxAmount;
            _allowanceMap.Set(from, allowance);
            _allowanceTargets.Set(from, to);

            Runtime.Notify(EventKind.GasEscrow, from, new GasEventData() { address = Runtime.Chain.Address, price = price, amount = limit });
        }

        public void SpendGas(Address from)
        {
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

            /*var token = this.Runtime.Nexus.FuelToken;
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "must be fungible token");
            */

            var balances = new BalanceSheet(Nexus.FuelTokenSymbol);

            var leftoverAmount = availableAmount - requiredAmount;

            var targetAddress = _allowanceTargets.Get<Address, Address>(from);
            BigInteger targetGas;

            if (targetAddress != Address.Null)
            {
                targetGas = spentGas / 2; // 50% for dapps
            }
            else
            {
                targetGas = 0;
            }

            // return unused gas to transaction creator
            if (leftoverAmount > 0)
            {
                Runtime.Expect(balances.Subtract(this.Storage, Runtime.Chain.Address, leftoverAmount), "gas leftover deposit failed");
                Runtime.Expect(balances.Add(this.Storage, from, leftoverAmount), "gas leftover withdraw failed");
            }

            if (targetGas > 0)
            {
                var targetPayment = targetGas * Runtime.GasPrice;
                Runtime.Expect(balances.Subtract(this.Storage, Runtime.Chain.Address, targetPayment), "gas target withdraw failed");
                Runtime.Expect(balances.Add(this.Storage, targetAddress, targetPayment), "gas target deposit failed");
                spentGas -= targetGas;
            }

            _allowanceMap.Remove(from);
            _allowanceTargets.Remove(from);

            if (targetGas > 0)
            {
                Runtime.Notify(EventKind.GasPayment, targetAddress, new GasEventData() { address = from, price = Runtime.GasPrice, amount = targetGas });
            }

            Runtime.Notify(EventKind.GasPayment, Runtime.Chain.Address, new GasEventData() { address = from, price = Runtime.GasPrice, amount = spentGas });
        }

    }
}
