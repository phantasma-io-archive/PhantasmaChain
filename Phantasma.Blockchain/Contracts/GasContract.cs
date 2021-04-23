using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using Phantasma.Core.Performance;
using Phantasma.VM;
using System.Collections.Generic;
using Phantasma.Storage;
using Phantasma.Blockchain.Tokens;

namespace Phantasma.Blockchain.Contracts
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

        internal BigInteger _rewardAccum;

        internal Timestamp _lastInflationDate;
        internal bool _inflationReady;

        public void AllowGas(Address from, Address target, BigInteger price, BigInteger limit)
        {
            if (Runtime.IsReadOnlyMode())
            {
                return;
            }

            if (_lastInflationDate == 0)
            {
                _lastInflationDate = Runtime.Time;
            }

            Runtime.Expect(from.IsUser, "must be a user address");
            Runtime.Expect(Runtime.PreviousContext.Name == VirtualMachine.EntryContextName, "must be entry context");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(target.IsSystem, "destination must be system address");

            Runtime.Expect(price > 0, "price must be positive amount");
            Runtime.Expect(limit > 0, "limit must be positive amount");

            if (target.IsNull)
            {
                target = Runtime.Chain.Address;
            }

            var maxAmount = price * limit;

            using (var m = new ProfileMarker("_allowanceMap"))
            {
                var allowance = _allowanceMap.ContainsKey(from) ? _allowanceMap.Get<Address, BigInteger>(from) : 0;
                Runtime.Expect(allowance == 0, "unexpected pending allowance");

                allowance += maxAmount;
                _allowanceMap.Set(from, allowance);
                _allowanceTargets.Set(from, target);
            }

            BigInteger balance;
            using (var m = new ProfileMarker("Runtime.GetBalance"))
            {
                balance = Runtime.GetBalance(DomainSettings.FuelTokenSymbol, from);
            }

            if (maxAmount > balance)
            {
                var diff = maxAmount - balance;
                var fuelToken = Runtime.GetToken(DomainSettings.FuelTokenSymbol);
                throw new BalanceException(fuelToken, from, diff);
            }

            Runtime.Expect(balance >= maxAmount, $"not enough {DomainSettings.FuelTokenSymbol} {balance} in address {from} {maxAmount}");

            using (var m = new ProfileMarker("Runtime.TransferTokens"))
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, this.Address, maxAmount);
            using (var m = new ProfileMarker("Runtime.Notify"))
                Runtime.Notify(EventKind.GasEscrow, from, new GasEventData(target, price, limit));
        }
        
        public void ApplyInflation(Address from)
        {
            Runtime.Expect(_inflationReady, "inflation not ready");

            Runtime.Expect(Runtime.IsRootChain(), "only on root chain");

            var currentSupply = Runtime.GetTokenSupply(DomainSettings.StakingTokenSymbol);

            var minExpectedSupply = UnitConversion.ToBigInteger(100000000, DomainSettings.StakingTokenDecimals);
            if (currentSupply < minExpectedSupply)
            {
                currentSupply = minExpectedSupply;
            }

            // NOTE this gives an approximate inflation of 3% per year (0.75% per season)
            var inflationAmount = currentSupply / 133;
            BigInteger mintedAmount = 0;

            Runtime.Expect(inflationAmount > 0, "invalid inflation amount");
            
            var masterOrg = Runtime.GetOrganization(DomainSettings.MastersOrganizationName);
            var masters = masterOrg.GetMembers();
            
            var rewardList = new List<Address>();
            foreach (var addr in masters)
            {
                var masterDate = Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.GetMasterDate), addr).AsTimestamp();

                if (masterDate <= _lastInflationDate)
                {
                    rewardList.Add(addr);
                }
            }

            if (rewardList.Count > 0)
            {
                var rewardAmount = inflationAmount / 10;

                var rewardStake = rewardAmount / rewardList.Count;
                rewardAmount = rewardList.Count * rewardStake; // eliminate leftovers

                var rewardFuel = _rewardAccum / rewardList.Count;

                BigInteger stakeAmount;

                if (Runtime.ProtocolVersion > 5)
                {
                    stakeAmount = UnitConversion.ToBigInteger(2, DomainSettings.StakingTokenDecimals);
                }
                else
                {
                    stakeAmount = UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals);
                }

                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, this.Address, this.Address, rewardAmount);

                var crownAddress = TokenUtils.GetContractAddress(DomainSettings.RewardTokenSymbol);
                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, this.Address, crownAddress, stakeAmount);
                Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Stake), crownAddress, stakeAmount);

                foreach (var addr in rewardList)
                {
                    var reward = new StakeReward(addr, Runtime.Time);
                    var rom = Serialization.Serialize(reward);
                    var tokenID = Runtime.MintToken(DomainSettings.RewardTokenSymbol, this.Address, this.Address, rom, new byte[0], 0);
                    Runtime.InfuseToken(DomainSettings.RewardTokenSymbol, this.Address, tokenID, DomainSettings.FuelTokenSymbol, rewardFuel);
                    Runtime.InfuseToken(DomainSettings.RewardTokenSymbol, this.Address, tokenID, DomainSettings.StakingTokenSymbol, rewardStake);
                    Runtime.TransferToken(DomainSettings.RewardTokenSymbol, this.Address, addr, tokenID);
                }

                _rewardAccum -= rewardList.Count * rewardFuel;
                Runtime.Expect(_rewardAccum >= 0, "invalid reward leftover");

                inflationAmount -= rewardAmount;
                inflationAmount -= stakeAmount;
            }

            var refillAmount = inflationAmount / 50;
            var cosmicAddress = SmartContract.GetAddressForNative(NativeContractKind.Swap);
            Runtime.MintTokens(DomainSettings.StakingTokenSymbol, this.Address, cosmicAddress, refillAmount);
            inflationAmount -= refillAmount;

            var phantomOrg = Runtime.GetOrganization(DomainSettings.PhantomForceOrganizationName);
            if (phantomOrg != null)
            {
                var phantomFunding = inflationAmount / 3;
                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, this.Address, phantomOrg.Address, phantomFunding);
                inflationAmount -= phantomFunding;

                if (phantomOrg.Size == 1)
                {
                    Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Stake), phantomOrg.Address, phantomFunding);
                }
            }

            var bpOrg = Runtime.GetOrganization(DomainSettings.ValidatorsOrganizationName);
            if (bpOrg != null)
            {
                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, this.Address, bpOrg.Address, inflationAmount);

                if (bpOrg.Size == 1)
                {
                    Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Stake), bpOrg.Address, inflationAmount);
                }
            }

            Runtime.Notify(EventKind.Inflation, from, new TokenEventData(DomainSettings.StakingTokenSymbol, mintedAmount, Runtime.Chain.Name));

            _lastInflationDate = Runtime.Time;
            _inflationReady = false;
        }

        public void SpendGas(Address from)
        {
            if (Runtime.IsReadOnlyMode())
            {
                return;
            }

            Runtime.Expect(Runtime.PreviousContext.Name == VirtualMachine.EntryContextName, "must be entry context");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(_allowanceMap.ContainsKey(from), "no gas allowance found");

            if (Runtime.ProtocolVersion >= 3)
            {
                SpendGasV2(from);
            }
            else
            {
                SpendGasV1(from);
            }

        }

        private void SpendGasV1(Address from)
        {
            var availableAmount = _allowanceMap.Get<Address, BigInteger>(from);

            var spentGas = Runtime.UsedGas;
            var requiredAmount = spentGas * Runtime.GasPrice;
            Runtime.Expect(requiredAmount > 0, "gas fee must exist");

            Runtime.Expect(availableAmount >= requiredAmount, "gas allowance is not enough");

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

            targetGas = spentGas / 2; // 50% for dapps (or reward accum if dapp not specified)

            if (targetGas > 0)
            {
                var targetPayment = targetGas * Runtime.GasPrice;

                if (targetAddress == Runtime.Chain.Address)
                {
                    _rewardAccum += targetPayment;
                    Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, this.Address, targetPayment);
                    Runtime.Notify(EventKind.CrownRewards, from, new TokenEventData(DomainSettings.FuelTokenSymbol, targetPayment, Runtime.Chain.Name));
                }
                else
                {
                    Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, targetAddress, targetPayment);
                }
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

            CheckInflation();
        }

        private void SpendGasV2(Address from)
        {
            var availableAmount = _allowanceMap.Get<Address, BigInteger>(from);

            var spentGas = Runtime.UsedGas;
            var requiredAmount = spentGas * Runtime.GasPrice;
            Runtime.Expect(requiredAmount > 0, $"{Runtime.GasPrice} {spentGas} gas fee must exist");

            Runtime.Expect(availableAmount >= requiredAmount, "gas allowance is not enough");

            var leftoverAmount = availableAmount - requiredAmount;

            var targetAddress = _allowanceTargets.Get<Address, Address>(from);
            BigInteger targetGas;

            Runtime.Notify(EventKind.GasPayment, from, new GasEventData(targetAddress,  Runtime.GasPrice, spentGas));

            // return leftover escrowed gas to transaction creator
            if (leftoverAmount > 0)
            {
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, from, leftoverAmount);
            }

            Runtime.Expect(spentGas > 1, "gas spent too low");
            var burnGas = spentGas / 2;

            if (burnGas > 0)
            {
                BigInteger burnAmount;
                
                if (Runtime.ProtocolVersion >= 4)
                {
                    burnAmount = burnGas * Runtime.GasPrice;
                }
                else
                {
                    burnAmount = burnGas;
                }

                Runtime.BurnTokens(DomainSettings.FuelTokenSymbol, this.Address, burnAmount);
                spentGas -= burnGas;
            }

            targetGas = spentGas / 2; // 50% for dapps (or reward accum if dapp not specified)

            if (targetGas > 0)
            {
                var targetPayment = targetGas * Runtime.GasPrice;

                if (targetAddress == Runtime.Chain.Address)
                {
                    _rewardAccum += targetPayment;
                    Runtime.Notify(EventKind.CrownRewards, from, new TokenEventData(DomainSettings.FuelTokenSymbol, targetPayment, Runtime.Chain.Name));
                }
                else
                {
                    Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, targetAddress, targetPayment);
                }
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

            CheckInflation();
        }

        private void CheckInflation()
        {
            if (!Runtime.HasGenesis)
            {
                return;
            }

            if (_lastInflationDate.Value == 0)
            {
                var genesisTime = Runtime.GetGenesisTime();
                _lastInflationDate = genesisTime;
            }
            else
            if (!_inflationReady)
            {
                var infDiff = Runtime.Time - _lastInflationDate;
                var inflationPeriod = SecondsInDay * 90;
                if (infDiff >= inflationPeriod)
                {
                    _inflationReady = true;
                }
            }
        }
    }
}
