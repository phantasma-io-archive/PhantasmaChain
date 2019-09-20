using System;
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
        public BigInteger selected;
        public BigInteger round;
        public Timestamp startTime;
        public Timestamp endTime;
        public BigInteger votesPerUser;
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

        public const int MaxEntriesPerPoll = 100;
        private const int MinimumPollLength = 86400;
        private const int MaximumPollLength = MinimumPollLength * 90;
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
                    _pollList.Add<string>(subject);
                }
                else
                if (Runtime.Time >= poll.endTime && poll.state == PollState.Active)
                {
                    // its time to count votes...
                    poll.selected = -1;
                    BigInteger bestVotes = -1;
                    BigInteger totalVotes = 0;
                    int ties = 0;

                    for (int i=0; i<poll.entries.Length; i++)
                    {
                        var entry = poll.entries[i];
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

                    Runtime.Notify(EventKind.PollFinished, this.Address, subject);
                }
            }

            return poll;
        }

        public void InitPoll(Address from, string subject, ConsensusKind kind, ConsensusMode mode, Timestamp startTime, Timestamp endTime, PollValue[] entries, BigInteger votesPerUser, byte[] script)
        {
            if (subject.StartsWith(SystemPoll))
            {
                Runtime.Expect(IsValidator(from), "must be validator");
            }

            Runtime.Expect(Runtime.Chain.IsRoot, "not root chain");

            Runtime.Expect(kind == ConsensusKind.Validators, "community polls not yet");

            Runtime.Expect(entries.Length > 1, "invalid amount of entries");
            Runtime.Expect(entries.Length <= MaxEntriesPerPoll, "too many entries");

            Runtime.Expect(startTime >= Runtime.Time, "invalid start time");
            var minEndTime = new Timestamp(startTime.Value + MinimumPollLength);
            var maxEndTime = new Timestamp(startTime.Value + MaximumPollLength);
            Runtime.Expect(endTime >= minEndTime, "invalid end time");
            Runtime.Expect(endTime <= maxEndTime, "invalid end time");

            Runtime.Expect(script != null && script.Length > 0, "invalid script");

            Runtime.Expect(votesPerUser > 0, "number of votes per user too low");
            Runtime.Expect(votesPerUser < entries.Length, "number of votes per user too high");

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
            poll.entries = entries;
            poll.kind = kind;
            poll.mode = mode;
            poll.script = script;
            poll.selected = -1;
            poll.state = PollState.Inactive;
            poll.votesPerUser = votesPerUser;

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

            if (poll.state == PollState.Consensus && poll.selected>=0 && poll.selected<poll.entries.Length)
            {
                var winner = poll.entries[(int)poll.selected].value;
                return value.SequenceEqual(winner);
            }

            return false;
        }
    }
}
