using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

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
    public sealed class Transaction : ISerializable
    {
        public Timestamp Expiration { get; private set; }
        public byte[] Script { get; private set; }

        public string NexusName { get; private set; }
        public string ChainName { get; private set; }

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
            var tx = new Transaction();
            tx.UnserializeData(reader);
            return tx;
        }

        public void Serialize(BinaryWriter writer, bool withSignature)
        {
            writer.WriteVarString(this.NexusName);
            writer.WriteVarString(this.ChainName);
            writer.WriteByteArray(this.Script);
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

        internal bool Execute(Chain chain, Block block, StorageChangeSetContext changeSet, Action<Hash, Event> onNotify, out byte[] result)
        {
            result = null;

            var runtime = new RuntimeVM(this.Script, chain, block, this, changeSet, false);

            var state = runtime.Execute();

            if (state != ExecutionState.Halt)
            {
                return false;
            }

            var cost = runtime.UsedGas;

            // fee distribution TODO
            //            if (chain.NativeTokenAddress != null && cost > 0)
            {
                //chain.TransferToken(this.PublicKey, chain.DistributionPubKey, cost);
            }

            foreach (var evt in runtime.Events)
            {
                onNotify(this.Hash, evt);
            }

            if (runtime.Stack.Count > 0)
            {
                var obj = runtime.Stack.Pop();
                result = Serialization.Serialize(obj);
            }

            return true;
        }

        // required for deserialization
        public Transaction()
        {

        }

        public Transaction(string nexusName, string chainName, byte[] script, Timestamp expiration, IEnumerable<Signature> signatures = null)
        {
            Throw.IfNull(script, nameof(script));

            this.NexusName = nexusName;
            this.ChainName = chainName;
            this.Script = script;
            this.Expiration = expiration;

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
            var hash = CryptoExtensions.SHA256(data);
            this.Hash = new Hash(hash);
        }

        public void SerializeData(BinaryWriter writer)
        {
            this.Serialize(writer, true);
        }

        public void UnserializeData(BinaryReader reader)
        {
            this.NexusName = reader.ReadVarString();
            this.ChainName = reader.ReadVarString();
            this.Script = reader.ReadByteArray();
            this.Expiration = reader.ReadUInt32();

            // check if we have some signatures attached
            try
            {
                var signatureCount = (int)reader.ReadVarInt();
                this.Signatures = new Signature[signatureCount];
                for (int i = 0; i < signatureCount; i++)
                {
                    Signatures[i] = reader.ReadSignature();
                }
            }
            catch
            {
                this.Signatures = new Signature[0];
            }

            this.UpdateHash();
        }
    }
}
