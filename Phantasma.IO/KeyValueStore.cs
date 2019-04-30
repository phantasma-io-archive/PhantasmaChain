using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Phantasma.IO
{
    public interface IKeyValueStore
    {
        void SetValue(byte[] key, byte[] value);
        byte[] GetValue(byte[] key);
        bool ContainsKey(byte[] key);
        bool Remove(byte[] key);
        uint Count { get; }
    }

    public enum KeyStoreDataSize
    {
        Small, // up to 255 bytes per entry
        Medium, // up to 64kb per entry
        Large, // up to 16mb per entry
        Huge, // up to 4gb per entry
    }

    internal struct MemoryStoreEntry
    {
        public readonly byte[] Value;
        public readonly long Timestamp;

        public MemoryStoreEntry(byte[] value, long timestamp)
        {
            Value = value;
            Timestamp = timestamp;
        }
    }

    public class MemoryStore : IKeyValueStore
    {
        public readonly int CacheSize;

        private Dictionary<byte[], MemoryStoreEntry> _cache = new Dictionary<byte[], MemoryStoreEntry>(new ByteArrayComparer());

        public uint Count => (uint)_cache.Count;

        public MemoryStore(int cacheSize)
        {
            Throw.If(cacheSize != -1 && cacheSize < 4, "invalid maxsize");
            this.CacheSize = cacheSize;
        }

        public void SetValue(byte[] key, byte[] value)
        {
            if (value == null || value.Length == 0)
            {
                Remove(key);
                return;
            }

            if (CacheSize != -1)
            {
                while (_cache.Count >= CacheSize)
                {
                    long minVal = 0;
                    byte[] minKey = null;

                    foreach (var entry in _cache)
                    {
                        if (minKey == null || entry.Value.Timestamp < minVal)
                        {
                            minVal = entry.Value.Timestamp;
                            minKey = entry.Key;
                        }
                    }
                    Remove(minKey);
                }
            }

            _cache[key] = new MemoryStoreEntry(value, DateTime.UtcNow.Ticks);
        }

        public byte[] GetValue(byte[] key)
        {
            if (ContainsKey(key))
            {
                var val = _cache[key].Value;
                _cache[key] = new MemoryStoreEntry(val, DateTime.UtcNow.Ticks);
                return val;
            }

            return null;
        }

        public bool ContainsKey(byte[] key)
        {
            var result = _cache.ContainsKey(key);

            if (result)
            {
                _cache[key] = new MemoryStoreEntry(_cache[key].Value, DateTime.UtcNow.Ticks);
            }

            return result;
        }

        public bool Remove(byte[] key)
        {
            if (ContainsKey(key))
            {
                _cache.Remove(key);
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public struct DiskEntry
    {
        public uint Offset;
        public uint Length;
    }

    public class DiskStore : IKeyValueStore, IDisposable
    {
        private const bool BackgroundWriting = true;

        public readonly KeyStoreDataSize DataSize;

        public readonly string fileName;
        public uint Count => (uint)_entries.Count;

        private object _lock = new object();

        private Dictionary<byte[], DiskEntry> _entries = new Dictionary<byte[], DiskEntry>(new ByteArrayComparer());

        private FileStream _stream;
        private uint _lastBlockOffset;

        private List<DiskEntry> _freeSpace = new List<DiskEntry>();
        private HashSet<byte[]> _pendingEntries = new HashSet<byte[]>(new ByteArrayComparer());

        private static readonly byte[] _header = Encoding.ASCII.GetBytes("BLOK");

        private bool _ready;

        private bool _pendingWrite;
        private DateTime _pendingTime;

        private static Thread _diskThread = null;
        private static object _globalLock = new object();
        private static Dictionary<string, DiskStore> _storeMap = new Dictionary<string, DiskStore>();
        private static HashSet<string> _pendingFiles = new HashSet<string>();

        public DiskStore(string fileName, KeyStoreDataSize dataSize)
        {
            Throw.If(string.IsNullOrEmpty(fileName), "invalid filename");

            this.DataSize = dataSize;

            //var path = Path.GetDirectoryName(fileName);
            var path = "Storage";
            fileName = path + "/"+ Path.GetFileName(fileName);
            this.fileName = fileName;

            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            _lastBlockOffset = 0;
            _stream = null;

            if (File.Exists(fileName))
            {
                ReadBlocks();
            }

            if (BackgroundWriting)
            {
                lock (_globalLock)
                {
                    if (_diskThread == null)
                    {
                        _diskThread = new Thread(BackgroundTask);

                        _diskThread.Start();
                    }

                    _storeMap[fileName] = this;
                }
            }

            _ready = true;
        }

        public static uint GetMaxAlllowedItemSize(KeyStoreDataSize dataSize)
        {
            switch (dataSize)
            {
                case KeyStoreDataSize.Small: return 0xFF;
                case KeyStoreDataSize.Medium: return 0xFFFF;
                case KeyStoreDataSize.Large: return 0xFFFFFF;
                case KeyStoreDataSize.Huge: return 0xFFFFFF;

                default: throw new Exception("invalid data size");
            }
        }

        public static void FlushAll()
        {
            lock (_globalLock)
            {
                FlushPendingStores();
            }
        }

        private static bool FlushPendingStores()
        {
            lock (_globalLock)
            {
                if (_storeMap.Count == 0)
                {
                    return false;
                }

                foreach (var fileName in _pendingFiles)
                {
                    var store = _storeMap[fileName];
                    lock (store._lock)
                    {
                        store.UpdatePendingWrites(true);
                    }
                }

                _pendingFiles.Clear();
            }

            return true;
        }

        // TODO use Wait events instead of Sleep
        private static void BackgroundTask()
        {
            Thread.CurrentThread.IsBackground = true;

            while (FlushPendingStores())
            {
                Thread.Sleep(500);
            }

            lock (_globalLock)
            {
                _diskThread = null;
            }
        }

        // NOTE - always call this from a lock{} block
        private void UpdatePendingWrites(bool force)
        {
            if (_pendingWrite)
            {
                var diff = DateTime.UtcNow - _pendingTime;
                if (diff.TotalSeconds >= 1 || force)
                {
                    WriteBlocks();
                }
            }
        }

        private void RequestUpdate(byte[] key)
        {
            if (!_pendingWrite)
            {
                _pendingWrite = true;
                _pendingTime = DateTime.UtcNow;
            }

            _pendingEntries.Add(key);

            if (BackgroundWriting)
            {
                lock (_globalLock)
                {
                    _pendingFiles.Add(this.fileName);
                }
            }
            else
            {
                UpdatePendingWrites(false);
            }
        }

        // NOTE always call this from lock{} block
        private void WriteBlocks()
        {
            _stream.Seek(0, SeekOrigin.End);
            var blockOffset = (uint)_stream.Position;

            using (var writer = new BinaryWriter(_stream, Encoding.ASCII, true))
            {
                writer.Write(_header);
                writer.Write((uint)_pendingEntries.Count);
                foreach (var key in _pendingEntries)
                {
                    writer.WriteByteArray(key);

                    var entry = _entries[key];
                    writer.Write(entry.Offset);

                    switch (DataSize)
                    {
                        case KeyStoreDataSize.Small: writer.Write((byte)entry.Length); break;
                        case KeyStoreDataSize.Medium: writer.Write((ushort)entry.Length); break;
                        case KeyStoreDataSize.Large: writer.WriteUInt24(entry.Length); break;
                        case KeyStoreDataSize.Huge: writer.Write(entry.Length); break;
                        default: throw new Exception("unsupported data size");
                    }                    
                }
                writer.Write((uint)_lastBlockOffset);

                _stream.Seek(0, SeekOrigin.Begin);
                writer.Write((uint)blockOffset);
            }

            _lastBlockOffset = blockOffset;

            _pendingEntries.Clear();
            _pendingWrite = false;

            _stream.Flush();
        }

        public void ReadBlocks()
        {
            lock (_lock)
            {
                InitStream();

                _entries.Clear();
                _freeSpace.Clear();

                if (_stream.Length > 0)
                {
                    _stream.Seek(0, SeekOrigin.Begin);

                    using (var reader = new BinaryReader(_stream, Encoding.ASCII, true))
                    {
                        //uint nextOffset = (uint)(_stream.Length - _header.Length);
                        uint blockOffset = reader.ReadUInt32();
                        _lastBlockOffset = blockOffset;

                        while (blockOffset > 0)
                        {
                            _stream.Seek(blockOffset, SeekOrigin.Begin);

                            var temp = reader.ReadBytes(_header.Length);
                            Throw.If(!temp.SequenceEqual(_header), "header mark expected");

                            var entryCount = reader.ReadUInt32();
                            while (entryCount > 0)
                            {
                                var key = reader.ReadByteArray();
                                uint location = reader.ReadUInt32();

                                uint size;
                                switch (DataSize)
                                {
                                    case KeyStoreDataSize.Small: size = reader.ReadByte(); break;
                                    case KeyStoreDataSize.Medium: size = reader.ReadUInt16(); break;
                                    case KeyStoreDataSize.Large: size = reader.ReadUInt24(); break;
                                    case KeyStoreDataSize.Huge: size = reader.ReadUInt32(); break;
                                    default: throw new Exception("unsupported data size");
                                }

                                if (!_entries.ContainsKey(key))
                                {
                                    _entries[key] = new DiskEntry() { Length = size, Offset = location };
                                }

                                entryCount--;
                            }

                            blockOffset = reader.ReadUInt32();
                        }
                    }
                }
                else
                {
                    this._lastBlockOffset = 0;
                }

            }
        }

        private void InitStream()
        {
            if (_stream == null)
            {
                _stream = new FileStream(fileName, FileMode.OpenOrCreate);
                var temp = new byte[0];
                _stream.Write(temp, 0, temp.Length); // write null block offset
            }
        }

        // inserts a key/value into an existing space
        // if necessary, splits the space into two (used/free)
        // NOTE - only call this method from inside a lock {} block
        private void InsertIntoDisk(byte[] key, byte[] value, DiskEntry entry)
        {
            InitStream();

            if (_stream.Position != entry.Offset)
            {
                _stream.Seek(entry.Offset, SeekOrigin.Begin);
            }

            _stream.Write(value, 0, value.Length);
            _stream.Flush();

            var diff = entry.Length - value.Length;
            if (diff > 0)
            {
                var free = new DiskEntry() { Offset = (uint)(entry.Offset + value.Length), Length = (uint)diff };

                _freeSpace.Add(free);
            }

            _entries[key] = new DiskEntry() { Offset = entry.Offset, Length = (uint)value.Length };
            RequestUpdate(key);
        }

        public void SetValue(byte[] key, byte[] value)
        {
            Throw.If(!_ready, "not ready");

            if (value == null || value.Length == 0)
            {
                Remove(key);
                return;
            }

            if (ContainsKey(key))
            {
                DiskEntry entry;
                lock (_lock)
                {
                    entry = _entries[key];

                    // check if we can fit this in the previous occupied space
                    if (entry.Length >= value.Length)
                    {
                        InsertIntoDisk(key, value, entry);
                        return;
                    }
                    else
                    {
                        // we cant fit it into the previous space, so mark the previous space as free
                        _freeSpace.Add(entry);
                    }
                }
            }

            int freeSpaceIndex = -1;
            uint min = uint.MaxValue;

            lock (_lock)
            {
                // search for the minimum free space that can fit this new entry
                for (int i=0; i<_freeSpace.Count; i++)
                {
                    var entry = _freeSpace[i];
                    if (entry.Length >= value.Length && entry.Length < min)
                    {
                        min = entry.Length;
                        freeSpaceIndex = i;
                    }
                }
            }

            if (freeSpaceIndex >= 0)
            {
                lock (_lock)
                {
                    var entry = _freeSpace[freeSpaceIndex];
                    _freeSpace.RemoveAt(freeSpaceIndex);
                    InsertIntoDisk(key, value, entry);
                }
            }
            else // nothing found, we need to alloc more disk space here
            {
                lock (_lock)
                {
                    InitStream();
                    _stream.Seek(0, SeekOrigin.End);

                    var entry = new DiskEntry() { Length = (uint)value.Length, Offset = (uint)_stream.Position };
                    InsertIntoDisk(key, value, entry);
                }
            }
        }

        public byte[] GetValue(byte[] key)
        {
            Throw.If(!_ready, "not ready");

            if (ContainsKey(key))
            {
                var entry = _entries[key];

                var result = new byte[entry.Length];

                lock (_lock)
                {
                    if (!BackgroundWriting)
                    {
                        UpdatePendingWrites(true);
                    }

                    _stream.Seek(entry.Offset, SeekOrigin.Begin);
                    _stream.Read(result, 0, result.Length);
                }

                return result;
            }

            return null;
        }

        public bool ContainsKey(byte[] key)
        {
            bool result;

            lock (_lock)
            {
                if (!BackgroundWriting)
                {
                    UpdatePendingWrites(false);
                }

                result = _entries.ContainsKey(key);
            }

            return result;
        }

        public bool Remove(byte[] key)
        {
            Throw.If(!_ready, "not ready");

            if (ContainsKey(key))
            {
                lock (_lock)
                {
                    var entry = _entries[key];
                    _freeSpace.Add(entry);
                    _entries.Remove(key);

                    _pendingEntries.Remove(key);

                    if (!BackgroundWriting)
                    {
                        UpdatePendingWrites(false);
                    }
                }

                return true;
            }

            return false;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (!_ready)
                {
                    return;
                }

                _ready = false;
                UpdatePendingWrites(true);
            }

            if (BackgroundWriting)
            {
                lock (_globalLock)
                {
                    _storeMap.Remove(this.fileName);
                }
            }

            if (_stream != null)
            {
                ((IDisposable)_stream).Dispose();
                _stream = null;
            }
        }
    }

    public class KeyValueStore<K, V> : IKeyValueStore 
    {
        public readonly string Name;

        private MemoryStore _memory;
        private DiskStore _disk;

        public uint Count => _disk != null ? _disk.Count : _memory.Count;

        // TODO increase default size
        public KeyValueStore(string name, KeyStoreDataSize dataSize, int cacheSize)
        {
            var fileName = name + ".bin";

            var useDisk = cacheSize != -1;

            _memory = new MemoryStore(cacheSize);
            if (useDisk)
            {
                _disk = new DiskStore(fileName, dataSize);
            }
        }

        public KeyValueStore(Address address, string name, KeyStoreDataSize dataSize, int cacheSize = 16) : this(address.Text + "_" + name, dataSize, cacheSize)
        {
        }

        public V this[K key]
        {
            get { return Get(key); }
            set { Set(key, value); }
        }

        public void Set(K key, V value)
        {
            var keyBytes = Serialization.Serialize(key);
            var valBytes = Serialization.Serialize(value);
            SetValue(keyBytes, valBytes);
        }

        public V Get(K key)
        {
            var keyBytes = Serialization.Serialize(key);
            var bytes = GetValue(keyBytes);
            Throw.If(bytes == null, "item not found in keystore");
            return Serialization.Unserialize<V>(bytes);
        }

        public void SetValue(byte[] key, byte[] value)
        {
            _memory.SetValue(key, value);

            if (_disk != null)
            {
                _disk.SetValue(key, value);
            }
        }

        public byte[] GetValue(byte[] key)
        {
            if (_memory.ContainsKey(key))
            {
                return _memory.GetValue(key);
            }

            if (_disk != null)
            {
                var result = _disk.GetValue(key);
                _memory.SetValue(key, result); // promote this value to memory cache
                return result;
            }
            else {
                return null;
            }
        }

        public bool ContainsKey(K key)
        {
            var keyBytes = Serialization.Serialize(key);
            return ContainsKey(keyBytes);
        }

        public bool ContainsKey(byte[] key)
        {
            if (_memory.ContainsKey(key))
            {
                return true;
            }

            if (_disk != null)
            {
                return _disk.ContainsKey(key);
            }

            return false;
        }

        public bool Remove(K key)
        {
            var keyBytes = Serialization.Serialize(key);
            return Remove(keyBytes);
        }

        public bool Remove(byte[] key)
        {
            if (_disk == null)
            {
                return _memory.Remove(key);
            }

            if (_disk.Remove(key))
            {
                _memory.Remove(key);
                return true;
            }

            return false;
        }
    }
}
