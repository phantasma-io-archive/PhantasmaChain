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

    public struct GasLendEntry
    {
        public Hash hash;
        public Address target;
        public BigInteger amount;
    }

    public class GasContract : SmartContract
    {
        public override string Name => "gas";

        internal StorageMap _allowanceMap; //<Address, BigInteger>
        internal StorageMap _allowanceTargets; //<Address, Address>

        internal StorageMap _borrowerMap; // Address, GasLendEntry
        internal StorageList _lenderList; // Address

        public const int MaxLendAmount = 9999;
        public const int LendReturn = 50;
        public const int MaxLenderCount = 10;

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

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, user, Runtime.Chain.Address, maxAmount), "gas escrow failed");
            Runtime.Notify(EventKind.GasEscrow, user, new GasEventData() { address = Runtime.Chain.Address, price = price, amount = limit });
        }

        public void LoanGas(Address user, Address target, BigInteger price, BigInteger limit)
        {
            if (Runtime.readOnlyMode)
            {
                return;
            }

            Runtime.Expect(Runtime.Chain.IsRoot, "must be a root chain");

            Runtime.Expect(user.IsUser, "must be a user address");
            Runtime.Expect(target.IsSystem, "destination must be system address");
            Runtime.Expect(IsWitness(user), "invalid witness");

            Runtime.Expect(price > 0, "price must be positive amount");
            Runtime.Expect(limit > 0, "limit must be positive amount");

            var from = FindLender();
            Runtime.Expect(!from.IsNull, "no lender available");

            var maxAmount = price * limit;

            var allowance = _allowanceMap.ContainsKey(user) ? _allowanceMap.Get<Address, BigInteger>(user) : 0;
            Runtime.Expect(allowance == 0, "unexpected pending allowance");

            allowance += maxAmount;
            _allowanceMap.Set(user, allowance);
            _allowanceTargets.Set(user, from);

            BigInteger lendedAmount;

            Runtime.Expect(IsLender(from), "invalid lender address");

            Runtime.Expect(GetLoanAmount(user) == 0, "already has an active loan");

            lendedAmount = maxAmount;
            Runtime.Expect(lendedAmount <= MaxLendAmount, "limit exceeds maximum allowed for lend");

            var temp = (lendedAmount * LendReturn) / 100;
            var borrowEntry = new GasLendEntry()
            {
                amount = temp,
                hash = Runtime.Transaction.Hash,
                target = from
            };
            _borrowerMap.Set<Address, GasLendEntry>(user, borrowEntry);

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, from, Runtime.Chain.Address, borrowEntry.amount), "gas lend failed");
            Runtime.Notify(EventKind.GasLoan, user, new GasEventData() { address = from, price = price, amount = limit });
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

            // check if there is an active lend and it is time to pay it
            if (_borrowerMap.ContainsKey<Address>(from))
            {
                var borrowEntry = _borrowerMap.Get<Address, GasLendEntry>(from);
                if (borrowEntry.hash != Runtime.Transaction.Hash)
                {
                    Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, from, borrowEntry.target, borrowEntry.amount), "lend payment failed");
                    _borrowerMap.Remove<Address>(from);
                    Runtime.Notify(EventKind.GasPayment, borrowEntry.target, new GasEventData() { address = from, price = 1, amount = borrowEntry.amount});
                }
            }

            if (targetGas > 0)
            {
                Runtime.Notify(EventKind.GasPayment, targetAddress, new GasEventData() { address = from, price = Runtime.GasPrice, amount = targetGas });
            }

            Runtime.Notify(EventKind.GasPayment, Runtime.Chain.Address, new GasEventData() { address = from, price = Runtime.GasPrice, amount = spentGas });
        }

        public Address[] GetLenders()
        {
            return _lenderList.All<Address>();
        }

        public bool IsLender(Address address)
        {
            var count = _lenderList.Count();
            for (int i=0; i<count; i++)
            {
                var entry = _lenderList.Get<Address>(i);
                if (entry == address)
                {
                    return true;
                }
            }
            return false;
        }

        public BigInteger GetLoanAmount(Address address)
        {
            if (_borrowerMap.ContainsKey<Address>(address))
            {
                var entry = _borrowerMap.Get<Address, GasLendEntry>(address);
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

        public void StartLend(Address from)
        {
            Runtime.Expect(_lenderList.Count() < MaxLenderCount, "too many lenders already");
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(!IsLender(from), "already lending");

            _lenderList.Add<Address>(from);

            Runtime.Notify(EventKind.AddressLink, from, Runtime.Chain.Address);
        }

        public void StopLend(Address from)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

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

            Runtime.Notify(EventKind.AddressUnlink, from, Runtime.Chain.Address);
        }
    }
}
