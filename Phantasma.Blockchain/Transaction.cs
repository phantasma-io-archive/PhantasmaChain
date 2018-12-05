using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using Phantasma.VM.Contracts;
using Phantasma.Cryptography;
using Phantasma.Core;
using Phantasma.VM;
using Phantasma.Numerics;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Storage;
using Phantasma.IO;
using Phantasma.Core.Types;

namespace Phantasma.Blockchain
{
    public sealed class Transaction
    {
        public Timestamp Expiration { get; }
        public byte[] Script { get; }
        public uint Nonce { get; }

        public string NexusName { get; }
        public string ChainName { get; }

        public BigInteger GasPrice { get; }
        public BigInteger GasLimit { get; }

        public Signature[] Signatures { get; private set; }
        public Hash Hash { get; private set; }

        public static Transaction Unserialize(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    return Unserialize(reader);
                }
            }
        }

        public static Transaction Unserialize(BinaryReader reader)
        {
            var nexusName = reader.ReadShortString();
            var chainName = reader.ReadShortString();
            var script = reader.ReadByteArray();
            var gasPrice = reader.ReadBigInteger();
            var gasLimit = reader.ReadBigInteger();
            var nonce = reader.ReadUInt32();
            var expiration = reader.ReadUInt32();

            Signature[] signatures;

            // check if we have some signatures attached
            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var signatureCount = (int)reader.ReadVarInt();
                signatures = new Signature[signatureCount];
                for (int i = 0; i < signatureCount; i++)
                {
                    signatures[i] = reader.ReadSignature();
                }
            }
            else
            {
                signatures = new Signature[0];
            }

            return new Transaction(nexusName, chainName, script, gasPrice, gasLimit, new Timestamp(expiration), nonce, signatures);
        }

        private void Serialize(BinaryWriter writer, bool withSignature)
        {
            writer.WriteShortString(this.NexusName);
            writer.WriteShortString(this.ChainName);
            writer.WriteByteArray(this.Script);
            writer.WriteBigInteger(this.GasPrice);
            writer.WriteBigInteger(this.GasLimit);
            writer.Write(this.Nonce);
            writer.Write(this.Expiration.Value);

            if (withSignature)
            {
                writer.WriteVarInt(Signatures.Length);
                foreach (var signature in this.Signatures)
                {
                    writer.WriteSignature(signature);
                }
            }
        }

        public override string ToString()
        {
            return $"{Hash}";
        }

        // TODO should run the script and return true if sucess or false if exception
        private bool Validate(Chain chain, out BigInteger fee)
        {
            fee = 0;
            return true;
        }

        internal bool Execute(Chain chain, Block block, StorageChangeSetContext changeSet, Action<Hash, Event> onNotify)
        {
            var runtime = new RuntimeVM(this.Script, chain, block, this, changeSet);

            var state = runtime.Execute();

            if (state != ExecutionState.Halt)
            {
                return false;
            }

            var cost = runtime.usedGas;

            // fee distribution TODO
//            if (chain.NativeTokenAddress != null && cost > 0)
            {
                //chain.TransferToken(this.PublicKey, chain.DistributionPubKey, cost);
            }

            foreach (var evt in runtime.Events)
            {
                onNotify(this.Hash, evt);
            }
            
            return true;
        }

        public Transaction(string nexusName, string chainName, byte[] script, BigInteger gasPrice, BigInteger gasLimit, Timestamp expiration, uint nonce, IEnumerable<Signature> signatures = null)
        {
            Throw.IfNull(script, nameof(script));
            Throw.IfNull(gasPrice, nameof(gasPrice));
            Throw.IfNull(gasLimit, nameof(gasLimit));

            this.NexusName = nexusName;
            this.ChainName = chainName;
            this.Script = script;
            this.GasPrice = gasPrice;
            this.GasLimit = gasLimit;
            this.Expiration = expiration;
            this.Nonce = nonce;

            this.Signatures = signatures != null && signatures.Any() ? signatures.ToArray() : new Signature[0];

            this.UpdateHash();
        }

        public byte[] ToByteArray(bool withSignature)
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

        public bool HasSignatures => Signatures != null && Signatures.Length > 0;

        public void Sign(Signature signature)
        {
            this.Signatures = this.Signatures.Union(new Signature[] { signature }).ToArray();
        }

        public void Sign(KeyPair owner)
        {
            Throw.If(owner == null, "invalid keypair");

            var msg = this.ToByteArray(false);
            this.Signatures = new Signature[] { owner.Sign(msg) };
        }

        public bool IsSignedBy(Address address)
        {
            return IsSignedBy(new Address[] { address });
        }

        public bool IsSignedBy(IEnumerable<Address> addresses)
        {
            if (!HasSignatures)
            {
                return false;
            }

            var msg = this.ToByteArray(false);

            foreach (var signature in this.Signatures)
            {
                if (signature.Verify(msg, addresses))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsValid(Chain chain)
        {
            if (chain.Name != this.ChainName)
            {
                return false;
            }

            if (chain.Nexus.Name != this.NexusName)
            {
                return false;
            }

            // TODO unsigned tx should be supported too
            /* if (!IsSigned) 
             {
                 return false;
             }

             var data = ToArray(false);
             if (!this.Signature.Verify(data, this.SourceAddress))
             {
                 return false;
             }*/

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
            var data = this.ToByteArray(false);
            var hash = CryptoExtensions.Sha256(data);
            this.Hash = new Hash(hash);
        }
    }
}
