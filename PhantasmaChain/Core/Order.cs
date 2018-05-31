using PhantasmaChain.Cryptography;
using System;
using System.Numerics;

namespace PhantasmaChain.Core
{
    /*public class Order
    {
        public UInt256 orderID { get; private set; }
        public byte[] ownerPublicKey { get; private set; }
        public byte[] baseTokenID { get; private set; }
        public byte[] quoteTokenID { get; private set; }
        public OrderKind orderKind { get; private set; }
        public OrderSide side { get; private set; }
        public BigInteger amount { get; private set; }
        public BigInteger price { get; private set; }
        public uint creation { get; private set; }
        public uint expiration { get; private set; }

        public Order(UInt256 orderID, byte[] ownerPublicKey, byte[] baseTokenID, byte[] quoteTokenID, OrderKind orderKind, OrderSide side, BigInteger amount, BigInteger price, uint creation, uint expiration)
        {
            this.orderID = orderID;
            this.ownerPublicKey = ownerPublicKey;
            this.baseTokenID = baseTokenID;
            this.quoteTokenID = quoteTokenID;
            this.orderKind = orderKind;
            this.side = side;
            this.amount = amount;
            this.price = price;
            this.creation = creation;
            this.expiration = expiration;
        }

        internal void Fill(BigInteger total, Action<Event> notify)
        {
            this.amount -= total;
            notify(new Event(EventKind.Fill, this.ownerPublicKey));
        }
    }*/
}
