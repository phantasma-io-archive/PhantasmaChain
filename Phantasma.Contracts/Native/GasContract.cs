using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Contracts.Native
{
    public struct GasLoanEntry
    {
        public Hash hash;
        public Address borrower;
        public Address lender;
        public BigInteger amount;
        public BigInteger interest;
    }

    public struct GasLender
    {
        public BigInteger balance;
        public Address paymentAddress;
    }

    public sealed class GasContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Gas;

        internal StorageMap _allowanceMap; //<Address, BigInteger>
        internal StorageMap _allowanceTargets; //<Address, Address>

        public void AllowGas(Address from, Address target, BigInteger price, BigInteger limit)
        {
            if (Runtime.IsReadOnlyMode())
            {
                return;
            }

            Runtime.Expect(from.IsUser, "must be a user address");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(target.IsSystem, "destination must be system address");

            Runtime.Expect(price > 0, "price must be positive amount");
            Runtime.Expect(limit > 0, "limit must be positive amount");

            var maxAmount = price * limit;

            var allowance = _allowanceMap.ContainsKey(from) ? _allowanceMap.Get<Address, BigInteger>(from) : 0;
            Runtime.Expect(allowance == 0, "unexpected pending allowance");

            allowance += maxAmount;
            _allowanceMap.Set(from, allowance);
            _allowanceTargets.Set(from, target);

            Runtime.Expect(Runtime.GetBalance(DomainSettings.FuelTokenSymbol, from) >= maxAmount, "not enough gas in address");

            Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, this.Address, maxAmount);
            Runtime.Notify(EventKind.GasEscrow, from, new GasEventData(target, price, limit));
        }
        
        public void SpendGas(Address from)
        {
            if (Runtime.IsReadOnlyMode())
            {
                return;
            }

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(_allowanceMap.ContainsKey(from), "no gas allowance found");

            var availableAmount = _allowanceMap.Get<Address, BigInteger>(from);

            var spentGas = Runtime.UsedGas;
            var requiredAmount = spentGas * Runtime.GasPrice;
            Runtime.Expect(requiredAmount > 0, "gas fee must exist");

            Runtime.Expect(availableAmount >= requiredAmount, "gas allowance is not enough");

            /*var token = this.Runtime.Nexus.FuelToken;
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "must be fungible token");
            */

            var leftoverAmount = availableAmount - requiredAmount;

            var targetAddress = _allowanceTargets.Get<Address, Address>(from);
            BigInteger targetGas;

            Runtime.Notify(EventKind.GasPayment, from, new GasEventData(targetAddress,  Runtime.GasPrice, spentGas));

            // return unused gas to transaction creator
            if (leftoverAmount > 0)
            {
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, from, leftoverAmount);
            }

            Runtime.Expect(spentGas > 1, "gas spent too low");
            var bombGas = Runtime.IsRootChain() ? spentGas / 2 : 0;

            if (bombGas > 0)
            {
                var bombPayment = bombGas * Runtime.GasPrice;
                var bombAddress = SmartContract.GetAddressForNative(NativeContractKind.Bomb);
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, bombAddress, bombPayment);
                spentGas -= bombGas;
            }

            if (!targetAddress.IsNull)
            {
                targetGas = spentGas / 2; // 50% for dapps
            }
            else
            {
                targetGas = 0;
            }

            if (targetGas > 0)
            {
                var targetPayment = targetGas * Runtime.GasPrice;
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, targetAddress, targetPayment);
                spentGas -= targetGas;
            }

            if (spentGas > 0)
            {
                var validatorPayment = spentGas * Runtime.GasPrice;
                var validatorAddress = SmartContract.GetAddressForNative(NativeContractKind.Block);
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, validatorAddress, validatorPayment);
                spentGas = 0;
            }

            _allowanceMap.Remove(from);
            _allowanceTargets.Remove(from);
        }
    }
}
