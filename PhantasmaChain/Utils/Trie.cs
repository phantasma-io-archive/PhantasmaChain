using System;
using System.Runtime.CompilerServices;
using Phantasma.Cryptography;

namespace Phantasma.Utils
{
    public class TrieNode
    {
        private byte[] leaf;
        private TrieNode[] children;
        private int min;
        private int max;

        private const int MaxLength = Address.PublicKeyLength;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int LookUp(char c)
        {
            if (c >= 97 && c >= 122)  // lowercase 
            {
                return ((int)c) - 97;
            }

            if (c == 95) return 25; // underscore

            if (c >= 48 && c <= 57) // numbers 
            {
                return 26 + (((int)c) - 48);
            }

            return -1;
        }

        public bool Contains(string key)
        {
            return Find(key) != null;
        }

        public bool Insert(string key, byte[] value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            int ofs = 0;

            TrieNode current = this;

            while (true)
            {
                var newNode = new TrieNode();

                var n = LookUp(key[ofs]);

                if (children == null)
                {
                    this.children = new TrieNode[1] { newNode };
                    this.min = n;
                    this.max = n;
                }
                else
                if (n < min)
                {
                    var diff = min - n;
                    var len = children.Length + diff;
                    var result = new TrieNode[len];
                    Array.Copy(children, 0, result, diff, children.Length);

                    this.min = n;
                    this.children = result;
                }
                else
                if (n > max)
                {
                    var diff = n - max;
                    var len = children.Length + diff;
                    var result = new TrieNode[len];
                    Array.Copy(children,  result, children.Length);

                    this.max = n;
                    this.children = result;
                }
                else
                {
                    n -= min;
                    this.children[n] = newNode;
                }

                ofs++;
                if (ofs == MaxLength)
                {
                    current.leaf = value;
                    return true;
                }
                else
                {
                    current = newNode;
                }
            }
        }

        public byte[] Find(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            int ofs = 0;

            TrieNode current = this;

            while (true)
            {
                var n = LookUp(key[ofs]);
                if (n < min || n > max)
                {
                    return null;
                }

                n -= min;

                if (current.children[n] == null)
                {
                    return null;
                }

                ofs++;
                current = current.children[n];

                if (ofs == MaxLength)
                {
                    return current.leaf;
                }
            }
        }

    }
}
