using Phantasma.Core;

namespace Phantasma.Blockchain.Tokens
{
    public class TokenContent
    {
        public byte[] ReadOnlyData { get; private set; }
        public byte[] DynamicData { get; private set; }

        public TokenContent(byte[] data)
        {
            Throw.IfNull(data, "readonly data");
            this.ReadOnlyData = data;
        }

        public void SetDynamicData(byte[] data)
        {
            Throw.IfNull(data, "data");
            this.DynamicData = data;
        }
    }
}
