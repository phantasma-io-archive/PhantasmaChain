namespace Phantasma.Storage.Sharding
{
    public struct Shard
    {
        public readonly byte[] Bytes;

        public Shard(byte[] data)
        {
            this.Bytes = data;
        }

        public Shard(int length)
        {
            this.Bytes = new byte[length];
        }
    }
}
