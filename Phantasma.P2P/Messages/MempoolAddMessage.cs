using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phantasma.Blockchain;
using Phantasma.Cryptography;

namespace Phantasma.Network.P2P.Messages
{
    public class MempoolAddMessage : Message
    {
        public readonly Transaction[] Transactions;

        public MempoolAddMessage(Address pubKey, string host, IEnumerable<Transaction> txs) : base(Opcode.MEMPOOL_Add, pubKey, host)
        {
            this.Transactions = txs.ToArray();
        }

        internal static MempoolAddMessage FromReader(Address address, string host, BinaryReader reader)
        {
            var count = reader.ReadUInt16();
            var transactions = new Transaction[count];
            for (int i=0; i<count; i++)
            {
                transactions[i] = Transaction.Unserialize(reader);
            }

            return new MempoolAddMessage(address, host, transactions);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            writer.Write((ushort)Transactions.Length);
            foreach (var tx in Transactions)
            {
                tx.Serialize(writer, true);
            }
        }
    }
}