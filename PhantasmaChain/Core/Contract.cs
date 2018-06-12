using Phantasma.Contracts;
using Phantasma.Utils;
using Phantasma.VM;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Phantasma.Core
{
    public abstract class Contract : IInteropObject, IContractABI
    {
        public BigInteger Order { get; internal set; }

        public abstract byte[] PublicKey { get; }
        public abstract byte[] Script { get; }
        public abstract byte[] ABI { get; }

        private Dictionary<byte[], byte[]> _storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public readonly Chain Chain;

        public Contract(Chain chain)
        {
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

        private static readonly byte[] NameTag = Encoding.ASCII.GetBytes(".NAME");

        public bool HasName {
            get
            {
                var tag = PublicKey.Concat(NameTag).ToArray();
                return _storage.ContainsKey(tag);
            }
        }

        public string Name
        {
            get
            {
                var tag = PublicKey.Concat(NameTag).ToArray();
                if (_storage.ContainsKey(tag))
                {
                    return Encoding.UTF8.GetString(_storage[tag]);
                }

                return PublicKey.PublicKeyToAddress();
            }
        }

        public int GetSize()
        {
            return this.PublicKey.Length + this.Script.Length;
        }
    }
}
