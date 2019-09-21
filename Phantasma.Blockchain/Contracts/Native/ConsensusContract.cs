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
        Ranking,
    }

    public enum PollState
    {
        Inactive,
        Active,
        Consensus,
        Failure
    }

    public struct PollChoice
    {
        public byte[] value;
    }

    public struct PollValue
    {
        public byte[] value;
        public BigInteger ranking;
        public BigInteger votes;
    }

    public struct PollVote
    {
        public BigInteger index;
        public BigInteger percentage;
    }

    public struct ConsensusPoll
    {
        public string subject;
        public ConsensusKind kind;
        public ConsensusMode mode;
        public PollState state;
        public PollValue[] entries;
        public BigInteger round;
        public Timestamp startTime;
        public Timestamp endTime;
        public BigInteger votesPerUser;
        public BigInteger totalVotes;
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
        private StorageList _pollList; 
        private StorageMap _presences; // address, List<PollPresence>

        public const int MinimumPollLength = 86400;
        public const string MaximumPollLengthTag = "poll.max.length";
        public const string MaxEntriesPerPollTag = "poll.max.entries";
        public const string PollVoteLimitTag = "poll.vote.limit";

        public const string SystemPoll = "system.";

        public ConsensusContract() : base()
        {
        }

        private ConsensusPoll FetchPoll(string subject)
        {
            var poll = _pollMap.Get<string, ConsensusPoll>(subject);

            if (!Runtime.readOnlyMode)
            {
                var MaxVotesPerPoll = Runtime.GetGovernanceValue(PollVoteLimitTag);

                if (Runtime.Time < poll.startTime && poll.state != PollState.Inactive)
                {
                    poll.state = PollState.Inactive;
                }
                else
                if (Runtime.Time >= poll.startTime && Runtime.Time<poll.endTime && poll.state == PollState.Inactive)
                {
                    poll.state = PollState.Active;
                    _pollList.Add<string>(subject);
                }
                else
                if ((Runtime.Time >= poll.endTime || poll.totalVotes >= MaxVotesPerPoll) && poll.state == PollState.Active)
                {
                    // its time to count votes...
                    BigInteger totalVotes = 0;
                    for (int i = 0; i < poll.entries.Length; i++)
                    {
                        var entry = poll.entries[i];
                        totalVotes += entry.votes;
                    }

                    var rankings = poll.entries.OrderByDescending(x => x.votes).ToArray();

                    var winner = rankings[0];
                    int ties = 0;

                    for (int i = 1; i < rankings.Length; i++)
                    {
                        if (rankings[i].votes == winner.votes)
                        {
                            ties++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    for (int i = 0; i < poll.entries.Length; i++)
                    {
                        var val = poll.entries[i].value;
                        int index = -1;
                        for (int j = 0; j < rankings.Length; j++)
                        {
                            if (rankings[j].value == val)
                            {
                                index = j;
                                break;
                            }
                        }
                        Runtime.Expect(index >= 0, "missing entry in poll rankings");

                        poll.entries[i].ranking = index + 1;
                    }

                    BigInteger percentage = (winner.votes * 100) / totalVotes;

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

                    _pollMap.Set<string, ConsensusPoll>(subject, poll);

                    Runtime.Notify(EventKind.PollFinished, this.Address, subject);
                }
            }

            return poll;
        }

        public void InitPoll(Address from, string subject, ConsensusKind kind, ConsensusMode mode, Timestamp startTime, Timestamp endTime, PollChoice[] choices, BigInteger votesPerUser, byte[] script)
        {
            if (subject.StartsWith(SystemPoll))
            {
                Runtime.Expect(IsValidator(from), "must be validator");
                Runtime.Expect(mode == ConsensusMode.Majority, "must use majority mode for system governance");
            }

            Runtime.Expect(Runtime.Chain.IsRoot, "not root chain");

            Runtime.Expect(kind == ConsensusKind.Validators, "community polls not yet");

            var maxEntriesPerPoll = Runtime.GetGovernanceValue(MaxEntriesPerPollTag);
            Runtime.Expect(choices.Length > 1, "invalid amount of entries");
            Runtime.Expect(choices.Length <= maxEntriesPerPoll, "too many entries");

            var MaximumPollLength = (uint)Runtime.GetGovernanceValue(MaximumPollLengthTag);

            Runtime.Expect(startTime >= Runtime.Time, "invalid start time");
            var minEndTime = new Timestamp(startTime.Value + MinimumPollLength);
            var maxEndTime = new Timestamp(startTime.Value + MaximumPollLength);
            Runtime.Expect(endTime >= minEndTime, "invalid end time");
            Runtime.Expect(endTime <= maxEndTime, "invalid end time");

            Runtime.Expect(script != null && script.Length > 0, "invalid script");

            Runtime.Expect(votesPerUser > 0, "number of votes per user too low");
            Runtime.Expect(votesPerUser < choices.Length, "number of votes per user too high");

            Runtime.Expect(IsWitness(from), "invalid witness");

            ConsensusPoll poll;
            if (_pollMap.ContainsKey<string>(subject))
            {
                poll = FetchPoll(subject);
                Runtime.Expect(poll.state == PollState.Consensus || poll.state == PollState.Failure, "poll already in progress");
                poll.round += 1;
                poll.state = PollState.Inactive;
            }
            else
            {
                poll = new ConsensusPoll();
                poll.subject = subject;
                poll.round = 1;
            }

            poll.startTime = startTime;
            poll.endTime = endTime;
            poll.kind = kind;
            poll.mode = mode;
            poll.script = script;
            poll.state = PollState.Inactive;
            poll.votesPerUser = votesPerUser;
            poll.totalVotes = 0;

            poll.entries = new PollValue[choices.Length];
            for (int i=0; i<choices.Length; i++)
            {
                poll.entries[i] = new PollValue()
                {
                    ranking = -1,
                    value = choices[i].value,
                    votes = 0
                };
            }

            _pollMap.Set<string, ConsensusPoll>(subject, poll);

            Runtime.Notify(EventKind.PollStarted, this.Address, subject);
        }

        public void SingleVote(Address from, string subject, BigInteger index)
        {
            MultiVote(from, subject, new PollVote[] { new PollVote() { index = index, percentage = 100 } });
        }

        public void MultiVote(Address from, string subject, PollVote[] choices)
        {
            Runtime.Expect(_pollMap.ContainsKey<string>(subject), "invalid subject");

            Runtime.Expect(choices.Length > 0, "invalid number of choices");

            var poll = FetchPoll(subject);

            Runtime.Expect(poll.state == PollState.Active, "poll not active");

            Runtime.Expect(choices.Length <= poll.votesPerUser, "too many choices");

            Runtime.Expect(IsWitness(from), "invalid witness");

            var presences = _presences.Get<Address, StorageList>(from);
            var count = presences.Count();
            int index = -1;
            BigInteger round = 0;

            for (int i=0; i<count; i++)
            {
                var presence = presences.Get<PollPresence>(i);
                if (presence.subject == subject)
                {
                    index = -1;
                    round = presence.round;
                    break;
                }
            }

            if (index >= 0)
            {
                Runtime.Expect(round < poll.round, "already voted");
            }

            BigInteger votingPower;

            if (poll.kind == ConsensusKind.Validators)
            {
                votingPower = 100;
            }
            else
            {
                votingPower = (BigInteger)Runtime.CallContext("energy", "GetAddressVotingPower", from);
            }

            Runtime.Expect(votingPower > 0, "not enough voting power");

            for (int i=0; i<choices.Length; i++)
            {
                var votes = (votingPower * choices[i].percentage) / 100;
                Runtime.Expect(votes > 0, "choice percentage is too low");

                var targetIndex = (int)choices[i].index;
                poll.entries[targetIndex].votes += votes;
            }

            poll.totalVotes += 1;
            _pollMap.Set<string, ConsensusPoll>(subject, poll);

            // finally add this voting round to the presences list
            var temp = new PollPresence()
            {
                subject = subject,
                round = poll.round,
            };

            if (index >= 0)
            {
                presences.Replace<PollPresence>(index, temp);
            }
            else
            {
                presences.Add(temp);
            }

            Runtime.Notify(EventKind.PollVote, from, subject);
        }

        public bool HasConsensus(string subject, byte[] value)
        {
            if (subject.StartsWith(SystemPoll))
            {
                var validatorCount = (BigInteger)Runtime.CallContext("validator", "GetValidatorCount");
                if (validatorCount == 1)
                {
                    return true;
                }
            }

            Runtime.Expect(_pollMap.ContainsKey<string>(subject), "invalid subject");

            var poll = FetchPoll(subject);
            if (poll.state != PollState.Consensus)
            {
                return false;
            }

            int index = -1;
            for (int i = 0; i < poll.entries.Length; i++)
            {
                if (poll.entries[i].value.SequenceEqual(value))
                {
                    return poll.entries[i].ranking == 0;
                }
            }

            return false;
        }

        // note this returns true if the rank is same or better than the argument
        public bool HasRank(string subject, byte[] value, BigInteger rank)
        {
            if (subject.StartsWith(SystemPoll))
            {
                var validatorCount = (BigInteger)Runtime.CallContext("validator", "GetValidatorCount");
                if (validatorCount == 1)
                {
                    return true;
                }
            }

            Runtime.Expect(_pollMap.ContainsKey<string>(subject), "invalid subject");

            var poll = FetchPoll(subject);
            if (poll.state != PollState.Consensus)
            {
                return false;
            }

            for (int i = 0; i < poll.entries.Length; i++)
            {
                if (poll.entries[i].value.SequenceEqual(value))
                {
                    return poll.entries[i].ranking <= rank;
                }
            }

            return false;
        }
    }
}
