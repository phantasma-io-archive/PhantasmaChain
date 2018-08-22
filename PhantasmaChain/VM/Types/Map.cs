using Phantasma.Mathematics;
using System;

namespace Phantasma.VM.Types
{
    [VMType]
    public interface IMap<Key, Value>
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
