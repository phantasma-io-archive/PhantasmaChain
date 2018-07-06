using Phantasma.Core;
using System;
using System.IO;

namespace Phantasma.Network
{
    internal class MempoolAddMessage : Message
    {
        public readonly Transaction transaction;

        public MempoolAddMessage(Transaction tx)
        {
            this.transaction = tx;
        }

        internal static MempoolAddMessage FromReader(BinaryReader reader)
        {
            var tx = Transaction.Unserialize(reader);
            return new MempoolAddMessage(tx); 
        }
    }
}