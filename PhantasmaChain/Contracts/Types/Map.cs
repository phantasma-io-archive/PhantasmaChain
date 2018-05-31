using System;
using System.Numerics;

namespace Phantasma.Contracts.Types
{
    public interface Map<Key, Value>
    {
        void Put(Key key, Value val);
        Value Get(Key key);
        bool Remove(Key key);
        bool Contains(Key key);    
        void Clear();
        void Iterate(Action<Key, Value> visitor);

        BigInteger Count { get; }
    }
}
