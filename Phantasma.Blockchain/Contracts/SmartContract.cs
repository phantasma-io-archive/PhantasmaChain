using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.VM.Contracts;
using Phantasma.Core.Utils;
using Phantasma.Blockchain.Storage;

namespace Phantasma.Blockchain.Contracts
{
    public abstract class SmartContract : IContract
    {
        public BigInteger Order { get; internal set; }

        public abstract byte[] Script { get; }
        public abstract ContractInterface ABI { get; }

        private Dictionary<byte[], byte[]> _storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public RuntimeVM Runtime { get; private set; }
        public StorageContext Storage { get; private set; }

        public SmartContract()
        {
            this.Order = 0;
        }

        internal void SetRuntimeData(RuntimeVM VM, StorageContext storage)
        {
            this.Runtime = VM;
            this.Storage = storage;
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
            return this.Script.Length;
        }

        public bool IsWitness(Address address)
        {
            if (address == this.Runtime.Chain.Address) // TODO this is not right...
            {
                return true;
            }

            return Runtime.Transaction.IsSignedBy(address);
        }
    }
}
