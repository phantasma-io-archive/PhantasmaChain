using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Core.Utils
{
    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return left.SequenceEqual(right);
        }

        public int GetHashCode(byte[] key)
        {
            Throw.IfNull(key , "key");
            return key.Sum(b => b);
        }
    }

}
