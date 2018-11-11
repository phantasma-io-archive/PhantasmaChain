using System;
using System.Collections.Generic;
using System.IO;

namespace Phantasma.IO.Database
{
    internal struct ClusterEntry
    {
        public byte[] Key;
        public uint Offset;
        public uint Length;
    }

    internal class Cluster: IDisposable
    {
        public readonly uint Hash;
        public readonly string Path;

        private FileStream _stream;
        private BinaryReader _reader;
        private BinaryWriter _writer;

        private string _fileName;

        private uint _freeOffset;
        private uint _tableOffset;

        private Dictionary<byte[], ClusterEntry> _entries = new Dictionary<byte[], ClusterEntry>();

        private static byte[] _pad = new byte[1024];
        
        public Cluster(uint hash, string path)
        {
            this.Hash = hash;
            this.Path = path;

            _fileName = path + hash + DB.Extension;

            _stream = new FileStream(_fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            _reader = new BinaryReader(_stream);
            _writer = new BinaryWriter(_stream);

            if (_stream.Length <= 0)
            {
                _freeOffset = 0;
                for (int i=1; i<=100; i++)
                {
                    _writer.Write(_pad);
                }
                _tableOffset = (uint)_stream.Position;
            }
            else
            {
                ReadTable();
            }
        }

        private void ReadTable()
        {
            _entries.Clear();

            _stream.Seek(-sizeof(uint), SeekOrigin.End);
            _tableOffset = _reader.ReadUInt32();
            _stream.Seek(_tableOffset, SeekOrigin.Begin);
            var count = _reader.ReadUInt32();

            while (count > 0)
            {
                var keyLength = _reader.ReadUInt16();
                var entry = new ClusterEntry() { Key = _reader.ReadBytes(keyLength), Offset = _reader.ReadUInt32(), Length = _reader.ReadUInt32() };
                _entries[entry.Key] = entry;

                count--;
            }
        }

        public bool Contains(byte[] key)
        {
            return _entries.ContainsKey(key);
        }

        public byte[] Get(byte[] key)
        {
            if (_entries.ContainsKey(key))
            {
                var entry = _entries[key];
                _stream.Seek(entry.Offset, SeekOrigin.Begin);
                var bytes = _reader.ReadBytes((int)entry.Length);
                return bytes;
            }

            return null;
        }

        public void Set(byte[] key, byte[] value)
        {
            ClusterEntry entry;

            if (_entries.ContainsKey(key))
            {
                entry = _entries[key];
                if (entry.Length <= value.Length)
                {
                    _stream.Seek(entry.Offset, SeekOrigin.Begin);
                    _writer.Write(value);
                    return;
                }
            }

            entry = new ClusterEntry() { Key = key, Length = (uint)value.Length, Offset = _freeOffset };
            _stream.Seek(entry.Offset, SeekOrigin.Begin);
            _writer.Write(value);

            _freeOffset += entry.Length;
        }

        public void Remove(byte[] key)
        {
            _entries.Remove(key);
        }

        public void Flush()
        {
            _stream.Seek(_tableOffset, SeekOrigin.Begin);
            var offset = (uint)_stream.Position;
            _writer.Write((int)_entries.Count);
            foreach (var entry in _entries.Values)
            {
                ushort keyLength = (ushort) entry.Key.Length;

                _writer.Write(keyLength);
                _writer.Write(entry.Key);
                _writer.Write(entry.Offset);
                _writer.Write(entry.Length);
            }
            _writer.Write(offset);
        }

        public void Dispose()
        {
            _stream.Dispose();
            _stream = null;
        }
    }
}
