using System.IO;
using System.IO.Compression;

namespace Phantasma.Storage.Utils
{
    public static class CompressionUtils
    {
        public static byte[] Compress(byte[] data)
        {
            var output = new MemoryStream();
            using (var dstream = new DeflateStream(output, CompressionLevel.Optimal))
            {
                dstream.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        public static byte[] Decompress(byte[] data)
        {
            var input = new MemoryStream(data);
            var output = new MemoryStream();
            using (var dstream = new DeflateStream(input, CompressionMode.Decompress))
            {
                dstream.CopyTo(output);
            }
            return output.ToArray();
        }
    }

}
