using Phantasma.Cryptography;
using Phantasma.IO;
using System;

namespace Phantasma.Blockchain.Storage
{
    public class DiskStorageContext : StorageContext
    {
        private MemoryStore _memory;
        private DiskStore _disk;

        public DiskStorageContext(Address address, string name, KeyStoreDataSize dataSize)
        {
            _memory = new MemoryStore(32);
            _disk = new DiskStore(address.Text + "_" + name, dataSize);
        }

        private Hash CreateHash(StorageKey key)
        {
            return Hash.FromBytes(key.keyData);
        }

        public override void Clear()
        {
            throw new NotImplementedException();
        }

        public override void Delete(StorageKey key)
        {
            var hash = CreateHash(key);
            if (_disk.Remove(hash))
            {
                _memory.Remove(hash);
            }
        }

        public override byte[] Get(StorageKey key)
        {
            var hash = CreateHash(key);
            if (_memory.ContainsKey(hash))
            {
                return _memory.GetValue(hash);
            }

            if (_disk.ContainsKey(hash))
            {
                return _disk.GetValue(hash);
            }

            return null;
        }

        public override bool Has(StorageKey key)
        {
            var hash = CreateHash(key);
            return _disk.ContainsKey(hash);
        }

        public override void Put(StorageKey key, byte[] value)
        {
            var hash = CreateHash(key);
            _disk.SetValue(hash, value);
            _memory.SetValue(hash, value);
        }
    }
}
