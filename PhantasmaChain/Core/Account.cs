using Phantasma.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Phantasma.Core
{
    public class Account
    {
        public readonly byte[] PublicKey;
        public uint txIndex { get; internal set; }

        private Dictionary<Token, BigInteger> _balances = new Dictionary<Token, BigInteger>();

        public readonly Chain Chain;

        public Account(Chain chain, byte[] publicKey)
        {
            this.PublicKey = publicKey;
            this.Chain = chain;
            this.txIndex = 0;
        }

        public BigInteger GetBalance(Token token)
        {
            if (_balances.ContainsKey(token))
            {
                return _balances[token];
            }

            return 0;
        }

        public BigInteger Withdraw(Token token, BigInteger amount, Action<Event> notify)
        {
            if (!_balances.ContainsKey(token))
            {
                throw new Exception("No balance for this token");
            }

            var balance = _balances[token];
            if (balance < amount)
            {
                throw new Exception("Insuficient balance for this token");
            }

            if (balance == amount)
            {
                _balances.Remove(token);
                balance = 0;
            }
            else
            {
                balance -= amount;
                _balances[token] = balance;
            }

            Chain.Log($"Withdraw {amount} {token.Name} from {CryptoUtils.PublicKeyToAddress(this.PublicKey)}");

            this.txIndex++;
            notify(new Event(EventKind.Withdraw, this.PublicKey));

            return balance;
        }

        public BigInteger Deposit(Token token, BigInteger amount, Action<Event> notify)
        {
            Chain.Log($"Deposit {amount} {token.Name} to {CryptoUtils.PublicKeyToAddress(this.PublicKey)}");

            this.txIndex++;
            notify(new Event(EventKind.Deposit, this.PublicKey));

            if (!_balances.ContainsKey(token))
            {
                _balances[token] = amount;
                return amount;
            }
            else
            {
                var balance = _balances[token];
                balance += amount;
                _balances[token] = balance;
                return balance;
            }
        }

    }
}
