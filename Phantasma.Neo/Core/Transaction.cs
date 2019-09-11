using Phantasma.Cryptography.ECC;
using Phantasma.Neo.Cryptography;
using Phantasma.Neo.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Neo.Core
{
    public enum TransactionAttributeUsage
    {
        ContractHash = 0x00,
        ECDH02 = 0x02,
        ECDH03 = 0x03,
        Script = 0x20,
        Vote = 0x30,
        DescriptionUrl = 0x81,
        Description = 0x90,

        Hash1 = 0xa1,
        Hash2 = 0xa2,
        Hash3 = 0xa3,
        Hash4 = 0xa4,
        Hash5 = 0xa5,
        Hash6 = 0xa6,
        Hash7 = 0xa7,
        Hash8 = 0xa8,
        Hash9 = 0xa9,
        Hash10 = 0xaa,
        Hash11 = 0xab,
        Hash12 = 0xac,
        Hash13 = 0xad,
        Hash14 = 0xae,
        Hash15 = 0xaf,

        Remark = 0xf0,
        Remark1 = 0xf1,
        Remark2 = 0xf2,
        Remark3 = 0xf3,
        Remark4 = 0xf4,
        Remark5 = 0xf5,
        Remark6 = 0xf6,
        Remark7 = 0xf7,
        Remark8 = 0xf8,
        Remark9 = 0xf9,
        Remark10 = 0xfa,
        Remark11 = 0xfb,
        Remark12 = 0xfc,
        Remark13 = 0xfd,
        Remark14 = 0xfe,
        Remark15 = 0xff
    }

    public struct TransactionAttribute
    {
        public TransactionAttributeUsage Usage;
        public byte[] Data;

        public TransactionAttribute(TransactionAttributeUsage usage, byte[] data)
        {
            Usage = usage;
            Data = data;
        }

        public static TransactionAttribute Unserialize(BinaryReader reader)
        {
            var Usage = (TransactionAttributeUsage)reader.ReadByte();

            byte[] Data;

            if (Usage == TransactionAttributeUsage.ContractHash || Usage == TransactionAttributeUsage.Vote || (Usage >= TransactionAttributeUsage.Hash1 && Usage <= TransactionAttributeUsage.Hash15))
                Data = reader.ReadBytes(32);
            else if (Usage == TransactionAttributeUsage.ECDH02 || Usage == TransactionAttributeUsage.ECDH03)
                Data = new[] { (byte)Usage }.Concat(reader.ReadBytes(32)).ToArray();
            else if (Usage == TransactionAttributeUsage.Script)
                Data = reader.ReadBytes(20);
            else if (Usage == TransactionAttributeUsage.DescriptionUrl)
                Data = reader.ReadBytes(reader.ReadByte());
            else if (Usage == TransactionAttributeUsage.Description || Usage >= TransactionAttributeUsage.Remark)
                Data = reader.ReadVarBytes(ushort.MaxValue);
            else
                throw new NotImplementedException();

            return new TransactionAttribute() { Usage = Usage, Data = Data };
        }

        internal void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Usage);
            if (Usage == TransactionAttributeUsage.DescriptionUrl)
                writer.Write((byte)Data.Length);
            else if (Usage == TransactionAttributeUsage.Description || Usage >= TransactionAttributeUsage.Remark)
                writer.WriteVarInt(Data.Length);
            if (Usage == TransactionAttributeUsage.ECDH02 || Usage == TransactionAttributeUsage.ECDH03)
                writer.Write(Data, 1, 32);
            else
                writer.Write(Data);
        }
    }

    public class Witness
    {
        public byte[] invocationScript;
        public byte[] verificationScript;

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteVarBytes(this.invocationScript);
            writer.WriteVarBytes(this.verificationScript);
        }

        public static Witness Unserialize(BinaryReader reader)
        {
            var invocationScript = reader.ReadVarBytes(65536);
            var verificationScript = reader.ReadVarBytes(65536);
            return new Witness() { invocationScript = invocationScript, verificationScript = verificationScript };
        }
    }

    public enum TransactionType : byte
    {
        MinerTransaction = 0x00,
        IssueTransaction = 0x01,
        ClaimTransaction = 0x02,
        EnrollmentTransaction = 0x20,
        RegisterTransaction = 0x40,
        ContractTransaction = 0x80,
        StateTransaction = 0x90,
        PublishTransaction = 0xd0,
        InvocationTransaction = 0xd1
    }

    public class Transaction 
    {
        public class Input 
        {
            public UInt256 prevHash;
            public uint prevIndex;
        }

        public class Output
        {
            public UInt160 scriptHash;
            public byte[] assetID;
            public decimal value;
        }

        public TransactionType type;
        public byte version;
        public byte[] script;
        public decimal gas;

        public Block block;

        public Input[] inputs;
        public Output[] outputs;
        public Witness[] witnesses;

        public Input[] claimReferences;
        public TransactionAttribute[] attributes;

        public AssetRegistration assetRegistration;
        public Contract contractRegistration;

        public ECPoint enrollmentPublicKey;

        public uint nonce;

        #region HELPERS
        protected static void SerializeTransactionInput(BinaryWriter writer, Input input)
        {
            writer.Write(input.prevHash.ToArray());
            writer.Write((ushort)input.prevIndex);
        }

        protected static void SerializeTransactionOutput(BinaryWriter writer, Output output)
        {
            writer.Write(output.assetID);
            writer.WriteFixed(output.value);
            writer.Write(output.scriptHash.ToArray());
        }

        protected static Input UnserializeTransactionInput(BinaryReader reader)
        {
            var prevHash = reader.ReadBytes(32);
            var prevIndex = reader.ReadUInt16();
            return new Input() { prevHash = new UInt256(prevHash), prevIndex = prevIndex };
        }

        protected static Output UnserializeTransactionOutput(BinaryReader reader)
        {
            var assetID = reader.ReadBytes(32);
            var value = reader.ReadFixed();
            var scriptHash = reader.ReadBytes(20);
            return new Output() { assetID = assetID, value = value, scriptHash = new UInt160(scriptHash) };
        }
        #endregion

        public byte[] GetMessage()
        {
            return this.Serialize(false);
        }

        public override string ToString()
        {
            return Hash.ToString();
        }

        public bool HasOutput(string address)
        {
            return HasOutput(new UInt160(address.AddressToScriptHash()));
        }

        public bool HasOutput(UInt160 addressHash)
        {
            foreach (var output in this.outputs)
            {
                if (output.scriptHash.Equals(addressHash))
                {
                    return true;
                }
            }

            return false;
        }

        public byte[] Serialize(bool signed = true)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write((byte)this.type);
                    writer.Write((byte)this.version);

                    // exclusive data
                    switch (this.type)
                    {
                        case TransactionType.InvocationTransaction:
                            {
                                writer.WriteVarInt(this.script.Length);
                                writer.Write(this.script);
                                if (this.version >= 1)
                                {
                                    writer.WriteFixed(this.gas);
                                }
                                break;
                            }

                        case TransactionType.MinerTransaction:
                            {
                                writer.Write((uint)this.nonce);
                                break;
                            }

                        case TransactionType.ClaimTransaction:
                            {
                                writer.WriteVarInt(this.claimReferences.Length);
                                foreach (var entry in this.claimReferences)
                                {
                                    SerializeTransactionInput(writer, entry);
                                }
                                break;
                            }

                        case TransactionType.RegisterTransaction:
                            {
                                this.assetRegistration.Serialize(writer);
                                break;
                            }

                        case TransactionType.PublishTransaction:
                            {
                                this.contractRegistration.Serialize(writer, this.version);
                                break;
                            }

                        case TransactionType.EnrollmentTransaction:
                            {
                                writer.Write(this.enrollmentPublicKey.EncodePoint(true));
                                break;
                            }

                    }

                    // Don't need any attributes
                    if (this.attributes != null)
                    {
                        writer.WriteVarInt(this.attributes.Length);
                        foreach (var attr in this.attributes)
                        {
                            attr.Serialize(writer);
                        }
                    }
                    else
                    {
                        writer.Write((byte)0);
                    }

                    if (this.inputs != null)
                    {
                        writer.WriteVarInt(this.inputs.Length);
                        foreach (var input in this.inputs)
                        {
                            SerializeTransactionInput(writer, input);
                        }
                    }
                    else
                    {
                        writer.Write((byte)0);
                    }

                    if (this.outputs != null)
                    {
                        writer.WriteVarInt(this.outputs.Length);
                        foreach (var output in this.outputs)
                        {
                            SerializeTransactionOutput(writer, output);
                        }
                    }
                    else
                    {
                        writer.Write((byte)0);
                    }

                    if (signed)
                    {
                        if (this.witnesses != null)
                        {
                            writer.WriteVarInt(this.witnesses.Length);
                            foreach (var witness in this.witnesses)
                            {
                                witness.Serialize(writer);
                            }
                        }
                        else
                        {
                            writer.Write((byte)0);
                        }
                    }

                }

                return stream.ToArray();
            }
        }

        public void Sign(NeoKey key, IEnumerable<Witness> witnesses = null)
        {
            var txdata = this.Serialize(false);

            var witList = new List<Witness>();

            if (key != null)
            {
                var privkey = key.PrivateKey;
                var pubkey = key.PublicKey;
                var signature = CryptoUtils.Sign(txdata, privkey, pubkey);

                var invocationScript = new byte[] { (byte)OpCode.PUSHBYTES64 }.Concat(signature).ToArray();
                var verificationScript = key.signatureScript;
                witList.Add(new Witness() { invocationScript = invocationScript, verificationScript = verificationScript });
            }

            if (witnesses != null)
            {
                foreach (var entry in witnesses)
                {
                    witList.Add(entry);
                }
            }

            this.witnesses = witList.ToArray();
        }

        private UInt256 _hash = null;

        public UInt256 Hash
        {
            get
            {
                if (_hash == null)
                {
                    var rawTx = this.Serialize(false);
                    var hex = rawTx.ByteToHex();
                    _hash = new UInt256(CryptoUtils.Hash256(rawTx));
                }

                return _hash;
            }
        }

        public static Transaction Unserialize(BinaryReader reader)
        {
            var tx = new Transaction();

            tx.type = (TransactionType)reader.ReadByte();
            tx.version = reader.ReadByte();

            switch (tx.type)
            {
                case TransactionType.InvocationTransaction:
                    {
                        var scriptLength = reader.ReadVarInt();
                        tx.script = reader.ReadBytes((int)scriptLength);

                        if (tx.version >= 1)
                        {
                            tx.gas = reader.ReadFixed();
                        }
                        else
                        {
                            tx.gas = 0;
                        }

                        break;
                    }

                case TransactionType.MinerTransaction:
                    {
                        tx.nonce = reader.ReadUInt32();
                        break;
                    }

                case TransactionType.ClaimTransaction:
                    {
                        var len = (int)reader.ReadVarInt((ulong)0x10000000);
                        tx.claimReferences = new Input[len];
                        for (int i = 0; i < len; i++)
                        {
                            tx.claimReferences[i] = Transaction.UnserializeTransactionInput(reader);
                        }

                        break;
                    }

                case TransactionType.ContractTransaction:
                    {
                        break;
                    }

                case TransactionType.StateTransaction:
                    {
                        int descCount = (int)reader.ReadVarInt(16);
                        for (int i = 0; i < descCount; i++)
                        {
                            var Type = /*(StateType)*/reader.ReadByte();
                            var Key = reader.ReadVarBytes(100);
                            var Field = reader.ReadVarString();
                            var Value = reader.ReadVarBytes(65535);
                        }
                        break;
                    }

                case TransactionType.PublishTransaction:
                    {
                        tx.contractRegistration = Contract.Unserialize(reader, tx.version);
                        break;
                    }

                case TransactionType.EnrollmentTransaction:
                    {
                        tx.enrollmentPublicKey = ECPoint.DeserializeFrom(reader, ECCurve.Secp256r1);
                        break;
                    }

                case TransactionType.RegisterTransaction:
                    {
                        tx.assetRegistration = AssetRegistration.Unserialize(reader);
                        break;
                    }

                case TransactionType.IssueTransaction:
                    {
                        break;
                    }

                default:
                    {
                        throw new NotImplementedException();
                    }
            }

            var attrCount = (int)reader.ReadVarInt(16);
            if (attrCount != 0)
            {
                tx.attributes = new TransactionAttribute[attrCount];
                for (int i = 0; i < attrCount; i++)
                {
                    tx.attributes[i] = TransactionAttribute.Unserialize(reader);
                }
            }

            var inputCount = (int)reader.ReadVarInt();
            tx.inputs = new Input[inputCount];
            for (int i = 0; i < inputCount; i++)
            {
                tx.inputs[i] = UnserializeTransactionInput(reader);
            }

            var outputCount = (int)reader.ReadVarInt();
            tx.outputs = new Output[outputCount];
            for (int i = 0; i < outputCount; i++)
            {
                tx.outputs[i] = UnserializeTransactionOutput(reader);
            }

            var witnessCount = (int)reader.ReadVarInt();
            tx.witnesses = new Witness[witnessCount];
            for (int i = 0; i < witnessCount; i++)
            {
                tx.witnesses[i] = Witness.Unserialize(reader);
            }

            return tx;
        }

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
    }

}
