using PhantasmaChain.Cryptography;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace PhantasmaChain.Core
{
    public enum TransactionKind
    {
        Create,
        Transfer,
        Burn,
        Mint,
        Order,
        Cancel
    }

    public abstract class Transaction
    {
        public readonly byte[] PublicKey;
        public readonly BigInteger Fee;
        public readonly uint txOrder;
        public readonly TransactionKind Kind;

        public byte[] Signature { get; private set; }
        public byte[] Hash { get; private set; }

        protected abstract void UnserializeData(BinaryReader reader);
        protected abstract void SerializeData(BinaryWriter writer);

        protected abstract bool ValidateData(Chain chain);

        protected abstract void Apply(Chain chain, Action<Event> notify);

        public abstract BigInteger GetCost(Chain chain);

        public Transaction(TransactionKind kind, byte[] publicKey, BigInteger fee, uint txOrder)
        {
            this.Kind = kind;
            this.PublicKey = publicKey;
            this.Fee = fee;
            this.txOrder = txOrder;

            this.UpdateHash();
        }

        public byte[] ToArray(bool withSignature)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write((byte)Kind);
                }

                return stream.ToArray();
            }
        }

        public bool Sign(KeyPair owner)
        {
            if (this.Signature != null)
            {
                return false;
            }

            if (owner == null)
            {
                return false;
            }

            var msg = this.ToArray(false);
            this.Signature = CryptoUtils.Sign(msg, owner.PrivateKey, owner.PublicKey);

            return true;
        }

        public bool IsValid(Chain chain)
        {
            if (this.Signature == null)
            {
                return false;
            }

            if (chain.NativeToken != null)
            {
                var cost = this.GetCost(chain);
                if (this.Fee < cost)
                {
                    return false;
                }

                var account = chain.GetAccount(this.PublicKey);
                if (account == null)
                {
                    return false;
                }

                if (account.GetBalance(chain.NativeToken) < this.Fee)
                {
                    return false;
                }
            }

            var data = ToArray(false);
            if (!CryptoUtils.VerifySignature(data, this.Signature, this.PublicKey))
            {
                return false;
            }

            return ValidateData(chain);
        }

        public BigInteger Execute(Chain chain, Action<Event> notify)
        {
            var cost = this.GetCost(chain);

            if (chain.NativeToken != null && cost > 0)
            {
                var account = chain.GetAccount(this.PublicKey);
                account.Withdraw(chain.NativeToken, cost, notify);
            }

            this.Apply(chain, notify);

            return cost;
        }

        private void UpdateHash()
        {
            var data = this.ToArray(true);
            this.Hash = CryptoUtils.Sha256(data);
        }

    }
}
