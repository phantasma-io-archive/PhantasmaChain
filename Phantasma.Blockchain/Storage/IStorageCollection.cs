namespace Phantasma.Blockchain.Storage
{
    public interface IStorageCollection
    {
        byte[] BaseKey { get; }
        StorageContext Context { get; }
    }
}
