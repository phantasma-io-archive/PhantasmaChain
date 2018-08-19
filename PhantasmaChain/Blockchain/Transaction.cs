using System;
using System.IO;
using Phantasma.VM.Contracts;
using Phantasma.Cryptography;
using Phantasma.Utils;
using Phantasma.VM;
using Phantasma.VM.Types;
using Phantasma.Mathematics;

namespace Phantasma.Blockchain
{
    public sealed class Transaction: ITransaction
    {
        public Address SourceAddress { get; }
        public BigInteger Fee { get; }
        public BigInteger Index { get; }
        public byte[] Script { get; }

        public Signature Signature { get; private set; }
        public Hash Hash { get; private set; }

        public static Transaction Unserialize(BinaryReader reader)
        {
            var address = reader.ReadAddress();
            var script = reader.ReadByteArray();
            var fee = reader.ReadBigInteger();
            var txOrder = reader.ReadBigInteger();

            return new Transaction(address, script, fee, txOrder);
        }

        private void Serialize(BinaryWriter writer, bool withSignature)
        {
            writer.WriteAddress(this.SourceAddress);
            writer.WriteByteArray(this.Script);
            writer.WriteBigInteger(this.Fee);
            writer.WriteBigInteger(this.Index);

            if (withSignature)
            {
                Throw.If(this.Signature == null, "Signature cannot be null");

                this.Signature.Serialize(writer);
            }
        }

        // TODO should run the script and return true if sucess or false if exception
        private bool Validate(Chain chain, out BigInteger fee)
        {
            fee = 0;
            return true;
        }

        internal bool Execute(Chain chain, Action<Event> notify)
        {
            var vm = new RuntimeVM(chain, this);

            var state = vm.Execute();

            if (state != ExecutionState.Halt)
            {
                return false;
            }

            var cost = vm.gas;

            // fee distribution TODO
//            if (chain.NativeTokenAddress != null && cost > 0)
            {
                //chain.TransferToken(this.PublicKey, chain.DistributionPubKey, cost);
            }

            // TODO take storage changes from vm execution and apply to global state
            //this.Apply(chain, notify);

            return true;
        }

        public Transaction(Address sourceAddress, byte[] script, BigInteger fee, BigInteger txOrder)
        {
            this.Script = script;
            this.SourceAddress = sourceAddress;
            this.Fee = fee;
            this.Index = txOrder;

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
            this.Signature = owner.Sign(msg);

            return true;
        }

        public bool IsValid(Chain chain)
        {
            if (this.Signature == null)
            {
                return false;
            }

            var data = ToArray(false);
            if (!this.Signature.Verify(data, this.SourceAddress))
            {
                return false;
            }

            BigInteger cost;
            var validation = Validate(chain, out cost);
            if (!validation)
            {
                return false;
            }
           
            /*if (chain.NativeTokenAddress != null)
            {
                if (this.Fee < cost)
                {
                    return false;
                }

                var balance = chain.GetTokenBalance(chain.NativeTokenAddress, this.SourceAddress);

                if (balance < this.Fee)
                {
                    return false;
                }
            }*/

            return true;
        }

        private void UpdateHash()
        {
            var data = this.ToArray(false);
            var hash = CryptoUtils.Sha256(data);
            this.Hash = new Hash(hash);
        }

    }
}
