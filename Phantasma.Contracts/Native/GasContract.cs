using Phantasma.Core.Types;
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

        public void Initialize(Address from)
        {
            Runtime.Expect(from == Runtime.GenesisAddress, "must be genesis address");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            _lastInflation = Runtime.Time;
        }

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

            var balance = Runtime.GetBalance(DomainSettings.FuelTokenSymbol, from);
            Runtime.Expect(balance >= maxAmount, "not enough gas in address");

            Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, this.Address, maxAmount);
            Runtime.Notify(EventKind.GasEscrow, from, new GasEventData(target, price, limit));
        }

        private Timestamp _lastInflation;
        
        private void ApplyInflation()
        {
            var currentSupply = Runtime.GetTokenSupply(DomainSettings.StakingTokenSymbol);

            // NOTE this gives an approximate inflation of 3% per year (0.75% per season)
            var mintAmount = currentSupply / 133;
            Runtime.Expect(mintAmount > 0, "invalid inflation amount");

            var phantomOrg = Runtime.GetOrganization(DomainSettings.PhantomForceOrganizationName);
            if (phantomOrg != null)
            {
                var phantomFunding = mintAmount / 3;
                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, this.Address, phantomOrg.Address, phantomFunding);
                mintAmount -= phantomFunding;
            }

            var bpOrg = Runtime.GetOrganization(DomainSettings.ValidatorsOrganizationName);
            if (bpOrg != null)
            {
                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, this.Address, bpOrg.Address, mintAmount);
            }

            Runtime.Notify(EventKind.Inflation, this.Address, DomainSettings.StakingTokenSymbol);

            _lastInflation = Runtime.Time;
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

            // return escrowed gas to transaction creator
            Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, from, availableAmount);

            Runtime.Expect(spentGas > 1, "gas spent too low");
            var burnGas = spentGas / 2;

            if (burnGas > 0)
            {
                Runtime.BurnTokens(DomainSettings.FuelTokenSymbol, from, burnGas);
                spentGas -= burnGas;
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
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, targetAddress, targetPayment);
                spentGas -= targetGas;
            }

            if (spentGas > 0)
            {
                var validatorPayment = spentGas * Runtime.GasPrice;
                var validatorAddress = SmartContract.GetAddressForNative(NativeContractKind.Block);
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, validatorAddress, validatorPayment);
                spentGas = 0;
            }

            _allowanceMap.Remove(from);
            _allowanceTargets.Remove(from);

            Runtime.Notify(EventKind.GasPayment, Address.Null, new GasEventData(targetAddress, Runtime.GasPrice, spentGas));

            if (Runtime.HasGenesis)
            {
                if (_lastInflation.Value == 0)
                {
                    var genesisTime = Runtime.GetGenesisTime();
                    _lastInflation = genesisTime;
                }
                else
                {
                    var infDiff = Runtime.Time - _lastInflation;
                    var inflationPeriod = SecondsInDay * 90;
                    if (infDiff >= inflationPeriod)
                    {
                        ApplyInflation();
                    }
                }
            }
        }
    }
}
