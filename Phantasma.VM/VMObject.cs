using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.Core.Types;
using Phantasma.Storage.Utils;
using Phantasma.Storage;
using System.Linq;
using System.Reflection;

namespace Phantasma.VM
{
    public enum VMType
    {
        None,
        Struct,
        Bytes,
        Number,
        String,
        Timestamp,
        Bool,
        Enum,
        Object
    }

    public sealed class VMObject : ISerializable
    {
        public VMType Type { get; private set; }
        public bool IsEmpty => Data == null;

        public object Data { get; private set; }

        private int _localSize = 0;

        private static readonly string TimeFormat = "MM/dd/yyyy HH:mm:ss";

        public Dictionary<VMObject, VMObject> GetChildren() => this.Type == VMType.Struct ? (Dictionary<VMObject, VMObject>)Data : null;

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
            this.Data = null;
        }

        public BigInteger AsNumber()
        {
            if ((this.Type == VMType.Object || this.Type == VMType.Timestamp) && (Data is Timestamp))
            {
                return ((Timestamp)Data).Value;
            }

            switch (this.Type)
            {
                case VMType.String:
                    {
                        if (BigInteger.TryParse((string)Data, out BigInteger number))
                        {
                            return number;
                        }
                        else
                        {
                            throw new Exception($"Cannot convert String '{(string)Data}' to BigInteger.");
                        }
                    }

                case VMType.Bytes:
                    {
                        var bytes = (byte[])Data;
                        var num = BigInteger.FromSignedArray(bytes);
                        return num;
                    }

                case VMType.Enum:
                    {
                        var num = Convert.ToUInt32(Data);
                        return num;
                    }

                case VMType.Bool:
                    {
                        var val = (bool)Data;
                        return val ? 1 : 0;
                    }

                default:
                    {
                        if (this.Type != VMType.Number)
                        {
                            throw new Exception($"Invalid cast: expected number, got {this.Type}");
                        }

                        return (BigInteger)Data;
                    }
            }
        }

        public Timestamp AsTimestamp()
        {
            if (this.Type != VMType.Timestamp)
            {
                throw new Exception($"Invalid cast: expected timestamp, got {this.Type}");
            }

            return (Timestamp)Data;
        }

        public object AsType(VMType type)
        {
            switch (type)
            {
                case VMType.Bool: return AsBool();
                case VMType.String: return AsString();
                case VMType.Bytes: return AsByteArray();
                case VMType.Number: return AsNumber();
                case VMType.Timestamp: return AsTimestamp();
                default: throw new ArgumentException("Unsupported VM cast");
            }
        }

        public T AsEnum<T>() where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
            {
                throw new ArgumentException("T must be an enumerated type");
            }

            if (this.Type != VMType.Enum)
            {
                var num = this.AsNumber();
                Data = (int)((BigInteger)Data);
            }

            return (T)Enum.Parse(typeof(T), Data.ToString());
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
                    return Encoding.UTF8.GetString((byte[])Data);

                case VMType.Enum:
                    return ((uint)Data).ToString();

                case VMType.Object:
                    {
                        if (Data is Address)
                        {
                            return ((Address)Data).Text;
                        }

                        if (Data is Hash)
                        {
                            return ((Hash)Data).ToString();
                        }

                        return "Interop:" + Data.GetType().Name;
                    }

                case VMType.Bool:
                    return ((bool)Data) ? "true" : "false";

                case VMType.Timestamp:
                    var date = (Timestamp)Data;
                    return date.Value.ToString();

                default:
                    throw new Exception($"Invalid cast: expected string, got {this.Type}");
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

                case VMType.Bool:
                    {
                        return new byte[] { (byte)(((bool)Data) ? 1 : 0) };
                    }

                case VMType.String:
                    {
                        var str = AsString();
                        return Encoding.UTF8.GetBytes(str);
                    }

                case VMType.Number:
                    {
                        var num = AsNumber();
                        return num.ToSignedByteArray();
                    }

                case VMType.Enum:
                    {
                        var num = (uint)AsNumber();
                        var bytes = BitConverter.GetBytes(num);
                        return bytes;
                    }

                case VMType.Timestamp:
                    {
                        var time = AsTimestamp();
                        var bytes = BitConverter.GetBytes(time.Value);
                        return bytes;
                    }

