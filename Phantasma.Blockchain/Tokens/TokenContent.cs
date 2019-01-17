using Phantasma.Core;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain.Tokens
{
    public class TokenContent
    {
        public Address CurrentChain { get; internal set; }
        public Address CurrentOwner { get; internal set; }
        public byte[] ROM { get; private set; }
        public byte[] RAM { get; private set; }

        public TokenContent(byte[] rom, byte[] ram)
        {
            Throw.IfNull(rom, nameof(rom));
            Throw.IfNull(ram, nameof(ram));
            this.ROM = rom;
            this.RAM= ram;
        }

        public void WriteData(byte[] data)
        {
            Throw.IfNull(data, nameof(data));
            this.RAM = data;
        }
    }
}
