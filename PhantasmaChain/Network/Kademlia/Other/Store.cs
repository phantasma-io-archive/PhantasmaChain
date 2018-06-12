using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Phantasma.Network.Kademlia
{
    public class NodeStore
    {
        private readonly IDictionary<string, string> store;

        public NodeStore()
        {
            this.store = new ConcurrentDictionary<string, string>();
        }

        public bool AddValue(string key, string value)
        {
            if (this.store.ContainsKey(key))
            {
                throw new Exception($"Duplicated key: {key}");
            }

            this.store.Add(key, value);

            // We should also return false if the store is full
            return true;
        }

        public bool ContainsKey(string key)
        {
            return this.store.ContainsKey(key);
        }

        public string GetValue(string key)
        {
            string value = null;

            this.store.TryGetValue(key, out value);

            return value;
        }

        public bool RemoveValue(string key)
        {
            return this.store.Remove(key);
        }
    }
}
