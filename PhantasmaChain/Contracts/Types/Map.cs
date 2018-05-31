using System;
using System.Numerics;

namespace Phantasma.Contracts.Types
{
    public class Map<Key, Value>
    {
        public extern void Put(Key key, Value val);
        public extern Value Get(Key key);
        public extern bool Remove(Key key);
        public extern bool Contains(Key key);    
        public extern void Clear();
        public extern void Iterate(Action<Key, Value> visitor);

        public extern BigInteger Count { get; }
    }
}
