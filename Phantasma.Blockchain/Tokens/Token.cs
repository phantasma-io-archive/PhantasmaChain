using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Blockchain.Types;
using Phantasma.VM.Utils;

namespace Phantasma.Blockchain.Tokens
{
    public class Token
    {
        public string Symbol { get; private set; }
        public string Name { get; private set; }

        public BigInteger MaxSupply { get; private set; } // = 93000000;

        private BigInteger _supply = 0;

        private StorageContext _storage;

        public Token(StorageContext storage)
        {
            this._storage = storage;
        }

        public string GetName() => Name;
        public string GetSymbol() => Symbol;
        public BigInteger GetSupply() => MaxSupply;
        public BigInteger GetDecimals() => 8;

/*        private Map<Address, BigInteger> GetBalances()
        {
            return this.Storage.FindMapForContract<Address, BigInteger>("balances".AsByteArray());
        }

        public void Mint(Address target, BigInteger amount)
        {
            Expect(amount > 0);
            Expect(IsWitness(target));

            var nexusContract = (NexusContract) this.Chain.FindContract(ContractKind.Nexus);
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

            var balances = GetBalances();
            var balance = balances.ContainsKey(target)? balances[target] : 0;

            balance += amount;
            balances[target] = amount;
        }

        public void Burn(Address target, BigInteger amount)
        {
            var balances = GetBalances();

            Expect(amount > 0);
            Expect(balances.ContainsKey(target));
            Expect(IsWitness(target));

            var balance = balances[target];
            Expect(balance >= amount);

            balance -= amount;
            balances[target] = amount;
            this._supply -= amount;
        }

        public void Transfer(Address source, Address destination, BigInteger amount)
        {
            var balances = GetBalances();

            Expect(amount > 0);
            Expect(source != destination);
            Expect(IsWitness(source));

            Expect(balances.ContainsKey(source));

            BigInteger from_balance =  balances[source];
            Expect(from_balance >= amount);

            from_balance -= amount;
            if (from_balance == 0)
                balances.Remove(source);
            else
                balances[source] = from_balance;

            BigInteger to_balance;
            if (balances.ContainsKey(destination))
            {
                to_balance = balances[destination];
            }
            else
            {
                to_balance = 0;
            }

            to_balance += amount;

            balances[destination] = to_balance;
        }

        public BigInteger BalanceOf(Address address)
        {
            var balances = GetBalances();
            return balances.ContainsKey(address) ? balances[address]: 0;
        }
        */
    }
}