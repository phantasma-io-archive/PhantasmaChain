using Phantasma.Cryptography;
using System;

//TODO
namespace Phantasma.Domain
{
    public class ChainException : Exception
    {
        public ChainException(string msg) : base(msg)
        {

        }
    }

    public class ContractException : Exception
    {
        public ContractException(string msg) : base(msg)
        {

        }
    }

    public class ArchiveException : Exception
    {
        public ArchiveException(string msg) : base(msg)
        {

        }
    }

    public class RelayException : Exception
    {
        public RelayException(string msg) : base(msg)
        {

        }
    }

    public class OracleException : Exception
    {
        public OracleException(string msg) : base(msg)
        {

        }
    }

    public class SwapException : Exception
    {
        public SwapException(string msg) : base(msg)
        {

        }
    }

    public class NodeException : Exception
    {
        public NodeException(string msg) : base(msg)
        {

        }
    }


    public class BlockGenerationException : Exception
    {
        public BlockGenerationException(string msg) : base(msg)
        {

        }
    }

    public class DuplicatedTransactionException : Exception
    {
        public readonly Hash Hash;

        public DuplicatedTransactionException(Hash hash, string msg) : base(msg)
        {
            this.Hash = hash;
        }
    }

    public class InvalidTransactionException : Exception
    {
        public readonly Hash Hash;

        public InvalidTransactionException(Hash hash, string msg) : base(msg)
        {
            this.Hash = hash;
        }
    }
}
