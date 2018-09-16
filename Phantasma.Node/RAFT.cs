using System;
using System.Collections.Generic;
using Phantasma.Cryptography;

// TODO move to proper place
namespace Phantasma.Blockchain.Consensus
{
    public sealed partial class Node
    {
        public const int FollowerTimeOut = 1000;

        private RaftState State;
        private Address Leader = Address.Null;
        private Address Vote = Address.Null;

        private HashSet<Address> ReceivedVotes = new HashSet<Address>();

        //private int CurrentTerm = 0;
        private DateTime _lastLeaderBeat;

        private void SetState(RaftState newState)
        {
            if (newState == this.State)
            {
                return;
            }

            this.State = newState;
        }

        private void UpdateRAFT()
        {
            var currentTime = DateTime.UtcNow;

            switch (this.State)
            {
                case RaftState.Invalid: {
                        _lastLeaderBeat = DateTime.UtcNow;
                        SetState(RaftState.Follower);
                        break;                    
                    }

                case RaftState.Follower:
                case RaftState.Candidate:
                    {
                        var diff = (currentTime - _lastLeaderBeat).TotalMilliseconds;

                        if (diff >= FollowerTimeOut)
                        {
                            SetState(RaftState.Candidate);

                            // clear votes then vote for self
                            ReceivedVotes.Clear();
                            ReceivedVotes.Add(this.Address); 

                            // TODO send to all peers vote request
                        }

                        break;
                    }
            }
        }
    }
}
