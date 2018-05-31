using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Phantasma.VM
{
    public enum VMType
    {
        None,
        Object,
        Bytes,
        Number,
        String,
        Bool
    }

    public class VMObject
    {
        public VMType Type { get; private set; }
        public int Length => Data.Length;
        public bool IsEmpty => Data.Length == 0;

        private Dictionary<string, VMObject> _children;

        private static readonly byte[] Empty = new byte[0] { };

        private byte[] _data;
        public byte[] Data
        {
            get
            {
                if (_children != null)
                {
                    return Empty;
                }

                return _data != null ? _data : Empty;
            }

            set
            {
                _data = value != null ? value : Empty;
            }
        }

        public int Size
        {
            get
            {
                int total = 0;
                if (_data != null)
                {
                    total += _data.Length;
                }

                if (_children != null)
                {
                    foreach (var entry in _children.Values)
                    {
                        total += entry.Size;
                    }
                }

                return total;
            }
        }

        public VMObject()
        {
            this.Type = VMType.None;
        }

        public BigInteger AsNumber()
        {
            if (_children != null || this.Type != VMType.Number)
            {
                throw new Exception("Invalid cast");
            }

            return new BigInteger(Data);
        }

        public string AsString()
        {
            if (_children != null || this.Type != VMType.String)
            {
                throw new Exception("Invalid cast");
            }

            return Encoding.UTF8.GetString(Data);
        }

        public bool AsBool()
        {
            if (_children != null || this.Type != VMType.Bool)
            {
                throw new Exception("Invalid cast");
            }

            var temp = Data;
            return temp.Length == 1 && temp[0] != 0;
        }

        public void SetValue(BigInteger val)
        {
            this.Type = VMType.Number;
            this.Data = val.ToByteArray();
        }

        public void SetValue(string val)
        {
            this.Type = VMType.String;
            this.Data = Encoding.UTF8.GetBytes(val);
        }

        public void SetValue(bool val)
        {
            this.Type = VMType.Bool;
            this.Data = new byte[1] { (byte)(val ? 1 : 0) };
        }

        public void SetKey(string key, VMObject obj)
        {
            this.Type = VMType.Object;

            if (_children == null)
            {
                _children = new Dictionary<string, VMObject>();
            }

            _children[key] = obj;
        }

        public void SetKey(BigInteger key, VMObject obj)
        {
            SetKey(key.ToString(), obj);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is VMObject))
            {
                return false;
            }

            var temp = (VMObject)obj;

            if (temp.Type != this.Type)
            {
                return false;
            }

            if (temp._children != null)
            {
                foreach (var entry in _children)
                {
                    if (!temp._children.ContainsKey(entry.Key))
                    {
                        return false;
                    }

                    var A = this._children[entry.Key];
                    var B = temp._children[entry.Key];

                    if (A != B)
                    {
                        return false;
                    }
                }

                foreach (var entry in temp._children)
                {
                    if (!this._children.ContainsKey(entry.Key))
                    {
                        return false;
                    }

                }

                return true;
            }
            else
            {
                return temp.Data.SequenceEqual(this.Data);
            }
        }

        public override int GetHashCode()
        {
            return Data.GetHashCode();
        }

        public static bool operator ==(VMObject a, VMObject b)
        {
            return a.Data.SequenceEqual(b.Data);
        }

        public static bool operator !=(VMObject a, VMObject b)
        {
            return !(a == b);
        }

        internal void Copy(VMObject other)
        {
            if (other == null || other.Type == VMType.None)
            {
                this.Type = VMType.None;
                this._data = null;
                this._children = null;
                return;
            }

            if (other.Type == VMType.Object)
            {

            }
            else
            {
                var temp = other.Data;
                _data = new byte[temp.Length];
                Array.Copy(temp, _data, _data.Length);
            }
        }
    }

}
