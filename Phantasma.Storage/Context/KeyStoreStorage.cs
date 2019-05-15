using System;

namespace Phantasma.Storage.Context
{
    public class KeyStoreStorage : StorageContext
    {
        private IKeyValueStoreAdapter _adapter;

        public KeyStoreStorage(IKeyValueStoreAdapter adapter)
        {
            this._adapter = adapter;
        }

        public override void Clear()
        {
            throw new NotImplementedException();
        }

        public override void Delete(StorageKey key)
        {
            _adapter.Remove(key.keyData);
        }

        public override byte[] Get(StorageKey key)
        {
            return _adapter.GetValue(key.keyData);
        }

        public override bool Has(StorageKey key)
        {
            return _adapter.ContainsKey(key.keyData);
        }

        public override void Put(StorageKey key, byte[] value)
        {
            _adapter.SetValue(key.keyData, value);
        }
    }
}
