using Phantasma.Cryptography.ECC;
using Phantasma.Neo.Cryptography;
using Phantasma.Neo.Utils;
using System;
using System.IO;

namespace Phantasma.Neo.Core
{
    public class Asset
    {
        public UInt256 hash;
        public string name;
    }

    public enum AssetType : byte
    {
        CreditFlag = 0x40,
        DutyFlag = 0x80,

        GoverningToken = 0x00,
        UtilityToken = 0x01,
        Currency = 0x08,
        Share = DutyFlag | 0x10,
        Invoice = DutyFlag | 0x18,
        Token = CreditFlag | 0x20,
    }

    public class AssetRegistration
    {
        public AssetType type;
        public String name;
        public decimal amount;
        public byte precision;
        public ECPoint owner;
        public UInt160 admin;

        internal void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)this.type);
            writer.WriteVarString(this.name);
            writer.WriteFixed(this.amount);
            writer.Write((byte)this.precision);
            writer.Write(this.owner.EncodePoint(true));
            writer.Write(this.admin.ToArray());
        }

        public static AssetRegistration Unserialize(BinaryReader reader)
        {
            var reg = new AssetRegistration();
            reg.type = (AssetType)reader.ReadByte();
            reg.name = reader.ReadVarString();
            reg.amount = reader.ReadFixed();
            reg.precision = reader.ReadByte();
            reg.owner = ECPoint.DeserializeFrom(reader, ECCurve.Secp256r1);
            if (reg.owner.IsInfinity && reg.type != AssetType.GoverningToken && reg.type != AssetType.UtilityToken)
                throw new FormatException();
            reg.admin = new UInt160(reader.ReadBytes(20));
            return reg;
        }
    }
}
