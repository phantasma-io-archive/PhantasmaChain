using System;

namespace Phantasma.Consensus
{
    public enum RaftState
    {
        Invalid,
        Follower,
        Candidate,
        Leader
    }
}
