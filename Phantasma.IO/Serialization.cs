using System;
using System.IO;
using System.Reflection;
using Phantasma.Core;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.IO
{
    public static class Serialization
    {
        public static byte[] Serialize(this object obj)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    Serialize(writer, obj);
                }

                return stream.ToArray();
            }

        }

        public static void Serialize(BinaryWriter writer, object obj)
        {
            var type = obj.GetType();

            if (type == typeof(void))
            {
                return;
            }

            if (type == typeof(bool))
            {
                writer.Write((byte)(((bool)obj) ? 1 : 0));
            }
            else
            if (type == typeof(byte))
            {
                writer.Write((byte)obj);
            }
            else
            if (type == typeof(long))
            {
                writer.Write((long)obj);
            }
            else
            if (type == typeof(int))
            {
                writer.Write((int)obj);
            }
            else
            if (type == typeof(ushort))
            {
                writer.Write((ushort)obj);
            }
            else
            if (type == typeof(sbyte))
            {
                writer.Write((sbyte)obj);
            }
            else
            if (type == typeof(ulong))
            {
                writer.Write((ulong)obj);
            }
            else
            if (type == typeof(uint))
            {
                writer.Write((uint)obj);
            }
            else
            if (type == typeof(ushort))
            {
                writer.Write((ushort)obj);
            }
            else
            if (type == typeof(string))
            {
                writer.Write((string)obj);
            }
            else
            if (type == typeof(BigInteger))
            {
                writer.WriteBigInteger((BigInteger)obj);
            }
            else
            if (type == typeof(Hash))
            {
                writer.WriteHash((Hash)obj);
            }
            else
            if (type == typeof(Timestamp))
            {
                writer.Write(((Timestamp)obj).Value);
            }
            else
            if (type == typeof(Address))
            {
                writer.WriteAddress((Address)obj);
            }
            else
            if (type.IsArray)
            {
                var array = (Array)obj;
                writer.WriteVarInt(array.Length);

                for (int i = 0; i < array.Length; i++)
                {
                    var item = array.GetValue(i);
                    Serialize(writer, item);
                }
            }
            else
            if (type.IsEnum)
            {
                uint val = (uint)Convert.ChangeType(obj, typeof(uint));
                writer.WriteVarInt(val);
            }
            else
            if (IsStructOrClass(type)) // check if struct or class
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    var val = field.GetValue(obj);
                    Serialize(writer, val);
                }

                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var prop in props)
                {
                    var val = prop.GetValue(obj);
                    Serialize(writer, val);
                }
            }
            else
            {
                throw new Exception("Unknown type");
            }
        }

        public static T Unserialize<T>(byte[] bytes)
        {
            return (T)Unserialize(bytes, typeof(T));
        }

        public static object Unserialize(byte[] bytes, Type type)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    return Unserialize(reader, type);
                }
            }
        }

        public static T Unserialize<T>(BinaryReader reader)
        {
            return (T)Unserialize(reader, typeof(T));
        }

        public static object Unserialize(BinaryReader reader, Type type)
        {
            if (type == typeof(bool))
            {
                return reader.ReadByte() != 0;
            }

            if (type == typeof(byte))
            {
                return reader.ReadByte();
            }

            if (type == typeof(long))
            {
                return reader.ReadInt64();
            }

            if (type == typeof(int))
            {
                return reader.ReadInt32();
            }

            if (type == typeof(short))
            {
                return reader.ReadInt16();
            }

            if (type == typeof(sbyte))
            {
                return reader.ReadSByte();
            }

            if (type == typeof(ulong))
            {
                return reader.ReadUInt64();
            }

            if (type == typeof(uint))
            {
                return reader.ReadUInt32();
            }

            if (type == typeof(ushort))
            {
                return reader.ReadUInt16();
            }

            if (type == typeof(string))
            {
                return reader.ReadString(); //TODO improve me
            }

            if (type == typeof(BigInteger))
            {
                return reader.ReadBigInteger();
            }

            if (type == typeof(Hash))
            {
                return reader.ReadHash();
            }

            if (type == typeof(Address))
            {
                return reader.ReadAddress();
            }

            if (type == typeof(Timestamp))
            {
                return new Timestamp(reader.ReadUInt32());
            }

            if (type.IsArray)
            {
                var length = (int)reader.ReadVarInt();
                var arrayType = type.GetElementType();
                var array = Array.CreateInstance(arrayType, length);
                for (int i = 0; i < length; i++)
                {
                    var item = Unserialize(reader, arrayType);
                    array.SetValue(item, i);
                }

                return array;
            }

            if (type.IsEnum)
            {
                var val = (uint)reader.ReadVarInt();
                return Enum.Parse(type, val.ToString());
            }

            if (IsStructOrClass(type)) // check if struct or class
            {
                var obj = Activator.CreateInstance(type);
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    var fieldType = field.FieldType;

                    object val = Unserialize(reader, fieldType);
                    field.SetValue(obj, val);
                }

                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var prop in props)
                {
                    var propType = prop.PropertyType;

                    if (prop.CanWrite)
                    {
                        object val = Unserialize(reader, propType);
                        prop.SetValue(obj, val);
                    }
                }

                return obj;
            }

            throw new Exception("Unknown type");
        }

        public static bool IsStructOrClass(this Type type)
        {
            return (type.IsValueType && !type.IsEnum) || type.IsClass;
        }

        // only works in structs and classes
        public static void Copy(this object target, object source)
        {
            var type = target.GetType();

            Throw.IfNot(IsStructOrClass(type), "invalid type");

            var fields = type.GetFields();

            foreach (var field in fields)
            {
                var fieldType = field.FieldType;

                object val;
                if (IsStructOrClass(fieldType))
                {
                    val = Activator.CreateInstance(fieldType);
                    val.Copy(field.GetValue(source));
                }
                else
                {
                    val = field.GetValue(source);
                }
                field.SetValue(target, val);
            }
        }


    }
}
