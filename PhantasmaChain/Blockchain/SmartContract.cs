using System;
using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Mathematics;
using Phantasma.Utils;
using Phantasma.VM.Contracts;

namespace Phantasma.Blockchain
{
    public abstract class SmartContract : IContract
    {
        public BigInteger Order { get; internal set; }

        internal Hash SignatureHash { get; private set; }
        protected byte[] SignatureKey;

        public abstract Address Address { get; }
        public abstract byte[] Script { get; }
        public abstract ContractInterface ABI { get; }

        private Dictionary<byte[], byte[]> _storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        protected Transaction Transaction { get; private set; }
        protected Chain Chain { get; private set; }

        public SmartContract()
        {
            this.Order = 0;

            var n = new BigInteger(this.Address.PublicKey);
            n += Environment.TickCount;

            this.SignatureKey = n.ToByteArray();
            this.SignatureHash = new Hash(this.SignatureKey);
        }

        public void SetData(Chain chain, Transaction tx)
        {
            this.Chain = chain;
            this.Transaction = tx;
        }

        protected void Expect(bool condition)
        {
            Throw.If(!condition, "contract assertion failed");
        }

        public byte[] ReadStorage(byte[] key)
        {
            return _storage.ContainsKey(key) ? _storage[key] : null;
        }

        public void WriteStorage(byte[] key, byte[] value)
        {
            _storage[key] = value;
        }

        public int GetSize()
        {
            return this.Address.PublicKey.Length + this.Script.Length;
        }

        protected byte[] GetSignatureKey()
        {
            return SignatureKey;
        }

        public void Sign(Transaction tx, byte[] signatureKey)
        {
            Throw.If(tx == null, "null transaction");
            Throw.If(tx != this.Transaction, "invalid transaction");

            var signature = new ContractSignature(this, signatureKey);
            tx.Sign(signature);
        }
    }
}
