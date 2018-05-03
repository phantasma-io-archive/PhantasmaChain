using PhantasmaChain.Core;
using PhantasmaChain.Cryptography;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace PhantasmaChain.Transactions
{
    public enum OrderKind
    {
        GTC,
        FOK,
        IOC,
        Market
    }

    public enum OrderSide
    {
        Buy,
        Sell
    }

    public class OrderTransaction : Transaction
    {
        public byte[] baseTokenID { get; private set; }
        public byte[] quoteTokenID { get; private set; }
        public OrderKind orderKind { get; private set; }
        public OrderSide side { get; private set; }
        public BigInteger amount { get; private set; }
        public BigInteger price { get; private set; }
        public uint expiration { get; private set; }


        public OrderTransaction(byte[] publicKey, BigInteger fee, uint txOrder, byte[] baseTokenID, byte[] quoteTokenID, OrderKind orderKind, OrderSide side, BigInteger amount, BigInteger price, uint expiration) : base(TransactionKind.Order, publicKey, fee, txOrder)
        {
            this.baseTokenID = baseTokenID;
            this.quoteTokenID = quoteTokenID;
            this.orderKind = orderKind;
            this.side = side;
            this.amount = amount;
            this.price = price;
            this.expiration = expiration;
        }

        public override BigInteger GetCost(Chain chain)
        {
            return 0;
        }

        protected override void SerializeData(BinaryWriter writer)
        {
            writer.WriteByteArray(this.baseTokenID);
            writer.WriteByteArray(this.quoteTokenID);
            writer.Write((byte)this.orderKind);
            writer.Write((byte)this.side);
            writer.WriteBigInteger(this.amount);
            writer.WriteBigInteger(this.price);
            writer.Write((uint)this.expiration);
        }

        protected override void UnserializeData(BinaryReader reader)
        {
            this.baseTokenID = reader.ReadByteArray();
            this.quoteTokenID = reader.ReadByteArray();
            this.orderKind = (OrderKind)reader.ReadByte();
            this.side = (OrderSide)reader.ReadByte();
            this.amount = reader.ReadBigInteger();
            this.price = reader.ReadBigInteger();
            this.expiration = reader.ReadUInt32();
        }

        protected override bool ValidateData(Chain chain)
        {
            var account = chain.GetAccount(this.PublicKey);
            if (account == null)
            {
                return false;
            }

            var baseToken = chain.GetTokenByID(this.baseTokenID);
            if (baseToken == null)
            {
                return false;
            }

            var quoteToken = chain.GetTokenByID(this.quoteTokenID);
            if (quoteToken == null)
            {
                return false;
            }

            // TODO take into acount tokens decimals
            switch (this.side)
            {
                case OrderSide.Buy:
                    {
                        var balance = account.GetBalance(baseToken);
                        var total = this.amount * this.price;

                        if (balance < total)
                        {
                            return false;
                        }

                        break;
                    }

                case OrderSide.Sell:
                    {
                        var balance = account.GetBalance(quoteToken);
                        if (balance < this.amount)
                        {
                            return false;
                        }

                        break;
                    }
            }

            return true;
        }

        protected override void Apply(Chain chain, Action<Event> notify)
        {
            var account = chain.GetAccount(this.PublicKey);
            if (account == null)
            {
                throw new ChainException("Account does not exist");
            }

            var baseToken = chain.GetTokenByID(this.baseTokenID);

            if (baseToken == null)
            {
                throw new ChainException("Base token does not exist");
            }

            var quoteToken = chain.GetTokenByID(this.quoteTokenID);
            if (quoteToken == null)
            {
                throw new ChainException("Quote token does not exist");
            }

            switch (this.side)
            {
                case OrderSide.Buy:
                    {
                        var total = this.amount * this.price;
                        account.Withdraw(baseToken, total, notify);
                        break;
                    }

                case OrderSide.Sell:
                    {
                        account.Withdraw(quoteToken, this.amount, notify);
                        break;
                    }
            }

            var orderID = new UInt256(this.Hash);
            chain.CreateOrder(orderID, this.PublicKey, this.baseTokenID, this.quoteTokenID, this.orderKind, this.side, this.amount, this.price, this.expiration, notify);
        }

    }
}
