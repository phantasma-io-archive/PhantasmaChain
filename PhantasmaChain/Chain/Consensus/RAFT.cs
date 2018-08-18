using System;
using System.Collections.Generic;
using Phantasma.Utils;

namespace Phantasma.Blockchain.Consensus
{
    public sealed partial class Node
    {
        public const int FollowerTimeOut = 1000;

        private RaftState State;
        private byte[] Leader = null;
        private byte[] Vote = null;

        private HashSet<byte[]> ReceivedVotes = new HashSet<byte[]>(new ByteArrayComparer());

        private int CurrentTerm = 0;
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
                            ReceivedVotes.Add(this.PublicKey); 

                            // TODO send to all peers vote request
                        }

                        break;
                    }
            }
        }
    }
}
