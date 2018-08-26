using Phantasma.Mathematics;
using Phantasma.Cryptography;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Contracts
{
    public class TokenContract : NativeContract
    {
        internal override NativeContractKind Kind => NativeContractKind.Token;

        public static string Symbol => "SOUL";
        public static string Name => "Phantasma";

        public static readonly BigInteger MaxSupply = 93000000;

        public TokenContract(): base()
        {
        }

        private BigInteger _supply = 0;
        private Dictionary<Address, BigInteger> _balances = new Dictionary<Address, BigInteger>();

        public string GetName() => Name;
        public string GetSymbol() => Symbol;
        public BigInteger GetSupply() => MaxSupply;
        public BigInteger GetDecimals() => 8;

        public void Mint(Address target, BigInteger amount)
        {
            Expect(amount > 0);
            Expect(IsWitness(target));

            var nexusContract = (NexusContract) this.Chain.FindContract(NativeContractKind.Nexus);
            var mintAddress = nexusContract.Address;
        
            if (_supply == 0)
            {
                if (Chain.IsRoot)
                {
                    Expect(amount == MaxSupply);
                }
                else
                {
                    Expect(target == mintAddress);
                }
            }
            else
            {
                Expect(target == mintAddress);
            }

            this._supply += amount;
            Expect(_supply <= MaxSupply);

            var balance = _balances.ContainsKey(target)? _balances[target] : 0;

            balance += amount;
            _balances[target] = amount;
        }

        public void Burn(Address target, BigInteger amount)
        {
            Expect(amount > 0);
            Expect(_balances.ContainsKey(target));
            Expect(IsWitness(target));

            var balance = _balances[target];
            Expect(balance >= amount);

            balance -= amount;
            _balances[target] = amount;
            this._supply -= amount;
        }

        public void Transfer(Address source, Address destination, BigInteger amount)
        {
            Expect(amount > 0);
            Expect(source != destination);
            Expect(IsWitness(source));

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