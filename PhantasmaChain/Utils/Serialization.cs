using Phantasma.Cryptography;
using Phantasma.Mathematics;
using Phantasma.VM.Types;
using System;
using System.IO;

namespace Phantasma.Utils
{
    public static class Serialization
    {
        public static byte[] Serialize(this object obj)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    Serialize(obj, writer);
                }

                return stream.ToArray();
            }

        }

        public static void Serialize(object obj, BinaryWriter writer)
        {
            var type = obj.GetType();
            var fields = type.GetFields();

            foreach (var field in fields)
            {
                object val = field.GetValue(obj);
                var fieldType = field.FieldType;

                if (fieldType == typeof(bool))
                {
                    writer.Write((byte)(((bool)val) ? 1 : 0));
                }
                else
                if (fieldType == typeof(byte))
                {
                    writer.Write((byte)val);
                }
                else
                if (fieldType == typeof(long))
                {
                    writer.Write((long)val);
                }
                else
                if (fieldType == typeof(int))
                {
                    writer.Write((int)val);
                }
                else
                if (fieldType == typeof(short))
                {
                    writer.Write((short)val);
                }
                else
                if (fieldType == typeof(string))
                {
                    writer.Write((string)val);
                }
                else
                if (fieldType == typeof(BigInteger))
                {
                    writer.WriteBigInteger((BigInteger)val);
                }
                else
                if (fieldType == typeof(Hash))
                {
                    writer.WriteHash((Hash)val);
                }
                else
                if (fieldType == typeof(Timestamp))
                {
                    writer.Write(((Timestamp)val).Value);
                }
                else
                if (fieldType.IsArray)
                {
                    var array = (Array)val;
                    writer.WriteVarInt(array.Length);

                    for (int i = 0; i < array.Length; i++)
                    {
                        var item = array.GetValue(i);
                        Serialize(item, writer);
                    }

                    val = array;
                }
                else
                {
                    throw new Exception("Unknown type");
                }

                field.SetValue(obj, val);
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
            var fields = type.GetFields();
            var obj = Activator.CreateInstance(type);

            foreach (var field in fields)
            {
                var fieldType = field.FieldType;

                object val;

                if (fieldType == typeof(bool))
                {
                    val = reader.ReadByte()!=0;
                }
                else
                if (fieldType == typeof(byte))
                {
                    val = reader.ReadByte();
                }
                else
                if (fieldType == typeof(long))
                {
                    val = reader.ReadInt64();
                }
                else
                if (fieldType == typeof(int))
                {
                    val = reader.ReadInt32();
                }
                else
                if (fieldType == typeof(short))
                {
                    val = reader.ReadInt16();
                }
                else
                if (fieldType == typeof(string))
                {
                    val = reader.ReadString(); //TODO improve me
                }
                else
                if (fieldType == typeof(BigInteger))
                {
                    val = reader.ReadBigInteger();
                }
                else
                if (fieldType == typeof(Hash))
                {
                    val = reader.ReadHash();
                }
                else
                if (fieldType == typeof(Timestamp))
                {
                    val = new Timestamp(reader.ReadUInt32());
                }
                else
                if (fieldType.IsArray)
                {
                    var length = (int)reader.ReadVarInt();
                    var array = Array.CreateInstance(fieldType, length);
                    for (int i=0; i<length; i++)
                    {
                        var item = Unserialize(reader, type);
                        array.SetValue(item, i);
                    }

                    val = array;
                }
                else
                {
                    throw new Exception("Unknown type");
                }

                field.SetValue(obj, val);
            }

            return obj;
        }

    }
}
