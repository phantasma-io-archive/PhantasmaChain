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

            if (left.Length != right.Length)
            {
                return false;
            }

            for (int i=0; i<left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(byte[] key)
        {
            Throw.IfNull(key, nameof(key));
            return key.Sum(b => b);
        }
    }

}
