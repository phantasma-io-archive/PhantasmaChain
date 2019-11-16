using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Contracts.Extra
{
    public struct BoardInfo
    {
        public string Name;
        public string Symbol;
        public BigInteger Cost;
        public byte[] Pieces;
        public uint Seed;
        public BigInteger Pot;

        public BoardInfo(string name, string symbol, BigInteger cost, byte[] pieces, uint seed, BigInteger pot)
        {
            Name = name;
            Symbol = symbol;
            Cost = cost;
            Pieces = pieces;
            Seed = seed;
            Pot = pot;
        }
    }

    public sealed class TetraminoContract : SmartContract
    {
        public override string Name => "tetramino";

        internal StorageMap _players;
        internal StorageList _boards;

        public TetraminoContract() : base()
        {
        }
    }
}
