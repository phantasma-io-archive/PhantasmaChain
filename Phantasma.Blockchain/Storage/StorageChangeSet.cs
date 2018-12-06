using Phantasma.Core;
using System;
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

        private readonly Dictionary<StorageKey, StorageChangeSetEntry> _entries = new Dictionary<StorageKey, StorageChangeSetEntry>(new StorageKeyComparer());
//        public IEnumerable<KeyValuePair<StorageKey, StorageChangeSetEntry>> Entries => _entries;

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
                var entry = _entries[key];
                return entry.newValue != null;
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

        public override void Put(StorageKey key, byte[] newValue)
        {
            StorageChangeSetEntry change;

            if (_entries.ContainsKey(key))
            {
                change = _entries[key];
                change.newValue = newValue;
            }
            else
            {
                byte[] oldValue;

                if (baseContext.Has(key))
                {
                    oldValue = baseContext.Get(key);
                }
                else
                {
                    oldValue = null;
                }

                change = new StorageChangeSetEntry()
                {
                    oldValue = oldValue,
                    newValue = newValue,
                };
            }

            _entries[key] = change;
        }

        public override void Delete(StorageKey key)
        {
            Put(key, null);
        }

        public void Execute()
        {
            foreach (var entry in _entries)
            {
                if (entry.Value.newValue == null)
                {
                    baseContext.Delete(entry.Key);
                }
                else
                {
                    baseContext.Put(entry.Key, entry.Value.newValue);
                }
            }
        }

        public void Undo()
        {
            foreach (var entry in _entries)
            {
                if (entry.Value.oldValue == null)
                {
                    baseContext.Delete(entry.Key);
                }
                else
                {
                    baseContext.Put(entry.Key, entry.Value.oldValue);
                }
            }
        }

        public bool Any()
        {
            return _entries.Count > 0;
        }
    }
}
