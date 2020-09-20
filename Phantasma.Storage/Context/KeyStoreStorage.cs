using System;
using System.Collections.Generic;

namespace Phantasma.Storage.Context
{
    public class KeyStoreStorage : StorageContext
    {
        public readonly IKeyValueStoreAdapter Adapter;

        public KeyStoreStorage(IKeyValueStoreAdapter adapter)
        {
            this.Adapter = adapter;
        }

        public override void Clear()
        {
            throw new NotImplementedException();
        }

        public override void Delete(StorageKey key)
        {
            Adapter.Remove(key.keyData);
        }

        public override byte[] Get(StorageKey key)
        {
            return Adapter.GetValue(key.keyData);
        }

        public override bool Has(StorageKey key)
        {
            return Adapter.ContainsKey(key.keyData);
        }

        public override void Put(StorageKey key, byte[] value)
        {
            Adapter.SetValue(key.keyData, value);
        }

        public override void Visit(Action<byte[], byte[]> visitor, ulong searchCount = 0, byte[] prefix = null)
        {
            Adapter.Visit((keyBytes, valBytes) =>
            {
                visitor(keyBytes, valBytes);
            }, searchCount, prefix);
        }
    }
}
