using System;

namespace Phantasma.Consensus
{
    public enum RaftState
    {
        Follower,
        Candidate,
        Leader
    }
}
