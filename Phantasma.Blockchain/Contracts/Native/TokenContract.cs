using Phantasma.Numerics;
using Phantasma.Cryptography;
using System.Collections.Generic;
using Phantasma.Blockchain.Contracts.Types;
using Phantasma.Core.Utils;
using Phantasma.VM.Utils;

namespace Phantasma.Blockchain.Contracts.Native
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

        public string GetName() => Name;
        public string GetSymbol() => Symbol;
        public BigInteger GetSupply() => MaxSupply;
        public BigInteger GetDecimals() => 8;

        private Map<Address, BigInteger> GetBalances()
        {
            return this.Storage.FindMapForContract<Address, BigInteger>("balances".AsByteArray());
        }

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
    }
}