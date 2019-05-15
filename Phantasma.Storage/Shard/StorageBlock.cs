using System.Collections.Generic;
using System.IO;

namespace Phantasma.Storage.Sharding
{
    public class StorageBlock
    {
        public const int BlockSize = 1024;

        public static int DATA_SHARDS = 4;
        public static int PARITY_SHARDS = 2;
        public static int TOTAL_SHARDS = 6;

        public readonly int Index;
        public readonly Shard[] Shards;

        public StorageBlock(int index, byte[] srcData)
        {
            this.Index = index;
            this.Shards = new Shard[TOTAL_SHARDS];

            int shardSize = (int)((srcData.Length+ DATA_SHARDS - 1) / DATA_SHARDS);

            // Fill in the data shards
            int offset = 0;
            for (int i=0; i<DATA_SHARDS; i++)
            {
                for (int j=0; j<shardSize; j++)
                {
                    if (offset >= srcData.Length)
                    {
                        break;
                    }

                    Shards[i].Bytes[j] = srcData[offset];
                   offset++;
                }
            }

            // Use Reed-Solomon to calculate the parity.
            ReedSolomon reedSolomon = new ReedSolomon(DATA_SHARDS, PARITY_SHARDS);
            reedSolomon.encodeParity(Shards, 0, shardSize);
        }

        public static List<StorageBlock> FromFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    return FromStream(stream);
                }
            }
            else
            {
                throw new FileNotFoundException("Could not find file: "+filePath);
            }
        }

        public static List<StorageBlock> FromStream(Stream stream)
        {
            var result = new List<StorageBlock>();
            using (var reader = new BinaryReader(stream))
            {
                byte[] buffer = new byte[BlockSize];
                int count;
                int index = 0;
                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                {
                    var block = new StorageBlock(index, buffer);
                    result.Add(block);
                    index++;
                }
            }
            return result;
        }
    }
}
