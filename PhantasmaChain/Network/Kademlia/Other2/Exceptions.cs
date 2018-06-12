using System;

namespace Phantasma.Kademlia
{
    public class IDLengthException : Exception
    {
        public IDLengthException() { }
        public IDLengthException(string msg) : base(msg) { }
    }

    public class TooManyContactsException : Exception
    {
        public TooManyContactsException() { }
        public TooManyContactsException(string msg) : base(msg) { }
    }

	public class OurNodeCannotBeAContactException : Exception
	{
		public OurNodeCannotBeAContactException() { }
		public OurNodeCannotBeAContactException(string msg) : base(msg) { }
	}

    public class NoNonEmptyBucketsException : Exception
    {
        public NoNonEmptyBucketsException() { }
        public NoNonEmptyBucketsException(string msg) : base(msg) { }
    }

    public class SendingQueryToSelfException : Exception
    {
        public SendingQueryToSelfException() { }
        public SendingQueryToSelfException(string msg) : base(msg) { }
    }

    public class ValueCannotBeNullException : Exception
    {
        public ValueCannotBeNullException() { }
        public ValueCannotBeNullException(string msg) : base(msg) { }
    }

    public class NotAnIDException : Exception
    {
        public NotAnIDException() { }
        public NotAnIDException(string msg) : base(msg) { }
    }

    public class NotContactException : Exception
    {
        public NotContactException() { }
        public NotContactException(string msg) : base(msg) { }
    }

    public class NullIDException : Exception
    {
        public NullIDException() { }
        public NullIDException(string msg) : base(msg) { }
    }

    public class BadIDException : Exception
    {
        public BadIDException() { }
        public BadIDException(string msg) : base(msg) { }
    }

    public class RpcException : Exception
    {
        public RpcException() { }
        public RpcException(string msg) : base(msg) { }
    }

    public class IDMismatchException : Exception
    {
        public IDMismatchException() { }
        public IDMismatchException(string msg) : base(msg) { }
    }

    public class BucketDoesNotContainContactToEvict : Exception
    {
        public BucketDoesNotContainContactToEvict() { }
        public BucketDoesNotContainContactToEvict(string msg) : base(msg) { }
    }
}
