using Phantasma.Contracts;
using Phantasma.Utils;
using Phantasma.VM;
using System;
using System.IO;
using System.Numerics;

namespace Phantasma.Core
{
    public class Transaction: ITransaction
    {
        public byte[] PublicKey { get; }
        public BigInteger Fee { get; }
        public BigInteger Order { get; }
        public byte[] Script { get; }

        public byte[] Signature { get; private set; }
        public byte[] Hash { get; private set; }

        protected Transaction Unserialize(BinaryReader reader)
        {
            var publicKey = reader.ReadByteArray();
            var script = reader.ReadByteArray();
            var fee = reader.ReadBigInteger();
            var txOrder = reader.ReadBigInteger();

            return new Transaction(publicKey, script, fee, txOrder);
        }

        protected void Serialize(BinaryWriter writer, bool withSignature)
        {
            writer.WriteByteArray(this.PublicKey);
            writer.WriteByteArray(this.Script);
            writer.WriteBigInteger(this.Fee);
            writer.WriteBigInteger(this.Order);

            if (withSignature)
            {
                if (this.Signature == null)
                {
                    throw new Exception("Signature cannot be null");
                }

                writer.WriteByteArray(this.Signature);
            }
        }

        // TODO should run the script and return true if sucess or false if exception
        protected bool Validate(Chain chain, out BigInteger fee)
        {
            fee = 0;
            return true;
        }

        internal bool Execute(Chain chain, Action<Event> notify)
        {
            var vm = new RuntimeVM(chain, this);

            vm.Execute();

            if (vm.State != ExecutionState.Halt)
            {
                return false;
            }

            var cost = vm.gas;

            /*if (chain.NativeToken != null && cost > 0)
            {
                chain.NativeToken.Burn(new Address(this.PublicKey), cost);
            }*/

            // TODO take storage changes from vm execution and apply to global state
            //this.Apply(chain, notify);

            return true;
        }

        public Transaction(byte[] publicKey, byte[] script, BigInteger fee, BigInteger txOrder)
        {
            this.Script = script;
            this.PublicKey = publicKey;
            this.Fee = fee;
            this.Order = txOrder;

            this.UpdateHash();
        }

        public byte[] ToArray(bool withSignature)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    Serialize(writer, withSignature);
                }

                return stream.ToArray();
            }
        }

        public bool Sign(KeyPair owner)
        {
            if (this.Signature != null)
            {
                return false;
            }

            if (owner == null)
            {
                return false;
            }

            var msg = this.ToArray(false);
            this.Signature = CryptoUtils.Sign(msg, owner.PrivateKey, owner.PublicKey);

            return true;
        }

        public bool IsValid(Chain chain)
        {
            if (this.Signature == null)
            {
                return false;
            }

            var data = ToArray(false);
            if (!CryptoUtils.VerifySignature(data, this.Signature, this.PublicKey))
            {
                return false;
            }

            BigInteger cost;
            var validation = Validate(chain, out cost);
            if (!validation)
            {
                return false;
            }

            if (chain.NativeTokenPubKey != null)
            {
                if (this.Fee < cost)
                {
                    return false;
                }

                var balance = chain.GetTokenBalance(chain.NativeTokenPubKey, this.PublicKey);

                if (balance < this.Fee)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateHash()
        {
            var data = this.ToArray(false);
            this.Hash = CryptoUtils.Sha256(data);
        }

    }
}
