using Phantasma.Core;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Storage
{
    public struct StorageChangeSetEntry
    {
        public byte[] oldValue;
        public byte[] newValue;
    }

    public class StorageChangeSetContext: StorageContext
    {
        public StorageContext baseContext { get; private set; }

        public readonly Dictionary<StorageKey, StorageChangeSetEntry> _entries = new Dictionary<StorageKey, StorageChangeSetEntry>(new StorageKeyComparer());

        public StorageChangeSetContext(StorageContext baseContext)
        {
            Throw.IfNull(baseContext, "base context");
            this.baseContext = baseContext;
        }

        public override void Clear()
        {
            _entries.Clear();
        }

        public override bool Has(StorageKey key)
        {
            if (_entries.ContainsKey(key))
            {
                return true;
            }

            return baseContext.Has(key);
        }

        public override byte[] Get(StorageKey key)
        {
            if (_entries.ContainsKey(key))
            {
                return _entries[key].newValue;
            }

            return baseContext.Get(key);
        }

        public override void Put(StorageKey key, byte[] value)
        {
            StorageChangeSetEntry change;

            if (_entries.ContainsKey(key))
            {
                change = _entries[key];
                change.newValue = value;
            }
            else
            {
                change = new StorageChangeSetEntry()
                {
                    oldValue = baseContext.Get(key),
                    newValue = value,
                };
            }

            _entries[key] = change;
        }

        public override void Delete(StorageKey key)
        {
            Put(key, null);
        }
    }
}
