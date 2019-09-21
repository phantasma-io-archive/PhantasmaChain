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

    public struct GasLoanEntry
    {
        public Hash hash;
        public Address borrower;
        public Address lender;
        public BigInteger amount;
        public BigInteger interest;
    }

    public sealed class GasContract : SmartContract
    {
        public override string Name => "gas";

        internal StorageMap _allowanceMap; //<Address, BigInteger>
        internal StorageMap _allowanceTargets; //<Address, Address>

        internal StorageMap _loanMap; // Address, GasLendEntry
        internal StorageMap _loanList; // Address, List<Address>
        internal StorageMap _lenderMap; // Address, Address
        internal StorageList _lenderList; // Address

        public const int LendReturn = 50;

        internal const string MaxLoanAmountTag = "gas.max.loan"; //9999 * 10000;
        internal const string MaxLenderCountTag = "gas.lender.count";

        public void AllowGas(Address user, Address target, BigInteger price, BigInteger limit)
        {
            if (Runtime.readOnlyMode)
            {
                return;
            }

            Runtime.Expect(user.IsUser, "must be a user address");
            Runtime.Expect(IsWitness(user), "invalid witness");
            Runtime.Expect(target.IsSystem, "destination must be system address");

            Runtime.Expect(price > 0, "price must be positive amount");
            Runtime.Expect(limit > 0, "limit must be positive amount");

            var maxAmount = price * limit;

            var allowance = _allowanceMap.ContainsKey(user) ? _allowanceMap.Get<Address, BigInteger>(user) : 0;
            Runtime.Expect(allowance == 0, "unexpected pending allowance");

            allowance += maxAmount;
            _allowanceMap.Set(user, allowance);
            _allowanceTargets.Set(user, target);

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, user, this.Address, maxAmount), "gas escrow failed");
            Runtime.Notify(EventKind.GasEscrow, user, new GasEventData() { address = target, price = price, amount = limit });
        }

        public void LoanGas(Address from, BigInteger price, BigInteger limit)
        {
            if (Runtime.readOnlyMode)
            {
                return;
            }

            var pow = Runtime.Transaction.Hash.GetDifficulty();
            Runtime.Expect(pow >= (int)ProofOfWork.Moderate, "expected proof of work");

            Runtime.Expect(Runtime.Chain.IsRoot, "must be a root chain");

            Runtime.Expect(from.IsUser, "must be a user address");
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(price > 0, "price must be positive amount");
            Runtime.Expect(limit > 0, "limit must be positive amount");

            var lender = FindLender();
            Runtime.Expect(!lender.IsNull, "no lender available");

            BigInteger lendedAmount;

            Runtime.Expect(IsLender(lender), "invalid lender address");

            Runtime.Expect(GetLoanAmount(from) == 0, "already has an active loan");

            lendedAmount = price * limit;

            var maxLoanAmount = Runtime.GetGovernanceValue(MaxLoanAmountTag);
            Runtime.Expect(lendedAmount <= maxLoanAmount, "limit exceeds maximum allowed for lend");

            var loan = new GasLoanEntry()
            {
                amount = lendedAmount,
                hash = Runtime.Transaction.Hash,
                borrower = from,
                lender = lender,
                interest = 0
            };
            _loanMap.Set<Address, GasLoanEntry>(from, loan);

            var list = _loanList.Get<Address, StorageList>(lender);
            list.Add<Address>(from);

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, lender, from, loan.amount), "gas lend failed");
            Runtime.Notify(EventKind.GasLoan, from, new GasEventData() { address = lender, price = price, amount = limit });
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

            var leftoverAmount = availableAmount - requiredAmount;

            var targetAddress = _allowanceTargets.Get<Address, Address>(from);
            BigInteger targetGas;

            if (!targetAddress.IsNull)
            {
                targetGas = spentGas / 2; // 50% for dapps
            }
            else
            {
                targetGas = 0;
            }

            // TODO the transfers around here should pass through Nexus.TransferTokens!!
            // return unused gas to transaction creator
            if (leftoverAmount > 0)
            {
                Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, this.Address, from, leftoverAmount), "gas leftover return failed");
            }

            if (targetGas > 0)
            {
                var targetPayment = targetGas * Runtime.GasPrice;
                Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, this.Address, targetAddress, targetPayment), "gas target payment failed");
                Runtime.Notify(EventKind.GasPayment, targetAddress, new GasEventData() { address = from, price = Runtime.GasPrice, amount = targetGas });
                spentGas -= targetGas;
            }

            if (spentGas > 0)
            {
                var validatorPayment = spentGas * Runtime.GasPrice;
                var validatorAddress = Runtime.GetContractAddress("validator");
                Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, this.Address, validatorAddress, validatorPayment), "gas validator payment failed");
                Runtime.Notify(EventKind.GasPayment, validatorAddress, new GasEventData() { address = from, price = Runtime.GasPrice, amount = spentGas });
                spentGas = 0;
            }

            _allowanceMap.Remove(from);
            _allowanceTargets.Remove(from);

            // check if there is an active lend and it is time to pay it
            if (_loanMap.ContainsKey<Address>(from))
            {
                var loan = _loanMap.Get<Address, GasLoanEntry>(from);

                if (loan.hash == Runtime.Transaction.Hash)
                {
                    var unusedLoanAmount = loan.amount - requiredAmount;
                    Runtime.Expect(unusedLoanAmount >= 0, "loan amount overflow");

                    // here we return the gas to the original pool, not the the payment address, because this is not a payment
                    Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, from, loan.lender, unusedLoanAmount), "unspend loan payment failed");
                    Runtime.Notify(EventKind.GasPayment, loan.borrower, new GasEventData() { address = from, price = 1, amount = unusedLoanAmount});

                    loan.amount = requiredAmount;
                    loan.interest = (requiredAmount * LendReturn) / 100;
                    _loanMap.Set<Address, GasLoanEntry>(from, loan);
                }
                else
                {
                    Runtime.Expect(_lenderMap.ContainsKey<Address>(loan.lender), "missing payment address for loan");
                    var paymentAddress = _lenderMap.Get<Address, Address>(loan.lender);

                    Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, from, loan.lender, loan.amount), "loan payment failed");
                    Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, from, paymentAddress, loan.interest), "loan interest failed");
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
                    Runtime.Notify(EventKind.GasPayment, paymentAddress, new GasEventData() { address = from, price = 1, amount = loan.interest});
                }
            }
        }

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

        private Address FindLender()
        {
            var count = _lenderList.Count();
            if (count > 0)
            {
                var index = Runtime.NextRandom() % count;
                return _lenderList.Get<Address>(index);
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
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(to.IsUser, "invalid destination address");
            Runtime.Expect(!IsLender(from), "already lending at source address");
            Runtime.Expect(!IsLender(to), "already lending at destination address");

            _lenderList.Add<Address>(from);
            _lenderMap.Set<Address, Address>(from, to);

            Runtime.Notify(EventKind.AddressLink, from, Runtime.Chain.Address);
        }

        public void StopLend(Address from)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
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
    }
}
