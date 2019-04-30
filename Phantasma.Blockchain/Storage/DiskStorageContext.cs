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

        public override void Clear()
        {
            throw new NotImplementedException();
        }

        public override void Delete(StorageKey key)
        {
            if (_disk.Remove(key.keyData))
            {
                _memory.Remove(key.keyData);
            }
        }

        public override byte[] Get(StorageKey key)
        {
            if (_memory.ContainsKey(key.keyData))
            {
                return _memory.GetValue(key.keyData);
            }

            if (_disk.ContainsKey(key.keyData))
            {
                return _disk.GetValue(key.keyData);
            }

            return null;
        }

        public override bool Has(StorageKey key)
        {
            return _disk.ContainsKey(key.keyData);
        }

        public override void Put(StorageKey key, byte[] value)
        {
            _disk.SetValue(key.keyData, value);
            _memory.SetValue(key.keyData, value);
        }
    }
}
