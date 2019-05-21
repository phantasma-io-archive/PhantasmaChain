namespace Phantasma.Storage
{
    public interface IKeyStore
    {
        void Write(byte[] key, byte[] value);
        byte[] Read(byte[] key);
    }
}
