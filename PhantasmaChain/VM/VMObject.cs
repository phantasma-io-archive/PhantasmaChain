using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Phantasma.Cryptography;
using Phantasma.Utils;

namespace Phantasma.VM
{
    public enum VMType
    {
        None,
        Struct,
        Bytes,
        Number,
        String,
        Bool,
        Address,
        Object
    }

    public sealed class VMObject
    {
        public VMType Type { get; private set; }
        public bool IsEmpty => Data == null;
       
        public object Data { get; private set; }

        private int _localSize = 0;

        private Dictionary<string, VMObject> GetChildren() => (Dictionary<string, VMObject>)Data;

        public int Size
        {
            get
            {
                var total = 0;

                if (Type == VMType.Object)
                {
                    var children = this.GetChildren();
                    foreach (var entry in children.Values)
                    {
                        total += entry.Size;
                    }
                }
                else
                {
                    total = _localSize;
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
            if (this.Type != VMType.Number)
            {
                throw new Exception("Invalid cast");
            }

            return (BigInteger)Data;
        }

        public string AsString()
        {
            if (this.Type != VMType.String)
            {
                throw new Exception("Invalid cast");
            }

            return (string)Data;
        }

        public byte[] AsByteArray()
        {
            switch (this.Type)
            {
                case VMType.Bytes:
                case VMType.Address:
                    {
                        return (byte[])Data;
                    }

                case VMType.String:
                    {
                        var str = AsString();
                        return Encoding.UTF8.GetBytes(str);
                    }

                default:
                    {
                        throw new Exception("Invalid cast");
                    }
            }           
        }

        public byte[] AsAddress()
        {
            if (this.Type != VMType.Address)
            {
                throw new Exception("Invalid cast");
            }

            var temp = (byte[]) Data;

            if (temp.Length != KeyPair.PublicKeyLength)
            {
                throw new Exception("Invalid cast");
            }

            return temp;
        }

        public bool AsBool()
        {
            if (this.Type != VMType.Bool)
            {
                throw new Exception("Invalid cast");
            }

            return (bool)Data;
        }

        public T AsInterop<T>() where T: IInteropObject
        {
            if (this.Type != VMType.Object)
            {
                throw new Exception("Invalid cast");
            }

            return (T)Data;
        }

        public VMObject SetValue(byte[] val, VMType type)
        {
            this.Type = type;
            this._localSize = val.Length;

            switch (type)
            {
                case VMType.Address:
                case VMType.Bytes:
                    {
                        this.Data = val;
                        break;
                    }

                case VMType.Number:
                    {
                        this.Data = new BigInteger(val);
                        break;
                    }

                case VMType.String:
                    {
                        this.Data = Encoding.UTF8.GetString(val);
                        break;
                    }

                default:
                    {
                        throw new Exception("Invalid cast");
                    }
            }

            return this;
        }

        public VMObject SetValue(BigInteger val)
        {
            this.Type = VMType.Number;
            this.Data = val;
            this._localSize = val.ToByteArray().Length;
            return this;
        }

        public VMObject SetValue(IInteropObject val)
        {
            this.Type = VMType.Object;
            this.Data = val;
            this._localSize = val.GetSize();
            return this;
        }

        public VMObject SetValue(string val)
        {
            this.Type = VMType.String;
            this.Data = val;
            this._localSize = val.Length;
            return this;
        }

        public VMObject SetValue(bool val)
        {
            this.Type = VMType.Bool;
            this.Data = val;
            this._localSize = 1;
            return this;
        }

        public void SetKey(string key, VMObject obj)
        {
            Dictionary<string, VMObject> children;

            if (this.Type == VMType.Struct)
            {
                children = GetChildren();
            }
            else
            {
                this.Type = VMType.Struct;
                children = new Dictionary<string, VMObject>();
                this.Data = children;
                this._localSize = 0;
            }

            var result = new VMObject();
            children[key] = result;
            result.Copy(obj);
        }

        public VMObject GetKey(string key)
        {
            if (this.Type != VMType.Struct)
            {
                throw new Exception("Invalid cast");
            }

            var children = GetChildren();

            if (children.ContainsKey(key))
            {
                return children[key];
            }

            return new VMObject();
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

            return temp == this;
        }

        public override int GetHashCode()
        {
            return Data.GetHashCode();
        }

        public static bool operator ==(VMObject objA, VMObject objB)
        {
            if (objA.Type != objB.Type)
            {
                return false;
            }

            if (objA.Type == VMType.Struct)
            {
                var childrenA = objA.GetChildren();
                var childrenB = objB.GetChildren();

                foreach (var entry in childrenA)
                {
                    if (!childrenB.ContainsKey(entry.Key))
                    {
                        return false;
                    }

                    var A = childrenA[entry.Key];
                    var B = childrenB[entry.Key];

                    if (A != B)
                    {
                        return false;
                    }
                }

                foreach (var entry in childrenB)
                {
                    if (!childrenA.ContainsKey(entry.Key))
                    {
                        return false;
                    }

                }

                return true;
            }
            else
            {
                return objA.Data.Equals(objB.Data);
            }
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
                this.Data = null;
                return;
            }

            this.Type = other.Type;

            if (other.Type == VMType.Struct)
            {
                var children = new Dictionary<string, VMObject>();
                var otherChildren = other.GetChildren();
                foreach (var key in otherChildren.Keys)
                {
                    var temp = new VMObject();
                    temp.Copy(otherChildren[key]);
                    children[key] = temp;
                }

                this.Data = children;
            }
            else
            {
                this.Data = other.Data;
                /*var temp = other.Data;
                _data = new byte[temp.Length];
                Array.Copy(temp, _data, _data.Length);*/
            }
        }

        public override string ToString()
        {
            switch (this.Type)
            {
                case VMType.None:return "Null";
                case VMType.Struct: return "[Struct]";
                case VMType.Bytes: return $"[Bytes] => {((byte[])Data).ByteToHex()}";
                case VMType.Number: return $"[Number] => {((BigInteger)Data)}";
                case VMType.String: return $"[String] => {((string)Data)}";
                case VMType.Bool: return $"[Bool] => {((bool)Data)}";
                case VMType.Address: return $"[Address] => {((byte[])Data).PublicKeyToAddress()}";
                case VMType.Object: return $"[Object] => {Data.GetType().Name}";
                default: return "Unknown";
            }
        }
    }

}
