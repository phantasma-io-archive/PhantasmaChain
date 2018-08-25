using Phantasma.Mathematics;
using Phantasma.Cryptography;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Contracts
{
    public class TokenContract : NativeContract
    {
        internal override NativeContractKind Kind => NativeContractKind.Token;

        public string Symbol => "SOUL";
        public string Name => "Phantasma";

        public BigInteger MaxSupply => 93000000;
        public BigInteger CirculatingSupply => _supply;
        public BigInteger Decimals => 8;

        public TokenContract(): base()
        {
        }

        private BigInteger _supply = 0;
        private Dictionary<Address, BigInteger> _balances = new Dictionary<Address, BigInteger>();

        private Address ownerAddress = Address.Null;

        public void Burn(Address target, BigInteger amount)
        {
            Expect(amount > 0);
            Expect(_balances.ContainsKey(target));
            Expect(Transaction.IsSignedBy(target));

            var balance = _balances[target];
            Expect(balance >= amount);

            balance -= amount;
            _balances[target] = amount;
            this._supply -= amount;
        }

        public void Mint(Address target, BigInteger amount)
        {
            Expect(amount > 0);
            
            if (ownerAddress != Address.Null)
            {
                Expect(target == ownerAddress);
            }
            else
            {
                ownerAddress = target;
            }

            Expect(Transaction.IsSignedBy(target));

            this._supply += amount;
            Expect(_supply <= MaxSupply);

            var balance = _balances.ContainsKey(target)? _balances[target] : 0;

            balance += amount;
            _balances[target] = amount;
        }

        public void Transfer(Address source, Address destination, BigInteger amount)
        {
            Expect(amount > 0);
            Expect(source != destination);
            Expect(Transaction.IsSignedBy(source));

            Expect(_balances.ContainsKey(source));

            BigInteger from_balance =  _balances[source];
            Expect(from_balance >= amount);

            from_balance -= amount;
            if (from_balance == 0)
                _balances.Remove(source);
            else
                _balances[source] = from_balance;

            BigInteger to_balance;
            if (_balances.ContainsKey(destination))
            {
                to_balance = _balances[destination];
            }
            else
            {
                to_balance = 0;
            }

            to_balance += amount;

            _balances[destination] = to_balance;
        }

        public BigInteger BalanceOf(Address address)
        {
            return _balances.ContainsKey(address) ? _balances[address]: 0;
        }
    }
}