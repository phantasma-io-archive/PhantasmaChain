using System;


namespace Phantasma.Storage.Context
{
    public interface IStorageCollection
    {
        byte[] BaseKey { get; }
        StorageContext Context { get; }
    }

    public class StorageException: Exception
    {
        public StorageException(string msg): base(msg)
        {

        }
    }
}
