using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Core.Utils
{
    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] left, byte[] right)
        {
            return left.CompareBytes(right);
        }

        public int GetHashCode(byte[] key)
        {
            Throw.IfNull(key, nameof(key));
            unchecked // disable overflow, for the unlikely possibility that you
            {         // are compiling with overflow-checking enabled
                int hash = 27;
                for (int i=0; i<key.Length; i++)
                {
                    hash = (13 * hash) + key[i];
                }
                return hash;
            }
        }
    }

}