                case VMType.Struct:
                    {
                        var bytes = this.Serialize();
                        return bytes;
                    }

                case VMType.Object:
                    {
                        var serializable = Data as ISerializable;
                        if (serializable != null)
                        {
                            var bytes = serializable.Serialize().Skip(1).ToArray();
                            return bytes;
                        }

                        throw new Exception("Complex object type can't be");
                    }

                default:
                    {
                        throw new Exception($"Invalid cast: expected bytes, got {this.Type}");
                    }
            }
        }

        public Address AsAddress()
        {
            switch (this.Type)
            {
                case VMType.String:
                    {
                        var temp = (string)Data;
                        if (Address.IsValidAddress(temp))
                        {
                            return Address.FromText(temp);
                        }
                        break;
                    }

                case VMType.Bytes:
                    {
                        var temp = (byte[])Data;

                        if (temp.Length == Address.LengthInBytes + 1)
                        {
                            temp = temp.Skip(1).ToArray(); // TODO there might be better way to do this
                        }

                        if (temp.Length != Address.LengthInBytes)
                        {
                            throw new Exception($"Invalid address size, expected {Address.LengthInBytes} got {temp.Length}");
                        }

                        return Address.FromBytes(temp);
                    }

                case VMType.Object:
                    {
                        if (Data is Address)
                        {
                            return (Address)Data;
                        }

                        break;
                    }
            }

            throw new Exception($"Invalid cast: expected address, got {this.Type}");
        }

        public bool AsBool()
        {
            if (this.Type == VMType.Bytes)
            {
                var bytes = (byte[])Data;
                if (bytes.Length == 1)
                {
                    return bytes[0] != 0;
                }
            }

            switch (this.Type)
            {
                case VMType.Bool: return (bool)Data;

                case VMType.Number:
                    {
                        var val = this.AsNumber();
                        return val != 0;
                    }

                /*case VMType.String:
                    {
                        return !(((string)this.Data).Equals("false", StringComparison.OrdinalIgnoreCase));
                    }*/

                default:
                    throw new Exception($"Invalid cast: expected bool, got {this.Type}");
            }
        }

        public T AsStruct<T>()
        {
            Throw.If(this.Type != VMType.Struct, $"Invalid cast: expected struct, got {this.Type}");

            if (this.Data == null)
            {
                return default(T);
            }

            var values = this.Data as Dictionary<VMObject, VMObject>;

            Throw.If(values == null, "invalid struct data");

            var result = Activator.CreateInstance<T>();

            var structType = typeof(T);
            TypedReference reference = __makeref(result);

            // WARNING this code is still experimental, probably wont work in every situation
            // TODO check that values.Count equals the number of fields in type T
            foreach (var entry in values)
            {
                var fieldName = entry.Key.AsString();
                FieldInfo fi = structType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);

                Throw.If(fi == null, "unknown field: " + fieldName);

                var fieldValue = entry.Value.ToObject();
                fi.SetValueDirect(reference, fieldValue);
            }

            return result;
        }

        public T AsInterop<T>()
        {
            if (typeof(T) == typeof(Hash) && this.Type != VMType.Object)
            {
                var bytes = this.AsByteArray();

                if (bytes.Length == 33 && bytes[0] == 32)
                {
                    bytes = bytes.Skip(1).ToArray();
                }

                if (bytes.Length == 32)
                {
                    return (T)(object)(new Hash(bytes));
                }
            }

            Throw.If(this.Type != VMType.Object, $"Invalid cast: expected object, got {this.Type}");

            if (this.Data == null)
            {
                return default(T);
            }

            Throw.IfNot(this.Data is T, "invalid interop type");

            return (T)Data;
        }

        public VMObject SetValue(byte[] val, VMType type)
        {
            this.Type = type;
            this._localSize = val != null ? val.Length : 0;

            switch (type)
            {
                case VMType.Bytes:
                    {
                        this.Data = val;
                        break;
                    }

                case VMType.Number:
                    {
                        this.Data = (val == null || val.Length == 0) ? new BigInteger(0) : BigInteger.FromSignedArray(val);
                        break;
                    }

                case VMType.String:
                    {
                        this.Data = Encoding.UTF8.GetString(val);
                        break;
                    }

                case VMType.Enum:
                    {
                        // TODO this will fail if val is not exactly 4 bytes long. Add code here to autopad with zeros if necessary
                        this.Data = BitConverter.ToUInt32(val, 0);
                        break;
                    }

                case VMType.Timestamp:
                    {
                        var temp = BitConverter.ToUInt32(val, 0);
                        this.Data = new Timestamp(temp);
                        break;
                    }

                case VMType.Bool:
                    {
                        this.Data = BitConverter.ToBoolean(val, 0);
                        break;
                    }

                default:
                    if (val is byte[])
                    {
                        var bytes = (byte[])val;

                        var len = bytes != null ? bytes.Length : 0;

                        switch (len)
                        {
                            case Address.LengthInBytes:
                                this.Data = Address.FromBytes(bytes);
                                break;

                            case Hash.Length:
                                this.Data = Hash.FromBytes(bytes);
                                break;

                            default:
                                try
                                {
                                    this.UnserializeData(bytes);
                                }
                                catch (Exception e)
                                {
                                    throw new Exception("Cannot decode interop object from bytes with length: " + len);
                                }
                                break;
                    }

                        break;
                    }
                    else
                    {
                        throw new Exception("Cannot set value for vmtype: " + type);
                    }
            }

            return this;
        }

        public VMObject SetDefaultValue(VMType type)
        {
            this.Type = type;
            this._localSize = 1; // TODO fixme

            switch (type)
            {
                case VMType.Bytes:
                    {
                        this.Data = new byte[0];
                        break;
                    }

                case VMType.Number:
                    {
                        this.Data = new BigInteger(0);
                        break;
                    }

                case VMType.String:
                    {
                        this.Data = "";
                        break;
                    }

                case VMType.Enum:
                    {
                        this.Data = (uint)0;
                        break;
                    }

                case VMType.Timestamp:
                    {
                        this.Data = new Timestamp(0);
                        break;
                    }

                case VMType.Bool:
                    {
                        this.Data = false;
                        break;
                    }

                case VMType.Object:
                    {
                        this.Data = null;
                        break;
                    }

                default:
                    {
                        throw new Exception("Cannot init default value for vmtype: " + type);
                    }
            }

            return this;
        }

        public VMObject SetValue(BigInteger val)
        {
            this.Type = VMType.Number;
            this.Data = val;
            this._localSize = val.ToSignedByteArray().Length;
            return this;
        }

        public VMObject SetValue(Dictionary<VMObject, VMObject> children)
        {
            this.Type = VMType.Struct;
            this.Data = children;
            this._localSize = 4; // TODO not valid
            return this;
        }

        public VMObject SetValue(Hash hash)
        {
            this.Type = VMType.Object;
            this.Data = hash;
            this._localSize = 4;
            return this;
        }

        public VMObject SetValue(Address address)
        {
            this.Type = VMType.Object;
            this.Data = address;
            this._localSize = 4;
            return this;
        }

        public VMObject SetValue(object val)
        {
            var type = val.GetType();
            Throw.If(!type.IsStructOrClass(), $"Invalid cast: expected struct or class, got {type.Name}");
            this.Type = VMType.Object;
            this.Data = val;
            this._localSize = 4;
            return this;
        }

        public VMObject SetValue(DateTime val)
        {
            return SetValue((Timestamp)val);
        }

        public VMObject SetValue(Timestamp val)
        {
            this.Type = VMType.Timestamp;
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

        public VMObject SetValue(byte[] val)
        {
            this.Type = VMType.Bytes;
            this.Data = val;
            this._localSize = val.Length;
            return this;
        }

        public VMObject SetValue(Enum val)
        {
            this.Type = VMType.Enum;
            this.Data = val;
            this._localSize = 4;
            return this;
        }

        public void SetKey(VMObject key, VMObject obj)
        {
            Dictionary<VMObject, VMObject> children;

            // NOTE: here we need to instantiate the key as new object
            // otherwise keeping the key in a register allows modifications to it that also affect the dictionary keys
            var temp = new VMObject();
            temp.Copy(key);
            key = temp;

            if (this.Type == VMType.Struct)
            {
                children = GetChildren();
            }
            else
            if (this.Type == VMType.None)
            {
                this.Type = VMType.Struct;
                children = new Dictionary<VMObject, VMObject>();
                this.Data = children;
                this._localSize = 0;
            }
            else
            {
                throw new Exception($"Invalid cast from {this.Type} to struct");
            }

            var result = new VMObject();
            children[key] = result;
            result.Copy(obj);
        }

        public VMObject GetKey(VMObject key)
        {
            if (this.Type != VMType.Struct)
            {
                throw new Exception($"Invalid cast: expected struct, got {this.Type}");
            }

            var children = GetChildren();

            if (children.ContainsKey(key))
            {
                return children[key];
            }

            return new VMObject();
        }

        public override int GetHashCode()
        {
            switch (this.Type)
            {
                case VMType.Struct:
                    {
                        unchecked // Overflow is fine, just wrap
                        {
                            var hash = (int)2166136261;
                            var children = this.GetChildren();
                            foreach (var child in children)
                            {
                                hash = hash * 16777619 + child.GetHashCode();
                            }
                            return hash;
                        }
                    }

                default: return Data.GetHashCode(); // TODO is this ok for all cases?

            }
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
                var children = new Dictionary<VMObject, VMObject>();
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
                case VMType.None: return "Null";
                case VMType.Struct: return "[Struct]";
                case VMType.Bytes: return $"[Bytes] => {Base16.Encode(((byte[])Data))}";
                case VMType.Number: return $"[Number] => {((BigInteger)Data)}";
                case VMType.Timestamp: return $"[Time] => {((DateTime)((Timestamp)Data)).ToString(TimeFormat)}";
                case VMType.String: return $"[String] => {((string)Data)}";
                case VMType.Bool: return $"[Bool] => {((bool)Data)}";
                case VMType.Enum: return $"[Enum] => {((uint)Data)}";
                case VMType.Object: return $"[Object] => {(Data == null? "null" : Data.GetType().Name)}";
                default: return "Unknown";
            }
        }

        public static VMObject CastTo(VMObject srcObj, VMType type)
        {
            if (srcObj.Type == type)
            {
                var result = new VMObject();
                result.Copy(srcObj);
                return result;
            }

            switch (type) {
                case VMType.None:
                    return new VMObject();

                case VMType.String:
                    {
                        var result = new VMObject();
                        result.SetValue(srcObj.AsString()); // TODO does this work for all types?
                        return result;
                    }

                case VMType.Timestamp:
                    {
                        var result = new VMObject();
                        result.SetValue(srcObj.AsTimestamp()); // TODO does this work for all types?
                        return result;
                    }

                case VMType.Bool:
                    {
                        var result = new VMObject();
                        result.SetValue(srcObj.AsBool()); // TODO does this work for all types?
                        return result;
                    }

                case VMType.Bytes:
                    {
                        var result = new VMObject();
                        result.SetValue(srcObj.AsByteArray()); // TODO does this work for all types?
                        return result;
                    }

                case VMType.Number:
                    {
                        var result = new VMObject();
                        result.SetValue(srcObj.AsNumber()); // TODO does this work for all types?
                        return result;
                    }

                case VMType.Struct:
                    switch (srcObj.Type)
                    {
                        case VMType.Object: return CastViaReflection(srcObj.Data, 0);

                        default: throw new Exception($"invalid cast: {srcObj.Type} to {type}");
                    }

                default: throw new Exception($"invalid cast: {srcObj.Type} to {type}");
            }
        }

        public static VMType GetVMType(Type type)
        {
            if (type.IsEnum)
            {
                return VMType.Enum;
            }

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

            if (type == typeof(BigInteger) || type == typeof(int))
            {
                return VMType.Number;
            }

            if (type == typeof(Timestamp) || type == typeof(uint))
            {
                return VMType.Timestamp;
            }

            if (type.IsEnum)
            {
                return VMType.Enum;
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
            var objType = obj.GetType();
            var type = GetVMType(objType);
            Throw.If(type == VMType.None, "not a valid object");

            var result = new VMObject();

            switch (type)
            {
                case VMType.Bool: result.SetValue((bool)obj); break;
                case VMType.Bytes: result.SetValue((byte[])obj, VMType.Bytes); break;
                case VMType.String: result.SetValue((string)obj); break;
                case VMType.Number:
                    if (obj.GetType() == typeof(int))
                    {
                        obj = new BigInteger((int)obj); // HACK
                    }
                    result.SetValue((BigInteger)obj);
                    break;

                case VMType.Enum: result.SetValue((Enum)obj); break;
                case VMType.Object: result.SetValue(obj); break;
                case VMType.Timestamp:
                    if (obj.GetType() == typeof(uint))
                    {
                        obj = new Timestamp((uint)obj); // HACK
                    }
                    result.SetValue((Timestamp)obj);
                    break;
                default: return null;
            }

            return result;
        }

        public object ToObject()
        {
            Throw.If(Type == VMType.None, "not a valid object");

            switch (Type)
            {
                case VMType.Bool: return this.AsBool();
                case VMType.Bytes: return this.AsByteArray();
                case VMType.String: return this.AsString();
                case VMType.Number: return this.AsNumber();
                case VMType.Timestamp: return this.AsTimestamp();
                case VMType.Object: return this.Data;
                case VMType.Enum: return this.Data;
                default: return null;
            }
        }

        public object ToObject(Type type)
        {
            if (this.Type == VMType.Struct)
            {
                if (type.IsArray)
                {
                    var elementType = type.GetElementType();
                    return this.ToArray(elementType);
                }
                else
                if (type.IsStructOrClass())
                {
                    return this.ToStruct(type);
                }
                else
                {
                    throw new NotImplementedException(); // some stuff still missing: eg: lists, dictionaries..
                }
            }
            else
            {
                return this.ToObject();
            }
        }

        public T[] ToArray<T>()
        {
            return (T[])ToArray(typeof(T));
        }

        public object ToArray(Type arrayElementType)
        {
            Throw.If(Type != VMType.Struct, "not a valid source struct");

            var children = GetChildren();
            int maxIndex = -1;
            foreach (var child in children)
            {
                Throw.If(child.Key.Type != VMType.Number, "source contains an element with invalid array index");

                var temp = child.Key.AsNumber();
                // TODO use a constant for VM max array size
                Throw.If(temp >= 1024, "source contains an element with a very large array index");

                var index = (int)temp;
                Throw.If(index < 0, "source contains an array index with negative value");

                maxIndex = Math.Max(index, maxIndex);
            }

            var length = maxIndex + 1;
            var array = Array.CreateInstance(arrayElementType, length);

            foreach (var child in children)
            {
                var temp = child.Key.AsNumber();
                var index = (int)temp;

                var val = child.Value.ToObject(arrayElementType);
                array.SetValue(val, index);
            }

            return array;
        }

        public T ToStruct<T>()
        {
            return (T)ToStruct(typeof(T));
        }

        public object ToStruct(Type structType)
        {
            Throw.If(Type != VMType.Struct, "not a valid source struct");

            Throw.If(!structType.IsStructOrClass(), "not a valid destination struct");

            var dict = this.GetChildren();

            var fields = structType.GetFields();
            var result = Activator.CreateInstance(structType);

            object boxed = result;
            foreach (var field in fields)
            {
                var key = VMObject.FromObject(field.Name);
                Throw.If(!dict.ContainsKey(key), "field not present in source struct: " + field.Name);
                var val = dict[key].ToObject(field.FieldType);

                // here we check if the types mismatch
                // in case of getting a byte[] instead of an object, we try unserializing the bytes in a different approach
                // NOTE this should not be necessary often, but is already getting into black magic territory...
                if (val != null && field.FieldType != typeof(byte[]) && val.GetType() == typeof(byte[]))
                {
                    if (typeof(ISerializable).IsAssignableFrom(field.FieldType))
                    {
                        var temp = (ISerializable)Activator.CreateInstance(field.FieldType);
                        var bytes = (byte[])val;
                        using (var stream = new MemoryStream(bytes))
                        {
                            using (var reader = new BinaryReader(stream))
                            {
                                temp.UnserializeData(reader);
                            }
                        }
                        val = temp;
                    }
                }

                // HACK allows treating uints as enums, without this it is impossible to transform between C# objects and VM objects
                if (field.FieldType.IsEnum && !val.GetType().IsEnum)
                {
                    val = Enum.Parse(field.FieldType, val.ToString());
                }

                field.SetValue(boxed, val);
            }
            return boxed;
        }

        // this does the opposite of ToStruct(), takes a InteropObject and converts it to a VM.Struct
        private static VMObject CastViaReflection(object srcObj, int level)
        {
            var srcType = srcObj.GetType();

            if (srcType.IsArray)
            {
                var children = new Dictionary<VMObject, VMObject>();

                var array = (Array)srcObj;
                for (int i = 0; i < array.Length; i++)
                {
                    var val = array.GetValue(i);
                    var key = new VMObject();
                    key.SetValue(i);
                    var vmVal = CastViaReflection(val, level + 1);
                    children[key] = vmVal;
                }

                var result = new VMObject();
                result.SetValue(children);
                return result;
            }
            else
            {
                var targetType = VMObject.GetVMType(srcType);

                VMObject result;

                bool isKnownType = typeof(BigInteger) == srcType || typeof(Timestamp) == srcType || typeof(ISerializable).IsAssignableFrom(srcType);

                if (srcType.IsStructOrClass() && !isKnownType)
                {
                    var children = new Dictionary<VMObject, VMObject>();

                    var fields = srcType.GetFields();

                    if (fields.Length > 0)
                    {
                        foreach (var field in fields)
                        {
                            var key = new VMObject();
                            key.SetValue(field.Name);
                            var val = field.GetValue(srcObj);
                            var vmVal = CastViaReflection(val, level + 1);
                            children[key] = vmVal;
                        }

                        result = new VMObject();
                        result.SetValue(children);
                        return result;
                    }
                }

                result = VMObject.FromObject(srcObj);
                if (result != null)
                {
                    return result;
                }

                throw new Exception($"invalid cast: Interop.{srcType.Name} to vm object");
            }
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.Write((byte)this.Type);
            if (this.Type == VMType.None)
            {
                return;
            }

            var dataType = this.Data.GetType();

            if (this.Type == VMType.Struct)
            {
                var children = this.GetChildren();
                writer.WriteVarInt(children.Count);
                foreach (var entry in children)
                {
                    entry.Key.SerializeData(writer);
                    entry.Value.SerializeData(writer);
                }
            }
            else
            if (this.Type == VMType.Object)
            {
                var obj = this.Data as ISerializable;

                if (obj != null)
                {
                    var bytes = Serialization.Serialize(obj);
                    writer.WriteByteArray(bytes);
                }
                else
                {
                    throw new Exception($"Objects of type {dataType.Name} cannot be serialized");
                }
            }
            else
            {
                Serialization.Serialize(writer, this.Data);
            }
        }

        public static VMObject FromBytes(byte[] bytes)
        {
            var result = new VMObject();
            result.UnserializeData(bytes);
            return result;
        }

        public void UnserializeData(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    UnserializeData(reader);
                }
            }
        }

        public void UnserializeData(BinaryReader reader)
        {
            this.Type = (VMType)reader.ReadByte();
            switch (this.Type)
            {
                case VMType.Bool:
                    this.Data = Serialization.Unserialize<bool>(reader);
                    break;

                case VMType.Bytes:
                    this.Data = Serialization.Unserialize<byte[]>(reader);
                    break;

                case VMType.Number:
                    this.Data = Serialization.Unserialize<BigInteger>(reader);
                    break;

                case VMType.Timestamp:
                    this.Data = Serialization.Unserialize<Timestamp>(reader);
                    break;

                case VMType.String:
                    this.Data = Serialization.Unserialize<string>(reader);
                    break;

                case VMType.Struct:
                    var childCount = reader.ReadVarInt();
                    var children = new Dictionary<VMObject, VMObject>();
                    while (childCount > 0)
                    {
                        var key = new VMObject();
                        key.UnserializeData(reader);

                        var val = new VMObject();
                        val.UnserializeData(reader);

                        children[key] = val;
                        childCount--;
                    }

                    this.Data = children;
                    break;

                case VMType.Object:
                    var bytes  = reader.ReadByteArray();

                    if (bytes.Length == 35)
                    {
                        var addr = Serialization.Unserialize<Address>(bytes);
                        this.Data = addr;
                        this.Type = VMType.Object;
                    }
                    else
                    {
                        // NOTE object type information is lost during serialization, so we reconstruct it as byte array
                        this.Type = VMType.Bytes;
                        this.Data = bytes;
                    }

                    break;

                case VMType.Enum:
                    this.Type = VMType.Enum;
                    this.Data = (uint)reader.ReadVarInt();
                    break;

                case VMType.None:
                    this.Type = VMType.None;
                    this.Data = null;
                    break;

                default:
                    throw new Exception($"invalid unserialize: type {this.Type}");
            }
        }
    }

}
