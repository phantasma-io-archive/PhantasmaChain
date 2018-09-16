using System;

namespace Phantasma.Core.Types
{
    public interface IMap<Key, Value>
    {
        void Put(Key key, Value val);
        Value Get(Key key);
        bool Remove(Key key);
        bool Contains(Key key);
        void Clear();
        void Iterate(Action<Key, Value> visitor);

        uint Count { get; }
    }

}
