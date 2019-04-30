using Phantasma.Blockchain.Storage;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct CasinoQueue
    {
        public Address player;
        public BigInteger amount;
        public string symbol;
        public BigInteger table;
    }

    public struct CasinoMatch
    {
        public Address host;
        public Address opponent;
        public BigInteger amount;
        public Timestamp timestamp;
        public BigInteger matchTurn;
        public BigInteger hostTurn;
        public BigInteger opponentTurn;
        public byte[] cards;
    }

    public sealed class CasinoContract : SmartContract
    {
        public override string Name => "casino";

        internal StorageList _queue;

        internal StorageMap _matchMap;
        internal StorageMap _matchData;

        internal BigInteger _matchCount;

        public CasinoContract() : base()
        {
        }

        public BigInteger GetTableFee(BigInteger tableID)
        {
            return UnitConversion.ToBigInteger(1, Nexus.StakingTokenDecimals);
        }

        public void Queue(Address from, string symbol, BigInteger tableID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(tableID == 0, "invalid table");
 
            var token = Runtime.Nexus.StakingToken;
            Runtime.Expect(symbol == token.Symbol, "invalid symbol");

            Runtime.Expect(!_matchMap.ContainsKey<Address>(from), "already in match");

            var balances = Runtime.Chain.GetTokenBalances(token);
            var fee = GetTableFee(tableID);
            Runtime.Expect(token.Transfer(this.Storage, balances, from, Runtime.Chain.Address, fee), "fee transfer failed");

            int queueIndex = -1;
            var count = _queue.Count();
            for (var i=0; i<count; i++)
            {
                var temp = _queue.Get<CasinoQueue>(i);
                if (temp.table == tableID)
                {
                    queueIndex = i;
                    break;
                }
            }

            if (queueIndex >= 0)
            {
                var other = _queue.Get<CasinoQueue>(queueIndex);

                _matchCount++;

                _matchMap.Set<Address, BigInteger>(from, _matchCount);
                _matchMap.Set<Address, BigInteger>(other.player, _matchCount);

                Runtime.Notify(EventKind.CasinoTableStart, from, other.player);
                Runtime.Notify(EventKind.CasinoTableStart, other.player, from);

                var match = new CasinoMatch()
                {
                    amount = fee * 2,
                    host = other.player,
                    opponent = from ,
                    matchTurn = 0,
                    hostTurn = 0,
                    opponentTurn = 0,
                    timestamp = Runtime.Time,
                    cards = new byte[52],
                };

                for (int j = 0; j < 2; j++)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var card = DrawCard(match.cards, (byte)(i+1));
                        Runtime.Expect(card >= 0, "card draw failed");
                        Runtime.Notify(EventKind.CasinoTableCard, i == 0 ? match.host : match.opponent, i);
                    }
                }

                _matchData.Set<BigInteger, CasinoMatch>(_matchCount, match);
            }
            else
            {
                var entry = new CasinoQueue() { player = from, amount = fee, symbol = symbol, table = tableID };
                _queue.Add<CasinoQueue>(entry);
                Runtime.Notify(EventKind.CasinoTableQueued, from, tableID);
            }
        }

        private int DrawCard(byte[] cards, byte playerIndex)
        {
            var index = (int)(Runtime.NextRandom() % 52);
            bool failure = false;

            while (cards[index] != 0)
            {
                index++;
                if (index >= 52)
                {
                    if (failure)
                    {
                        return -1;
                    }
                    failure = true;
                    index = 0;
                }
            }

            cards[index] = playerIndex;
            return index;
        }

        private int GetCardValue(int card)
        {
            card = card % 13;
            if (card >= 9)
            {
                return 10;
            }

            return card + 1;
        }

        private int GetHandValue(byte[] cards, byte playerIndex)
        {
            int result = 0;

            for (int i=0; i<cards.Length; i++)
            {
                if (cards[i] == playerIndex)
                {
                    result += GetCardValue(i);
                }
            }

            return result;
        }

        public void PlayTurn(Address from, BigInteger turn, BigInteger action)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(_matchMap.ContainsKey<Address>(from), "not in match");

            var matchID = _matchMap.Get<Address, BigInteger>(from);
            var match = _matchData.Get<BigInteger, CasinoMatch>(matchID);

            Runtime.Expect(match.matchTurn == turn - 1, "invalid match turn");

            byte playerIndex;

            if (from == match.host)
            {
                Runtime.Expect(match.hostTurn == turn - 1, "invalid player turn");
                match.hostTurn = turn;
                playerIndex = 1;
            }
            else
            {
                Runtime.Expect(match.opponentTurn == turn - 1, "invalid player turn");
                match.opponentTurn = turn;
                playerIndex = 2;
            }

            switch ((int)action)
            {
                case 0: // stand
                    break;

                case 1: // hit
                    var card = DrawCard(match.cards, playerIndex);
                    Runtime.Expect(card >= 0, "card draw failed");

                    var value = GetHandValue(match.cards, playerIndex);
                    if (value > 21) // lost
                    {

                    }
                    else
                    if (value == 21)
                    {

                    }
                    break;

                case 2: // surrender
                    break;
            }

            // check if turn finished
            if (match.opponentTurn == match.hostTurn)
            {
                match.matchTurn++;
                Runtime.Notify(EventKind.CasinoTableTurn, match.host, match.matchTurn);
                Runtime.Notify(EventKind.CasinoTableTurn, match.opponent, match.matchTurn);
            }
        }

    }
}
