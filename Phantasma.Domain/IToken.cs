using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.Domain
{
    [Flags]
    public enum TokenFlags
    {
        None = 0,
        Transferable = 1 << 0,
        Fungible = 1 << 1,
        Finite = 1 << 2,
        Divisible = 1 << 3,
        Fuel = 1 << 4,
        Stakable = 1 << 5,
        Fiat = 1 << 6,
        Foreign = 1 << 7,
        Burnable = 1 << 8,
    }

    public struct TokenInfusion
    {
        public readonly string Symbol;
        public readonly BigInteger Value;

        public TokenInfusion(string symbol, BigInteger value)
        {
            Symbol = symbol;
            Value = value;
        }
    }

    public enum TokenSeriesMode
    {
        Unique,
        Duplicated
    }

    public interface IToken
    {
        string Name { get; }
        string Symbol { get; }
        Address Owner { get; }
        TokenFlags Flags { get; }
        BigInteger MaxSupply { get;  }
        int Decimals { get; }
        byte[] Script { get; }
        ContractInterface ABI { get; }
    }

    public struct PackedNFTData
    {
        public readonly string Symbol;
        public readonly byte[] ROM;
        public readonly byte[] RAM;

        public PackedNFTData(string symbol, byte[] rom, byte[] ram)
        {
            Symbol = symbol;
            ROM = rom;
            RAM = ram;
        }
    }

    public interface ITokenSeries: ISerializable
    {
        BigInteger MintCount { get; }
        BigInteger MaxSupply { get; }
        TokenSeriesMode Mode { get; }
        byte[] Script { get;  }
        ContractInterface ABI { get; }
        byte[] ROM { get; }
    }

    public struct TokenContent : ISerializable
    {
        // sizes in bytes
        // TODO find optimal values for this
        public static readonly int MaxROMSize = 1024;
        public static readonly int MaxRAMSize = 1024;

        public TokenContent(BigInteger seriesID, BigInteger mintID, string currentChain, Address creator, Address currentOwner, byte[] ROM, byte[] RAM, Timestamp timestamp, IEnumerable<TokenInfusion> infusion, TokenSeriesMode mode) : this()
        {
            this.SeriesID = seriesID;
            this.MintID = mintID;
            this.Creator = creator;
            CurrentChain = currentChain;
            CurrentOwner = currentOwner;
            this.ROM = ROM;
            this.RAM = RAM;
            this.Timestamp = timestamp;
            this.Infusion = infusion != null ? infusion.ToArray(): new TokenInfusion[0];

            UpdateTokenID(mode);
        }

        public string CurrentChain { get; private set; }
        public Address CurrentOwner { get; private set; }
        public Address Creator { get; private set; }
        public byte[] ROM { get; private set; }
        public byte[] RAM { get; private set; }

        public BigInteger SeriesID { get; private set; }
        public BigInteger MintID { get; private set; }

        public BigInteger TokenID { get; private set; }

        public TokenInfusion[] Infusion { get; private set; }

        public Timestamp Timestamp { get; private set; }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteBigInteger(SeriesID);
            writer.WriteBigInteger(MintID);
            writer.WriteAddress(Creator);
            writer.WriteVarString(CurrentChain);
            writer.WriteAddress(CurrentOwner);
            writer.WriteByteArray(ROM);
            writer.WriteByteArray(RAM);
            writer.Write(Timestamp.Value);
            writer.WriteVarInt(Infusion.Length);
            foreach (var entry in Infusion)
            {
                writer.WriteVarString(entry.Symbol);
                writer.WriteBigInteger(entry.Value);
            }
        }

        public void UnserializeData(BinaryReader reader)
        {
            SeriesID = reader.ReadBigInteger();
            MintID = reader.ReadBigInteger();

            Creator = reader.ReadAddress();

            CurrentChain = reader.ReadVarString();
            CurrentOwner = reader.ReadAddress();

            ROM = reader.ReadByteArray();
            ROM = ROM ?? new byte[0];

            RAM = reader.ReadByteArray();
            RAM = RAM ?? new byte[0];

            Timestamp = new Timestamp(reader.ReadUInt32());

            var infusionCount = (int)reader.ReadVarInt();
            this.Infusion = new TokenInfusion[infusionCount];
            for (int i = 0; i < infusionCount; i++)
            {
                var symbol = reader.ReadVarString();
                var value = reader.ReadBigInteger();
                Infusion[i] = new TokenInfusion(symbol, value);
            }
        }

        public void UpdateTokenID(TokenSeriesMode mode)
        {
            byte[] bytes;

            switch (mode)
            {
                case TokenSeriesMode.Unique: bytes = ROM; break;
                case TokenSeriesMode.Duplicated: bytes = ROM.Concat(SeriesID.ToUnsignedByteArray()).Concat(MintID.ToUnsignedByteArray()).ToArray(); break;
                default:
                    throw new ChainException($"Generation of tokenID for Series with {mode} is not implemented");
            }

            this.TokenID = Hash.FromBytes(bytes);
        }

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    SerializeData(writer);
                }

                return stream.ToArray();
            }
        }

        public void ReplaceROM(byte[] newROM)
        {
            this.ROM = newROM;
        }
    }


    public static class TokenUtils
    {
        public static Address GetContractAddress(this IToken token)
        {
            return GetContractAddress(token.Symbol);
        }

        public static Address GetContractAddress(string symbol)
        {
            return SmartContract.GetAddressForName(symbol);
        }

        public static IEnumerable<ContractMethod> GetTriggersForABI(IEnumerable<TokenTrigger> triggers)
        {
            var entries = new Dictionary<TokenTrigger, int>();
            foreach (var trigger in triggers)
            {
                entries[trigger] = 0;
            }

            return GetTriggersForABI(entries);
        }

        public static IEnumerable<ContractMethod> GetTriggersForABI(Dictionary<TokenTrigger, int> triggers)
        {
            var result = new List<ContractMethod>();

            foreach (var entry in triggers)
            {
                var trigger = entry.Key;
                var offset = entry.Value;

                switch (trigger)
                {
                    case TokenTrigger.OnBurn:
                    case TokenTrigger.OnMint:
                    case TokenTrigger.OnReceive:
                    case TokenTrigger.OnSend:
                        result.Add(new ContractMethod(trigger.ToString(), VM.VMType.None, offset, new[] {
                            new ContractParameter("from", VM.VMType.Object),
                            new ContractParameter("to", VM.VMType.Object),
                            new ContractParameter("symbol", VM.VMType.String),
                            new ContractParameter("amount", VM.VMType.Number)
                        }));
                        break;

                    default:
                        throw new System.Exception("AddTriggerToABI: Unsupported trigger: " + trigger);
                }
            }

            return result;
        }

    }

}
