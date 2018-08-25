using System;
using System.Collections.Generic;
using System.Text;
using Phantasma.Cryptography;
using Phantasma.Mathematics;
using Phantasma.Utils;
using Phantasma.VM.Types;

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
            switch (this.Type)
            {
                case VMType.String:
                        return (string)Data;

                case VMType.Number:
                    return ((BigInteger)Data).ToString();

                case VMType.Bytes:
                    return Base16.Encode((byte[])Data);

                case VMType.Object:
                    return "Interop:" + Data.GetType().Name;

                case VMType.Bool:
                    return ((bool)Data) ? "true" : "false";

                default:
                    throw new Exception("Invalid cast");
            }
        }

        public byte[] AsByteArray()
        {
            switch (this.Type)
            {
                case VMType.Bytes:
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

        public Address AsAddress()
        {
            switch (this.Type)
            {
                case VMType.Bytes:
                    {
                        var temp = (byte[])Data;

                        if (temp.Length != Address.PublicKeyLength)
                        {
                            throw new Exception("Invalid address size");
                        }

                        return new Address(temp);
                    }

                case VMType.Object:
                    {
                        if (Data is Address)
                        {
                            return (Address)Data;
                        }

                        throw new Exception("Invalid cast");
                    }

                default:
                    throw new Exception("Invalid cast");
            }
        }

        public bool AsBool()
        {
            if (this.Type != VMType.Bool)
            {
                throw new Exception("Invalid cast");
            }

            return (bool)Data;
        }

        public T AsInterop<T>() 
        {
            Throw.If(this.Type != VMType.Object, "Invalid cast");
            Throw.IfNot(this.Data is T, "invalid interop type");

            return (T)Data;
        }

        public VMObject SetValue(byte[] val, VMType type)
        {
            this.Type = type;
            this._localSize = val.Length;

            switch (type)
            {
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

        public VMObject SetValue(object val)
        {
            var type = val.GetType();
            Throw.If(!type.IsStructOrClass(), "invalid cast");
            this.Type = VMType.Object;
            this.Data = val;
            this._localSize = 4;
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

        public override int GetHashCode()
        {
            return Data.GetHashCode(); // TODO Fix me with proper hashing if byte array
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (!(obj is VMObject))
            {
                return false;
            }

            var other = (VMObject)obj;

            if (this.Type != other.Type)
            {
                return false;
            }

            if (this.Type == VMType.Struct)
            {
                var children = this.GetChildren();
                var otherChildren = other.GetChildren();

                foreach (var entry in children)
                {
                    if (!otherChildren.ContainsKey(entry.Key))
                    {
                        return false;
                    }

                    var A = children[entry.Key];
                    var B = otherChildren[entry.Key];

                    if (A != B)
                    {
                        return false;
                    }
                }

                foreach (var entry in otherChildren)
                {
                    if (!children.ContainsKey(entry.Key))
                    {
                        return false;
                    }

                }

                return true;
            }
            else
            {
                return this.Data.Equals(other.Data);
            }
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
                case VMType.Bytes: return $"[Bytes] => {Base16.Encode(((byte[])Data))}";
                case VMType.Number: return $"[Number] => {((BigInteger)Data)}";
                case VMType.String: return $"[String] => {((string)Data)}";
                case VMType.Bool: return $"[Bool] => {((bool)Data)}";
                case VMType.Object: return $"[Object] => {Data.GetType().Name}";
                default: return "Unknown";
            }
        }

        public static VMType GetVMType(Type type)
        {
            if (type == typeof(bool))
            {
                return VMType.Bool;
            }

            if (type == typeof(string))
            {
                return VMType.String;
            }

            if (type == typeof(byte[]))
            {
                return VMType.Bytes;
            }

            if (type == typeof(BigInteger))
            {
                return VMType.Number;
            }

            if (type.IsEnum)
            {
                return VMType.Number; // TODO make new optimized type for enums
            }

            if (type.IsClass || type.IsValueType) 
            {
                return VMType.Object;
            }

            return VMType.None;
        }

        public static bool IsVMType(Type type)
        {
            var result = GetVMType(type);
            return result != VMType.None;
        }

        public static VMObject FromObject(object obj)
        {
            var type = GetVMType(obj.GetType());
            Throw.If(type == VMType.None, "not a valid object");

            var result = new VMObject();

            switch (type)
            {
                case VMType.Bool: result.SetValue((bool)obj); break;
                case VMType.Bytes: result.SetValue((byte[])obj, VMType.Bytes); break;
                case VMType.String: result.SetValue((string)obj);break;
                case VMType.Number: result.SetValue((BigInteger)obj); break;
                case VMType.Object: result.SetValue(obj); break;
                default: return null;
            }

            return result;
        }

    }

}
