using Phantasma.Cryptography.Hashing;

namespace Phantasma.Cryptography
{
    public static class HMAC512
    {
        const byte IPAD = (byte)0x36;
        const byte OPAD = (byte)0x5C;

        private static byte[] GeneratePadding(byte[] key, byte padding)
        {
            byte[] buf = new byte[BlockSize];

            for (int i = 0; i < key.Length; ++i)
                buf[i] = (byte)((byte)key[i] ^ padding);

            for (int i = key.Length; i < BlockSize; ++i)
                buf[i] = padding;

            return buf;
        }

        public const int BlockSize = 128;

        public static byte[] ComputeHash(byte[] key, byte[] msg)
        {
            var _sha512 = new SHA512();

            if (key.Length > BlockSize)
            {
                _sha512.Init();
                _sha512.Update(key, 0, key.Length);
                key = _sha512.Finish();
            }

            _sha512.Init();

            var i_pad = GeneratePadding(key, IPAD);
            _sha512.Update(i_pad, 0, i_pad.Length);
            _sha512.Update(msg, 0, msg.Length);

            var temp = _sha512.Finish();
            _sha512.Init();

            var o_pad = GeneratePadding(key, OPAD);
            _sha512.Update(o_pad, 0, o_pad.Length);
            _sha512.Update(temp, 0, temp.Length);

            return _sha512.Finish();
        }
    }
}
