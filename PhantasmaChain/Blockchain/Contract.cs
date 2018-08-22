using System.Collections.Generic;
using Phantasma.Mathematics;
using Phantasma.Utils;
using Phantasma.VM;
using Phantasma.VM.Contracts;
using Phantasma.VM.Types;

namespace Phantasma.Blockchain
{
    [VMType]
    public abstract class Contract : IContract
    {
        public BigInteger Order { get; internal set; }

        public abstract Address Address { get; }
        public abstract byte[] Script { get; }
        public abstract ContractInterface ABI { get; }

        private Dictionary<byte[], byte[]> _storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public Contract()
        {
            this.Order = 0;
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
    }
}
