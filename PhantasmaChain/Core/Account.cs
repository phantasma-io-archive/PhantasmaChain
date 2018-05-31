using Phantasma.Contracts;
using Phantasma.Utils;
using System.Collections.Generic;
using System.Numerics;

namespace Phantasma.Core
{
    public class Account : IContract
    {
        public byte[] PublicKey { get; }

        public BigInteger Order { get; internal set; }
        public byte[] Script { get; private set; }

        private Dictionary<byte[], byte[]> _storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public readonly Chain Chain;

        public Account(Chain chain, byte[] publicKey)
        {
            this.PublicKey = publicKey;
            this.Chain = chain;
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
    }
}
