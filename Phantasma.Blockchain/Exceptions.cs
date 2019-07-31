using System;

//TODO
namespace Phantasma.Blockchain
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

}
