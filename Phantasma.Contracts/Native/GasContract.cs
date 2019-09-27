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

        internal StorageMap _loanMap; // Address, GasLendEntry
        internal StorageMap _loanList; // Address, List<Address>
        internal StorageMap _lenderMap; // Address, GasLender
        internal StorageList _lenderList; // Address

        public const int LendReturn = 50;

        public const string MaxLoanAmountTag = "gas.max.loan"; //9999 * 10000;
        public const string MaxLenderCountTag = "gas.lender.count";

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

            Runtime.Expect(Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, this.Address, maxAmount), "gas escrow failed");
            Runtime.Notify(EventKind.GasEscrow, from, new GasEventData() { address = target, price = price, amount = limit });
        }

        public void LoanGas(Address from, BigInteger price, BigInteger limit)
        {
            if (Runtime.IsReadOnlyMode())
            {
                return;
            }

            var pow = Runtime.Transaction.Hash.GetDifficulty();
            Runtime.Expect(pow >= (int)ProofOfWork.Moderate, "expected proof of work");

            Runtime.Expect(Runtime.IsRootChain(), "must be a root chain");

            Runtime.Expect(from.IsUser, "must be a user address");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(price > 0, "price must be positive amount");
            Runtime.Expect(limit > 0, "limit must be positive amount");

            BigInteger lendedAmount = price * limit;

            var lenderAddress = FindLender(lendedAmount);
            Runtime.Expect(!lenderAddress.IsNull, "no lender available");

            Runtime.Expect(IsLender(lenderAddress), "invalid lender address");

            Runtime.Expect(GetLoanAmount(from) == 0, "already has an active loan");

            var maxLoanAmount = Runtime.GetGovernanceValue(MaxLoanAmountTag);
            Runtime.Expect(lendedAmount <= maxLoanAmount, "limit exceeds maximum allowed for lend");

            var lender = _lenderMap.Get<Address, GasLender>(lenderAddress);
            Runtime.Expect(lender.balance >= lendedAmount, "not enough balance in lender");
            lender.balance -= lendedAmount;
            _lenderMap.Set<Address, GasLender>(lenderAddress, lender);

            var loan = new GasLoanEntry()
            {
                amount = lendedAmount,
                hash = Runtime.Transaction.Hash,
                borrower = from,
                lender = lenderAddress,
                interest = 0
            };
            _loanMap.Set<Address, GasLoanEntry>(from, loan);

            var list = _loanList.Get<Address, StorageList>(lenderAddress);
            list.Add<Address>(from);

            Runtime.Expect(Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, from, loan.amount), "gas lend failed");
            Runtime.Notify(EventKind.GasLoan, from, new GasEventData() { address = lenderAddress, price = price, amount = limit });
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

            // TODO the transfers around here should pass through Nexus.TransferTokens!!
            // return unused gas to transaction creator
            if (leftoverAmount > 0)
            {
                Runtime.Expect(Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, from, leftoverAmount), "gas leftover return failed");
            }

            Runtime.Expect(spentGas > 1, "gas spent too low");
            var bombGas = spentGas / 2;

            if (bombGas > 0)
            {
                var bombPayment = bombGas * Runtime.GasPrice;
                var bombAddress = SmartContract.GetAddressForNative(NativeContractKind.Bomb);
                Runtime.Expect(Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, bombAddress, bombPayment), "gas bomb payment failed");
                Runtime.Notify(EventKind.GasPayment, bombAddress, new GasEventData() { address = from, price = Runtime.GasPrice, amount = bombGas});
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
                Runtime.Expect(Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, targetAddress, targetPayment), "gas target payment failed");
                Runtime.Notify(EventKind.GasPayment, targetAddress, new GasEventData() { address = from, price = Runtime.GasPrice, amount = targetGas });
                spentGas -= targetGas;
            }

            if (spentGas > 0)
            {
                var validatorPayment = spentGas * Runtime.GasPrice;
                var validatorAddress = SmartContract.GetAddressForNative(NativeContractKind.Block);
                Runtime.Expect(Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, validatorAddress, validatorPayment), "gas validator payment failed");
                Runtime.Notify(EventKind.GasPayment, validatorAddress, new GasEventData() { address = from, price = Runtime.GasPrice, amount = spentGas });
                spentGas = 0;
            }

            _allowanceMap.Remove(from);
            _allowanceTargets.Remove(from);

            // check if there is an active lend and it is time to pay it
            if (_loanMap.ContainsKey<Address>(from))
            {
                var loan = _loanMap.Get<Address, GasLoanEntry>(from);

                Runtime.Expect(_lenderMap.ContainsKey<Address>(loan.lender), "missing lender info");
                var gasLender = _lenderMap.Get<Address, GasLender>(loan.lender);

                if (loan.hash == Runtime.Transaction.Hash)
                {
                    var unusedLoanAmount = loan.amount - requiredAmount;
                    Runtime.Expect(unusedLoanAmount >= 0, "loan amount overflow");

                    // here we return the gas to the original pool, not the the payment address, because this is not a payment
                    Runtime.Expect(Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, this.Address, unusedLoanAmount), "unspend loan payment failed");

                    gasLender.balance += unusedLoanAmount;
                    _lenderMap.Set<Address, GasLender>(loan.lender, gasLender);

                    Runtime.Notify(EventKind.GasPayment, loan.borrower, new GasEventData() { address = from, price = 1, amount = unusedLoanAmount});

                    loan.amount = requiredAmount;
                    loan.interest = (requiredAmount * LendReturn) / 100;
                    _loanMap.Set<Address, GasLoanEntry>(from, loan);
                }
                else
                {
                    Runtime.Expect(Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, loan.lender, loan.amount), "loan payment failed");
                    Runtime.Expect(Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, gasLender.paymentAddress, loan.interest), "loan interest failed");
                    _loanMap.Remove<Address>(from);

                    var list = _loanList.Get<Address, StorageList>(loan.lender);
                    int index = -1;
                    var count = list.Count();
                    for (int i=0; i<count; i++)
                    {
                        var temp = list.Get<Address>(i);
                        if (temp == from)
                        {
                            index = i;
                            break;
                        }
                    }

                    Runtime.Expect(index >= 0, "loan missing from list");
                    list.RemoveAt<Address>(index);

                    Runtime.Notify(EventKind.GasPayment, loan.lender, new GasEventData() { address = from, price = 1, amount = loan.amount });
                    Runtime.Notify(EventKind.GasPayment, gasLender.paymentAddress, new GasEventData() { address = from, price = 1, amount = loan.interest});
                }
            }
        }

        #region LOANS
        public Address[] GetLenders()
        {
            return _lenderList.All<Address>();
        }

        public bool IsLender(Address address)
        {
            if (address.IsUser)
            {
                return _lenderMap.ContainsKey<Address>(address);
            }

            return false;
        }

        public BigInteger GetLoanAmount(Address address)
        {
            if (_loanMap.ContainsKey<Address>(address))
            {
                var entry = _loanMap.Get<Address, GasLoanEntry>(address);
                return entry.amount;
            }

            return 0;
        }

        private Address FindLender(BigInteger amount)
        {
            var count = _lenderList.Count();
            if (count > 0)
            {
                var index = Runtime.GenerateRandomNumber() % count;
                var originalIndex = index;
                do
                {
                    var address = _lenderList.Get<Address>(index);
                    var lender = _lenderMap.Get<Address, GasLender>(address);
                    if (lender.balance >= amount)
                    {
                        return address;
                    }

                    index++;
                    if (index == originalIndex)
                    {
                        break;
                    }

                    if (index >= count)
                    {
                        index = 0;
                    }
                } while (true);
            }

            return Address.Null;
        }

        public Address[] GetBorrowers(Address from)
        {
            var list = _loanList.Get<Address, StorageList>(from);
            return list.All<Address>();
        }

        /// <summary>
        /// Setup a new lender
        /// </summary>
        /// <param name="from">Address from which KCAL will be lent</param>
        /// <param name="to">Address that will receive the profits from the lending (can be the same as the first address)</param>
        public void StartLend(Address from, Address to)
        {
            var maxLenderCount = Runtime.GetGovernanceValue(MaxLenderCountTag);
            Runtime.Expect(_lenderList.Count() < maxLenderCount, "too many lenders already");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(to.IsUser, "invalid destination address");
            Runtime.Expect(!IsLender(from), "already lending at source address");
            Runtime.Expect(!IsLender(to), "already lending at destination address");

            var balance = Runtime.GetBalance(DomainSettings.FuelTokenSymbol, from);
            Runtime.Expect(balance > 0, "not enough gas for lending");
            Runtime.Expect(Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, this.Address, balance), "gas transfer failed");

            var lender = new GasLender()
            {
                paymentAddress = to,
                balance = balance
            };

            _lenderList.Add<Address>(from);
            _lenderMap.Set<Address, GasLender>(from, lender);

            Runtime.Notify(EventKind.AddressLink, from, Runtime.Chain.Address);
        }

        public void StopLend(Address from)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(IsLender(from), "not a lender");

            int index = -1;
            var count = _lenderList.Count();
            for (int i = 0; i < count; i++)
            {
                var entry = _lenderList.Get<Address>(i);
                if (entry == from)
                {
                    index = i;
                    break;
                }
            }

            Runtime.Expect(index >= 0, "not lending");

            _lenderList.RemoveAt<Address>(index);
            _lenderMap.Remove<Address>(from);

            Runtime.Notify(EventKind.AddressUnlink, from, Runtime.Chain.Address);
        }
        #endregion
    }
}
