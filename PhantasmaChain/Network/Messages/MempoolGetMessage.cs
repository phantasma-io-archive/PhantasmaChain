using System;
using System.IO;

namespace Phantasma.Network
{
    internal class MempoolGetMessage : Message
    {
        public MempoolGetMessage(byte[] pubKey) :base(Opcode.MEMPOOL_Get, pubKey)
        {
        }

        internal static MempoolGetMessage FromReader(byte[] pubKey, BinaryReader reader)
        {
            return new MempoolGetMessage(pubKey);
        }
    }
}