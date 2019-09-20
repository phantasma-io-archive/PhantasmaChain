using System.Linq;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts.Native
{
    public enum ConsensusKind
    {
        Validators,
        Community
    }

    public enum ConsensusMode
    {
        Unanimity,
        Majority,
        Popularity,
    }

    public enum PollState
    {
        Inactive,
        Active,
        Consensus,
        Failure
    }

    public struct PollValue
    {
        public byte[] value;
        public BigInteger votes;
    }

    public struct ConsensusPoll
    {
        public string subject;
        public ConsensusKind kind;
        public ConsensusMode mode;
        public PollState state;
        public PollValue[] values;
        public BigInteger selected;
        public BigInteger round;
        public Timestamp startTime;
        public Timestamp endTime;
        public byte[] script;
    }

    public struct PollPresence
    {
        public string subject;
        public BigInteger round;
    }

    public sealed class ConsensusContract : SmartContract
    {
        public override string Name => "consensus";

        private StorageMap _pollMap; //<Address> 
        private StorageMap _presences; // address, List<PollPresence>

        public const int PollVoteLimit = 50000;

        public const string SystemPoll = "system.";

        public ConsensusContract() : base()
        {
        }

        private ConsensusPoll FetchPoll(string subject)
        {
            var poll = _pollMap.Get<string, ConsensusPoll>(subject);

            if (!Runtime.readOnlyMode)
            {
                if (Runtime.Time < poll.startTime && poll.state != PollState.Inactive)
                {
                    poll.state = PollState.Inactive;
                }
                else
                if (Runtime.Time >= poll.startTime && Runtime.Time<poll.endTime && poll.state == PollState.Inactive)
                {
                    poll.state = PollState.Active;
                }
                else
                if (Runtime.Time >= poll.endTime && poll.state == PollState.Active)
                {
                    // its time to count votes...
                    poll.selected = -1;
                    BigInteger bestVotes = -1;
                    BigInteger totalVotes = 0;
                    int ties = 0;

                    for (int i=0; i<poll.values.Length; i++)
                    {
                        var entry = poll.values[i];
                        totalVotes += entry.votes;

                        if (entry.votes > bestVotes)
                        {
                            bestVotes = entry.votes;
                            poll.selected = i;
                            ties = 0;
                        }
                        else
                        if (entry.votes == bestVotes)
                        {
                            ties++;
                        }
                    }

                    BigInteger percentage = (bestVotes * 100) / totalVotes;

                    if (poll.selected == -1)
                    {
                        poll.state = PollState.Failure;
                    }
                    else
                    {
                        if (poll.mode == ConsensusMode.Unanimity && percentage < 100)
                        {
                            poll.state = PollState.Failure;
                        }
                        else
                        if (poll.mode == ConsensusMode.Majority && percentage < 51)
                        {
                            poll.state = PollState.Failure;
                        }
                        else
                        if (poll.mode == ConsensusMode.Popularity && ties > 0)
                        {
                            poll.state = PollState.Failure;
                        }
                        else
                        {
                            poll.state = PollState.Consensus;
                        }
                    }

                    _pollMap.Set<string, ConsensusPoll>(subject, poll);
                }
            }

            return poll;
        }

        public void InitPoll(Address from, string subject, ConsensusKind kind, ConsensusMode mode, byte[] script)
        {
            if (subject.StartsWith(SystemPoll))
            {
                Runtime.Expect(IsValidator(from), "must be validator");
            }
        }

        public void Vote(Address from, string subject)
        {
            Runtime.Expect(_pollMap.ContainsKey<string>(subject), "invalid subject");

            var poll = FetchPoll(subject);

            Runtime.Expect(poll.state == PollState.Active, "poll not active");

            Runtime.Expect(IsWitness(from), "invalid witness");

            var presences = _presences.Get<Address, StorageList>(from);
        }

        public bool HasConsensus(string subject, byte[] value)
        {
            Runtime.Expect(_pollMap.ContainsKey<string>(subject), "invalid subject");

            var poll = FetchPoll(subject);

            if (poll.state == PollState.Consensus && poll.selected>=0 && poll.selected<poll.values.Length)
            {
                var winner = poll.values[(int)poll.selected].value;
                return value.SequenceEqual(winner);
            }

            return false;
        }
    }
}
